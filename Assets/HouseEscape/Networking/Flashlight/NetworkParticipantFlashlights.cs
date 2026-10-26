using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Flashlight;
using HouseEscape.Gameplay.Interaction;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Vitality;
using Unity.Netcode;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Networking.Flashlight
{
    /// <summary>
    /// Server adapter for the pure <see cref="FlashlightRoster"/> (GDD 3.2). Clients send toggle
    /// and charging-station hold intentions; the server validates sender ownership and that the
    /// participant is conscious in an active match (plus range and aim for stations). Lit
    /// flashlights drain on server time and turn off when empty or when the participant is no
    /// longer conscious; held stations recharge while the holder cannot walk. Battery, on/off and
    /// charging are replicated per participant, and the on/off state also drives each avatar's
    /// beam on every peer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantFlashlights : NetworkBehaviour, IFlashlightReadSource
    {
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private ChargingStation[] chargingStations = Array.Empty<ChargingStation>();

        private readonly Dictionary<ParticipantId, int> _chargingStationByParticipant =
            new Dictionary<ParticipantId, int>(ParticipantRegistry.MaximumParticipants);
        private readonly List<ParticipantId> _stopped = new List<ParticipantId>(ParticipantRegistry.MaximumParticipants);

        private NetworkList<NetworkFlashlightRecord> _records;
        private FlashlightRoster _roster;
        private NetworkManager _networkManager;
        private double _lastUpdateTime;
        private bool _serverSubscribed;

        public IReadOnlyList<ChargingStation> ChargingStations => chargingStations;

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        public event Action Changed;

        private void Awake()
        {
            _records = new NetworkList<NetworkFlashlightRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public void Initialize(
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantVitality explicitVitality,
            ChargingStation[] explicitChargingStations)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Participant flashlights must be configured before they are spawned.");
            }

            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            vitality = explicitVitality;
            chargingStations = explicitChargingStations;
            ValidateDependencies();
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _records.OnListChanged += HandleRecordsChanged;
            if (IsServer)
            {
                _roster = new FlashlightRoster();
                _chargingStationByParticipant.Clear();
                _records.Clear();
                _lastUpdateTime = SynchronizedTime;
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
            _chargingStationByParticipant.Clear();
            _networkManager = null;
            Changed?.Invoke();
        }

        public bool TryGetFlashlight(ParticipantId participantId, out ParticipantFlashlight flashlight)
        {
            if (TryGetRecord(participantId, out NetworkFlashlightRecord record))
            {
                flashlight = record.ToFlashlight();
                return true;
            }

            flashlight = default;
            return false;
        }

        /// <summary>Replicated: whether the participant is holding a charging station.</summary>
        public bool IsCharging(ParticipantId participantId)
        {
            return TryGetRecord(participantId, out NetworkFlashlightRecord record) && record.IsCharging;
        }

        /// <summary>
        /// Chooses the best-aimed charging station within range. Used locally to decide what a
        /// press targets; the server repeats range and aim checks.
        /// </summary>
        public bool TryFindStation(
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            out int stationIndex,
            out float aimError)
        {
            stationIndex = -1;
            aimError = float.PositiveInfinity;
            for (int index = 0; index < chargingStations.Length; index++)
            {
                ChargingStation station = chargingStations[index];
                if (station == null || !IsWithinRange(station, participantPosition))
                {
                    continue;
                }

                float error = GetAimError(station, viewOrigin, viewForward, FlashlightRules.StationAimRadiusMetres);
                if (error <= FlashlightRules.StationAimRadiusMetres && error < aimError)
                {
                    aimError = error;
                    stationIndex = index;
                }
            }

            return stationIndex >= 0;
        }

        public void RequestToggle(ParticipantId participantId)
        {
            if (IsSpawned && IsClient && participantId.IsValid)
            {
                ToggleRpc(participantId.Value);
            }
        }

        public void RequestBeginCharging(ParticipantId participantId, int stationIndex)
        {
            if (IsSpawned && IsClient && participantId.IsValid && stationIndex >= 0 && stationIndex < byte.MaxValue)
            {
                BeginChargingRpc(participantId.Value, (byte)stationIndex);
            }
        }

        public void RequestReleaseCharging(ParticipantId participantId)
        {
            if (IsSpawned && IsClient && participantId.IsValid)
            {
                ReleaseChargingRpc(participantId.Value);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleRpc(ulong participantId, RpcParams rpcParams = default)
        {
            TryToggleOnServer(ToParticipantId(participantId), rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BeginChargingRpc(ulong participantId, byte stationIndex, RpcParams rpcParams = default)
        {
            TryBeginChargingOnServer(ToParticipantId(participantId), stationIndex, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseChargingRpc(ulong participantId, RpcParams rpcParams = default)
        {
            TryReleaseChargingOnServer(ToParticipantId(participantId), rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and toggles the flashlight on behalf of <paramref name="senderClientId"/>.</summary>
        public FlashlightRequestResult TryToggleOnServer(ParticipantId participantId, ulong senderClientId)
        {
            FlashlightRequestResult validation = ValidateActor(participantId, senderClientId, out _);
            if (validation != FlashlightRequestResult.Changed)
            {
                return validation;
            }

            if (!_roster.TryGet(participantId, out ParticipantFlashlight flashlight))
            {
                return FlashlightRequestResult.InvalidParticipant;
            }

            if (!flashlight.IsOn && flashlight.Battery <= 0f)
            {
                return FlashlightRequestResult.EmptyBattery;
            }

            if (!flashlight.IsOn && IsHidden(participantId))
            {
                // GDD 3.6: a hidden player cannot use the flashlight.
                return FlashlightRequestResult.Busy;
            }

            _roster.SetOn(participantId, !flashlight.IsOn);
            Publish(participantId);
            return FlashlightRequestResult.Changed;
        }

        /// <summary>Validates and begins a charging-station hold on behalf of <paramref name="senderClientId"/>.</summary>
        public FlashlightRequestResult TryBeginChargingOnServer(ParticipantId participantId, int stationIndex, ulong senderClientId)
        {
            FlashlightRequestResult validation = ValidateActor(participantId, senderClientId, out NetworkParticipantAvatar avatar);
            if (validation != FlashlightRequestResult.Changed)
            {
                return validation;
            }

            if (avatar.IsMovementBlocked || _chargingStationByParticipant.ContainsKey(participantId))
            {
                return FlashlightRequestResult.Busy;
            }

            if (stationIndex < 0 || stationIndex >= chargingStations.Length || chargingStations[stationIndex] == null)
            {
                return FlashlightRequestResult.InvalidStation;
            }

            ChargingStation station = chargingStations[stationIndex];
            if (!IsWithinRange(station, avatar.transform.position))
            {
                return FlashlightRequestResult.OutOfRange;
            }

            Transform view = avatar.ViewAnchor;
            if (view == null
                || GetAimError(station, view.position, view.forward, FlashlightRules.StationServerAimRadiusMetres)
                    > FlashlightRules.StationServerAimRadiusMetres)
            {
                return FlashlightRequestResult.NotAiming;
            }

            _chargingStationByParticipant[participantId] = stationIndex;
            avatar.SetMovementBlockedOnServer(true);
            Publish(participantId);
            return FlashlightRequestResult.Changed;
        }

        /// <summary>Validates and ends the sender's charging hold; the charge gained is kept.</summary>
        public FlashlightRequestResult TryReleaseChargingOnServer(ParticipantId participantId, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                return FlashlightRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return FlashlightRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return FlashlightRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!_chargingStationByParticipant.ContainsKey(participantId))
            {
                return FlashlightRequestResult.NotCharging;
            }

            StopCharging(participantId);
            return FlashlightRequestResult.Released;
        }

        /// <summary>Server-only: adds battery from a battery item. False when unknown.</summary>
        public bool AddBatteryOnServer(ParticipantId participantId, float amount)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server flashlights can add battery.");
            }

            if (!_roster.TryGet(participantId, out _))
            {
                return false;
            }

            _roster.AddBattery(participantId, amount);
            Publish(participantId);
            return true;
        }

        /// <summary>Test seam: authoritative battery override on the server.</summary>
        internal void SetBatteryOnServer(ParticipantId participantId, float battery)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server flashlights can set battery.");
            }

            if (_roster.SetBattery(participantId, battery))
            {
                Publish(participantId);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                return;
            }

            double now = SynchronizedTime;
            double elapsed = now - _lastUpdateTime;
            _lastUpdateTime = now;
            IReadOnlyList<ParticipantFlashlight> participants = _roster.Participants;
            for (int index = 0; index < participants.Count; index++)
            {
                ParticipantId participantId = participants[index].ParticipantId;
                bool canAct = vitality.CanActOnServer(participantId);
                bool changed = false;
                if (participants[index].IsOn && (!canAct || IsHidden(participantId)))
                {
                    // Unconscious, eliminated, hidden or the match is over: the flashlight goes out.
                    changed |= _roster.SetOn(participantId, false);
                }

                if (elapsed > 0d && canAct)
                {
                    changed |= _roster.Drain(participantId, elapsed);
                    if (_chargingStationByParticipant.ContainsKey(participantId))
                    {
                        changed |= _roster.Recharge(participantId, elapsed);
                    }
                }

                if (changed)
                {
                    Publish(participantId);
                }
            }

            _stopped.Clear();
            foreach (ParticipantId participantId in _chargingStationByParticipant.Keys)
            {
                if (!vitality.CanActOnServer(participantId))
                {
                    _stopped.Add(participantId);
                }
            }

            for (int index = 0; index < _stopped.Count; index++)
            {
                StopCharging(_stopped[index]);
            }
        }

        private FlashlightRequestResult ValidateActor(ParticipantId participantId, ulong senderClientId, out NetworkParticipantAvatar avatar)
        {
            avatar = null;
            if (!IsSpawned || !IsServer || _roster == null)
            {
                return FlashlightRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return FlashlightRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return FlashlightRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!avatarSpawner.TryGetAvatar(participantId, out avatar)
                || avatar == null
                || !avatar.IsSpawned
                || avatar.NetworkObject.OwnerClientId != senderClientId)
            {
                avatar = null;
                return FlashlightRequestResult.AvatarUnavailable;
            }

            return vitality.CanActOnServer(participantId) ? FlashlightRequestResult.Changed : FlashlightRequestResult.CannotAct;
        }

        private void StopCharging(ParticipantId participantId)
        {
            if (!_chargingStationByParticipant.Remove(participantId))
            {
                return;
            }

            if (avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.SetMovementBlockedOnServer(false);
            }

            Publish(participantId);
        }

        private void HandleAvatarSpawned(NetworkParticipantAvatar avatar)
        {
            if (avatar == null || !avatar.IsSpawned || !avatar.ParticipantId.IsValid || _roster == null)
            {
                return;
            }

            _roster.Register(avatar.ParticipantId);
            Publish(avatar.ParticipantId);
        }

        /// <summary>Replicates the record and pushes on/off to the participant's avatar beam.</summary>
        private void Publish(ParticipantId participantId)
        {
            if (!_roster.TryGet(participantId, out ParticipantFlashlight flashlight))
            {
                return;
            }

            var record = new NetworkFlashlightRecord(flashlight, _chargingStationByParticipant.ContainsKey(participantId));
            bool found = false;
            for (int index = 0; index < _records.Count; index++)
            {
                if (_records[index].ParticipantId == participantId.Value)
                {
                    if (!_records[index].Equals(record))
                    {
                        _records[index] = record;
                    }

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                _records.Add(record);
            }

            if (avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned
                && avatar.IsFlashlightOn != flashlight.IsOn)
            {
                avatar.SetFlashlightOnServer(flashlight.IsOn);
            }
        }

        private bool IsHidden(ParticipantId participantId)
        {
            return avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned
                && avatar.IsHidden;
        }

        private bool TryGetRecord(ParticipantId participantId, out NetworkFlashlightRecord record)
        {
            if (_records != null && participantId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].ParticipantId == participantId.Value)
                    {
                        record = _records[index];
                        return true;
                    }
                }
            }

            record = default;
            return false;
        }

        private static bool IsWithinRange(ChargingStation station, Vector3 participantPosition)
        {
            return InteractionRules.IsWithinRange(ToNumerics(participantPosition), ToNumerics(station.InteractionPosition));
        }

        private static float GetAimError(ChargingStation station, Vector3 viewOrigin, Vector3 viewForward, float aimRadius)
        {
            return InteractionRules.GetAimError(
                ToNumerics(viewOrigin),
                ToNumerics(viewForward),
                ToNumerics(station.AimPosition),
                aimRadius);
        }

        private static NumericsVector3 ToNumerics(Vector3 value)
        {
            return new NumericsVector3(value.x, value.y, value.z);
        }

        private static ParticipantId ToParticipantId(ulong value)
        {
            return value == 0 ? ParticipantId.None : new ParticipantId(value);
        }

        private void HandleRecordsChanged(NetworkListEvent<NetworkFlashlightRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (participantRegistry == null || avatarSpawner == null || vitality == null || chargingStations == null)
            {
                throw new InvalidOperationException(
                    "Participant flashlights require explicit registry, avatar spawner, vitality and charging station references.");
            }

            for (int index = 0; index < chargingStations.Length; index++)
            {
                if (chargingStations[index] == null)
                {
                    throw new InvalidOperationException("Every charging station reference must be assigned.");
                }
            }
        }
    }
}
