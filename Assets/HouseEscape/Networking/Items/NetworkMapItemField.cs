using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Interaction;
using HouseEscape.Gameplay.Items;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using Unity.Netcode;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Networking.Items
{
    /// <summary>
    /// Map items of one match (GDD 3.4). When the match starts the server plans quantities and
    /// valid positions with <see cref="MapItemPlacementPlanner"/> and spawns one
    /// <see cref="NetworkMapItem"/> per item. Clients send pickup intentions; the server validates
    /// sender ownership, consciousness in an active match, range and aim, then adds the item to the
    /// inventory, awards the pickup points and despawns it, so each item is found exactly once.
    /// Every peer tracks spawned items here; an NGO prefab instance handler attaches replicas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMapItemField : NetworkBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantInventory inventory;
        [SerializeField] private NetworkParticipantScoreboard scoreboard;
        [SerializeField] private NetworkMapItem itemPrefab;
        [SerializeField] private Transform[] nearGeneratorPositions = Array.Empty<Transform>();
        [SerializeField] private Transform[] generalPositions = Array.Empty<Transform>();

        private readonly List<NetworkMapItem> _items = new List<NetworkMapItem>(32);
        private readonly List<MapItemPlacement> _placements = new List<MapItemPlacement>(32);

        private InstanceHandler _instanceHandler;
        private NetworkManager _handlerOwner;
        private bool _itemsSpawned;
        private bool _serverSubscribed;

        public IReadOnlyList<NetworkMapItem> Items => _items;

        public event Action Changed;

        public void Initialize(
            NetworkManager explicitNetworkManager,
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantInventory explicitInventory,
            NetworkParticipantScoreboard explicitScoreboard,
            NetworkMapItem explicitItemPrefab,
            Transform[] explicitNearGeneratorPositions,
            Transform[] explicitGeneralPositions)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The map item field must be configured before it is spawned.");
            }

            networkManager = explicitNetworkManager;
            matchTimeline = explicitMatchTimeline;
            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            vitality = explicitVitality;
            inventory = explicitInventory;
            scoreboard = explicitScoreboard;
            itemPrefab = explicitItemPrefab;
            nearGeneratorPositions = explicitNearGeneratorPositions;
            generalPositions = explicitGeneralPositions;
            ValidateDependencies();
        }

        private void Awake()
        {
            if (networkManager == null || itemPrefab == null)
            {
                Debug.LogError($"{nameof(NetworkMapItemField)} requires an explicit NetworkManager and map item prefab.", this);
                return;
            }

            _instanceHandler = new InstanceHandler(this);
            _handlerOwner = networkManager;
            _handlerOwner.PrefabHandler.AddHandler(itemPrefab.gameObject, _instanceHandler);
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            if (IsServer)
            {
                _itemsSpawned = false;
                matchTimeline.SnapshotChanged += HandleSnapshotChanged;
                _serverSubscribed = true;
                HandleSnapshotChanged(matchTimeline.Snapshot);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_serverSubscribed)
            {
                matchTimeline.SnapshotChanged -= HandleSnapshotChanged;
                _serverSubscribed = false;
            }
        }

        public override void OnDestroy()
        {
            if (_handlerOwner != null && itemPrefab != null)
            {
                _handlerOwner.PrefabHandler.RemoveHandler(itemPrefab.gameObject);
            }

            _handlerOwner = null;
            _instanceHandler = null;
            _items.Clear();
            base.OnDestroy();
        }

        /// <summary>
        /// Chooses the best-aimed map item within range. Used locally to decide what a press
        /// targets; <paramref name="aimError"/> lets the caller compare it with other targets.
        /// </summary>
        public bool TryFindTarget(
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            out NetworkMapItem target,
            out float aimError)
        {
            target = null;
            aimError = float.PositiveInfinity;
            for (int index = 0; index < _items.Count; index++)
            {
                NetworkMapItem item = _items[index];
                if (item == null || !item.IsSpawned || !IsWithinRange(item, participantPosition))
                {
                    continue;
                }

                float error = GetAimError(item, viewOrigin, viewForward, MapItemRules.AimRadiusMetres);
                if (error <= MapItemRules.AimRadiusMetres && error < aimError)
                {
                    aimError = error;
                    target = item;
                }
            }

            return target != null;
        }

        public void RequestPickup(ParticipantId participantId, NetworkMapItem item)
        {
            if (IsSpawned && IsClient && participantId.IsValid && item != null && item.IsSpawned)
            {
                PickupRpc(participantId.Value, item.NetworkObjectId);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PickupRpc(ulong requestedParticipantId, ulong itemNetworkObjectId, RpcParams rpcParams = default)
        {
            TryPickupOnServer(
                requestedParticipantId == 0 ? ParticipantId.None : new ParticipantId(requestedParticipantId),
                FindItem(itemNetworkObjectId),
                rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and completes a pickup on behalf of <paramref name="senderClientId"/>.</summary>
        public MapItemPickupRequestResult TryPickupOnServer(ParticipantId participantId, NetworkMapItem item, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer)
            {
                return MapItemPickupRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return MapItemPickupRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return MapItemPickupRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                || avatar == null
                || !avatar.IsSpawned
                || avatar.NetworkObject.OwnerClientId != senderClientId)
            {
                return MapItemPickupRequestResult.AvatarUnavailable;
            }

            if (!vitality.CanActOnServer(participantId))
            {
                return MapItemPickupRequestResult.CannotAct;
            }

            if (item == null || !item.IsSpawned || !_items.Contains(item))
            {
                return MapItemPickupRequestResult.ItemUnavailable;
            }

            if (!IsWithinRange(item, avatar.transform.position))
            {
                return MapItemPickupRequestResult.OutOfRange;
            }

            Transform view = avatar.ViewAnchor;
            if (view == null
                || GetAimError(item, view.position, view.forward, MapItemRules.ServerAimRadiusMetres) > MapItemRules.ServerAimRadiusMetres)
            {
                return MapItemPickupRequestResult.NotAiming;
            }

            ItemType type = item.ItemType;
            Unregister(item);
            item.NetworkObject.Despawn(true);
            inventory.AddItemOnServer(participantId, type);
            scoreboard.AddScoreOnServer(participantId, MapItemRules.PickupScoreAward);
            return MapItemPickupRequestResult.PickedUp;
        }

        internal void Register(NetworkMapItem item)
        {
            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
                Changed?.Invoke();
            }
        }

        internal void Unregister(NetworkMapItem item)
        {
            if (item != null && _items.Remove(item))
            {
                Changed?.Invoke();
            }
        }

        private void HandleSnapshotChanged(MatchTimelineSnapshot snapshot)
        {
            if (!_itemsSpawned && snapshot.IsStarted && !snapshot.IsComplete)
            {
                SpawnItemsOnServer();
            }
        }

        private void SpawnItemsOnServer()
        {
            _itemsSpawned = true;
            MapItemPlacementPlanner.Plan(
                new System.Random(),
                nearGeneratorPositions.Length,
                generalPositions.Length,
                _placements);
            for (int index = 0; index < _placements.Count; index++)
            {
                MapItemPlacement placement = _placements[index];
                Transform position = placement.PositionIndex < nearGeneratorPositions.Length
                    ? nearGeneratorPositions[placement.PositionIndex]
                    : generalPositions[placement.PositionIndex - nearGeneratorPositions.Length];
                SpawnItem(placement.Type, position.position, position.rotation);
            }
        }

        /// <summary>Test seam: spawns one extra map item at a known position on the server.</summary>
        internal NetworkMapItem SpawnItemOnServer(ItemType type, Vector3 position)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server field can spawn map items.");
            }

            return SpawnItem(type, position, Quaternion.identity);
        }

        private NetworkMapItem SpawnItem(ItemType type, Vector3 position, Quaternion rotation)
        {
            NetworkMapItem item = Instantiate(itemPrefab, position, rotation);
            item.ConfigureServer(type);
            item.AttachField(this);
            item.NetworkObject.Spawn(destroyWithScene: true);
            return item;
        }

        private NetworkMapItem FindItem(ulong networkObjectId)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index] != null && _items[index].NetworkObjectId == networkObjectId)
                {
                    return _items[index];
                }
            }

            return null;
        }

        private static bool IsWithinRange(NetworkMapItem item, Vector3 participantPosition)
        {
            return InteractionRules.IsWithinRange(ToNumerics(participantPosition), ToNumerics(item.InteractionPosition));
        }

        private static float GetAimError(NetworkMapItem item, Vector3 viewOrigin, Vector3 viewForward, float aimRadius)
        {
            return InteractionRules.GetAimError(
                ToNumerics(viewOrigin),
                ToNumerics(viewForward),
                ToNumerics(item.AimPosition),
                aimRadius);
        }

        private static NumericsVector3 ToNumerics(Vector3 value)
        {
            return new NumericsVector3(value.x, value.y, value.z);
        }

        private NetworkObject InstantiateReplica(Vector3 position, Quaternion rotation)
        {
            NetworkMapItem item = Instantiate(itemPrefab, position, rotation);
            item.AttachField(this);
            return item.NetworkObject;
        }

        private void ValidateDependencies()
        {
            if (networkManager == null
                || matchTimeline == null
                || participantRegistry == null
                || avatarSpawner == null
                || vitality == null
                || inventory == null
                || scoreboard == null
                || itemPrefab == null
                || nearGeneratorPositions == null
                || generalPositions == null)
            {
                throw new InvalidOperationException(
                    "The map item field requires explicit network, match, participant, inventory and scoreboard references and a map item prefab.");
            }

            if (nearGeneratorPositions.Length + generalPositions.Length < MapItemRules.MaximumTotalQuantity)
            {
                throw new InvalidOperationException(
                    $"The map item field requires at least {MapItemRules.MaximumTotalQuantity} valid item positions.");
            }

            for (int index = 0; index < nearGeneratorPositions.Length; index++)
            {
                if (nearGeneratorPositions[index] == null)
                {
                    throw new InvalidOperationException("Every near-generator item position must be assigned.");
                }
            }

            for (int index = 0; index < generalPositions.Length; index++)
            {
                if (generalPositions[index] == null)
                {
                    throw new InvalidOperationException("Every general item position must be assigned.");
                }
            }
        }

        /// <summary>Creates replicated map item instances with the field injected.</summary>
        private sealed class InstanceHandler : INetworkPrefabInstanceHandler
        {
            private readonly NetworkMapItemField _field;

            public InstanceHandler(NetworkMapItemField field)
            {
                _field = field;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                return _field.InstantiateReplica(position, rotation);
            }

            public void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null)
                {
                    UnityEngine.Object.Destroy(networkObject.gameObject);
                }
            }
        }
    }
}
