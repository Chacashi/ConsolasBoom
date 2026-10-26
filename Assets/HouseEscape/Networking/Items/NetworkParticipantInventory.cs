using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Items;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Flashlight;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Vitality;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Items
{
    /// <summary>
    /// Server adapter for the pure <see cref="ItemInventoryRoster"/>. Registers every spawned
    /// participant avatar with its spawn slot's starting items and consumes items on validated
    /// client intentions: the sender must own the participant and its avatar, the participant must
    /// be conscious in an active match, and must carry a usable item of that type. Health and
    /// energy gains are applied through <see cref="NetworkParticipantVitality"/> and battery gains
    /// through <see cref="NetworkParticipantFlashlights"/>. Counts are replicated per participant.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantInventory : NetworkBehaviour, IItemInventoryReadSource
    {
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantFlashlights flashlights;

        private NetworkList<NetworkItemInventoryRecord> _records;
        private ItemInventoryRoster _roster;
        private bool _serverSubscribed;

        public event Action Changed;

        private void Awake()
        {
            _records = new NetworkList<NetworkItemInventoryRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public void Initialize(
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantFlashlights explicitFlashlights)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The participant inventory must be configured before it is spawned.");
            }

            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            vitality = explicitVitality;
            flashlights = explicitFlashlights;
            ValidateDependencies();
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _records.OnListChanged += HandleRecordsChanged;
            if (IsServer)
            {
                _roster = new ItemInventoryRoster();
                _records.Clear();
                avatarSpawner.AvatarSpawned += HandleAvatarSpawned;
                _serverSubscribed = true;
                IReadOnlyList<NetworkParticipantAvatar> avatars = avatarSpawner.SpawnedAvatars;
                for (int index = 0; index < avatars.Count; index++)
                {
                    HandleAvatarSpawned(avatars[index]);
                }
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (_serverSubscribed)
            {
                avatarSpawner.AvatarSpawned -= HandleAvatarSpawned;
                _serverSubscribed = false;
            }

            _records.OnListChanged -= HandleRecordsChanged;
            _roster = null;
            Changed?.Invoke();
        }

        public bool TryGetItems(ParticipantId participantId, out ParticipantItems items)
        {
            if (_records != null && participantId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].ParticipantId == participantId.Value)
                    {
                        items = _records[index].ToItems();
                        return true;
                    }
                }
            }

            items = default;
            return false;
        }

        public void RequestUseItem(ParticipantId participantId, ItemType type)
        {
            if (IsSpawned && IsClient && participantId.IsValid && ItemRules.IsValid(type))
            {
                UseItemRpc(participantId.Value, (byte)type);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UseItemRpc(ulong requestedParticipantId, byte itemType, RpcParams rpcParams = default)
        {
            TryUseItemOnServer(
                requestedParticipantId == 0 ? ParticipantId.None : new ParticipantId(requestedParticipantId),
                (ItemType)itemType,
                rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and consumes one item on behalf of <paramref name="senderClientId"/>.</summary>
        public ItemUseRequestResult TryUseItemOnServer(ParticipantId participantId, ItemType type, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                return ItemUseRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return ItemUseRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return ItemUseRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                || avatar == null
                || !avatar.IsSpawned
                || avatar.NetworkObject.OwnerClientId != senderClientId)
            {
                return ItemUseRequestResult.AvatarUnavailable;
            }

            switch (_roster.CanUse(participantId, type))
            {
                case ItemUseResult.Used:
                    break;
                case ItemUseResult.InvalidItem:
                    return ItemUseRequestResult.InvalidItem;
                case ItemUseResult.NoneLeft:
                    return ItemUseRequestResult.NoneLeft;
                case ItemUseResult.NotUsable:
                    return ItemUseRequestResult.NotUsable;
                default:
                    return ItemUseRequestResult.InvalidParticipant;
            }

            if (!vitality.CanActOnServer(participantId))
            {
                return ItemUseRequestResult.CannotAct;
            }

            float batteryGain = ItemRules.GetBatteryGain(type);
            if (batteryGain > 0f)
            {
                flashlights.AddBatteryOnServer(participantId, batteryGain);
            }

            int healthGain = ItemRules.GetHealthGain(type);
            float energyGain = ItemRules.GetEnergyGain(type);
            if (healthGain > 0 || energyGain > 0f)
            {
                vitality.RestoreOnServer(participantId, healthGain, energyGain);
            }

            _roster.TryRemove(participantId, type);
            PublishRecord(participantId);
            return ItemUseRequestResult.Used;
        }

        /// <summary>Server-only: adds one carried item (for example a picked-up map item).</summary>
        public bool AddItemOnServer(ParticipantId participantId, ItemType type)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server inventory can add items.");
            }

            if (!_roster.Add(participantId, type))
            {
                return false;
            }

            PublishRecord(participantId);
            return true;
        }

        /// <summary>
        /// Server-only: removes one carried item without applying its effect (for example a
        /// first-aid kit spent on reviving a teammate).
        /// </summary>
        public bool RemoveItemOnServer(ParticipantId participantId, ItemType type)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server inventory can remove items.");
            }

            if (!_roster.TryRemove(participantId, type))
            {
                return false;
            }

            PublishRecord(participantId);
            return true;
        }

        /// <summary>Server-only: the authoritative carried count.</summary>
        public int GetCountOnServer(ParticipantId participantId, ItemType type)
        {
            return _roster != null ? _roster.GetCount(participantId, type) : 0;
        }

        private void HandleAvatarSpawned(NetworkParticipantAvatar avatar)
        {
            if (avatar == null || !avatar.IsSpawned || !avatar.ParticipantId.IsValid || _roster == null)
            {
                return;
            }

            if (_roster.Register(avatar.ParticipantId, avatarSpawner.GetSpawnSlot(avatar.ParticipantId)))
            {
                PublishRecord(avatar.ParticipantId);
            }
        }

        private void PublishRecord(ParticipantId participantId)
        {
            if (!_roster.TryGet(participantId, out ParticipantItems items))
            {
                return;
            }

            var record = new NetworkItemInventoryRecord(items);
            for (int index = 0; index < _records.Count; index++)
            {
                if (_records[index].ParticipantId == participantId.Value)
                {
                    if (!_records[index].Equals(record))
                    {
                        _records[index] = record;
                    }

                    return;
                }
            }

            _records.Add(record);
        }

        private void HandleRecordsChanged(NetworkListEvent<NetworkItemInventoryRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (participantRegistry == null || avatarSpawner == null || vitality == null || flashlights == null)
            {
                throw new InvalidOperationException(
                    "The participant inventory requires explicit participant registry, avatar spawner, vitality and flashlight references.");
            }
        }
    }
}
