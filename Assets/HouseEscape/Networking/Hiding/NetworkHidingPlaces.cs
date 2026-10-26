using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Hiding;
using HouseEscape.Gameplay.Interaction;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Monster;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Vitality;
using Unity.Netcode;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Networking.Hiding
{
    /// <summary>
    /// Server adapter for the pure <see cref="HidingTracker"/> (GDD 3.6). Clients send enter,
    /// release (cancels a held chest entry) and leave intentions; the server validates sender
    /// ownership, a conscious participant not busy with another hold, cooldown, occupancy, range
    /// and aim. While hidden the avatar cannot walk, is not rendered and, when protected, is not a
    /// valid monster target. Leaving keeps walking blocked for the second of vulnerability and
    /// awards the success points when the hiding place made the monster lose the participant.
    /// Hiding state is replicated per participant with server phase times.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkHidingPlaces : NetworkBehaviour, IHidingReadSource
    {
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkMonster monster;
        [SerializeField] private NetworkParticipantScoreboard scoreboard;
        [SerializeField] private HidingSpot[] spots = Array.Empty<HidingSpot>();

        private readonly List<HidingStatus> _becameHidden = new List<HidingStatus>(ParticipantRegistry.MaximumParticipants);
        private readonly List<HidingStatus> _forcedOut = new List<HidingStatus>(ParticipantRegistry.MaximumParticipants);
        private readonly List<ParticipantId> _recovered = new List<ParticipantId>(ParticipantRegistry.MaximumParticipants);
        private readonly List<ParticipantId> _removed = new List<ParticipantId>(ParticipantRegistry.MaximumParticipants);

        private NetworkList<NetworkHidingRecord> _records;
        private HidingTracker _tracker;
        private NetworkManager _networkManager;
        private Func<ParticipantId, bool> _isSeenByMonster;
        private Func<ParticipantId, bool> _isTargetedByMonster;

        public IReadOnlyList<HidingSpot> Spots => spots;

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        public event Action Changed;

        private void Awake()
        {
            _records = new NetworkList<NetworkHidingRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
            _isSeenByMonster = IsSeenByMonster;
            _isTargetedByMonster = IsTargetedByMonster;
        }

        public void Initialize(
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantVitality explicitVitality,
            NetworkMonster explicitMonster,
            NetworkParticipantScoreboard explicitScoreboard,
            HidingSpot[] explicitSpots)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Hiding places must be configured before they are spawned.");
            }

            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            vitality = explicitVitality;
            monster = explicitMonster;
            scoreboard = explicitScoreboard;
            spots = explicitSpots;
            ValidateDependencies();
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _records.OnListChanged += HandleRecordsChanged;
            if (IsServer)
            {
                _tracker = new HidingTracker();
                _records.Clear();
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _records.OnListChanged -= HandleRecordsChanged;
            _tracker = null;
            _networkManager = null;
            Changed?.Invoke();
        }

        public bool TryGetHiding(ParticipantId participantId, out HidingStatus status)
        {
            if (_records != null && participantId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].ParticipantId == participantId.Value)
                    {
                        status = _records[index].ToStatus();
                        return true;
                    }
                }
            }

            status = default;
            return false;
        }

        /// <summary>
        /// Chooses the best-aimed free hiding place within range. Used locally to decide what a
        /// press targets; the server repeats every check.
        /// </summary>
        public bool TryFindSpot(
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            out int spotIndex,
            out float aimError)
        {
            spotIndex = -1;
            aimError = float.PositiveInfinity;
            for (int index = 0; index < spots.Length; index++)
            {
                HidingSpot spot = spots[index];
                if (spot == null || IsOccupiedReplicated(index) || !IsWithinRange(spot, participantPosition))
                {
                    continue;
                }

                float error = GetAimError(spot, viewOrigin, viewForward, HidingRules.AimRadiusMetres);
                if (error <= HidingRules.AimRadiusMetres && error < aimError)
                {
                    aimError = error;
                    spotIndex = index;
                }
            }

            return spotIndex >= 0;
        }

        public void RequestBegin(ParticipantId participantId, int spotIndex)
        {
            if (IsSpawned && IsClient && participantId.IsValid && spotIndex >= 0 && spotIndex < byte.MaxValue)
            {
                BeginRpc(participantId.Value, (byte)spotIndex);
            }
        }

        public void RequestRelease(ParticipantId participantId)
        {
            if (IsSpawned && IsClient && participantId.IsValid)
            {
                ReleaseRpc(participantId.Value);
            }
        }

        public void RequestLeave(ParticipantId participantId)
        {
            if (IsSpawned && IsClient && participantId.IsValid)
            {
                LeaveRpc(participantId.Value);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BeginRpc(ulong participantId, byte spotIndex, RpcParams rpcParams = default)
        {
            TryBeginOnServer(ToParticipantId(participantId), spotIndex, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseRpc(ulong participantId, RpcParams rpcParams = default)
        {
            TryReleaseOnServer(ToParticipantId(participantId), rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void LeaveRpc(ulong participantId, RpcParams rpcParams = default)
        {
            TryLeaveOnServer(ToParticipantId(participantId), rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and begins hiding on behalf of <paramref name="senderClientId"/>.</summary>
        public HidingRequestResult TryBeginOnServer(ParticipantId participantId, int spotIndex, ulong senderClientId)
        {
            HidingRequestResult validation = ValidateActor(participantId, senderClientId, out NetworkParticipantAvatar avatar);
            if (validation != HidingRequestResult.Hidden)
            {
                return validation;
            }

            if (_tracker.TryGet(participantId, out HidingStatus current) && current.Phase != HidingPhase.None)
            {
                return HidingRequestResult.Busy;
            }

            if (avatar.IsMovementBlocked)
            {
                return HidingRequestResult.Busy;
            }

            if (spotIndex < 0 || spotIndex >= spots.Length || spots[spotIndex] == null)
            {
                return HidingRequestResult.InvalidSpot;
            }

            HidingSpot spot = spots[spotIndex];
            if (!IsWithinRange(spot, avatar.transform.position))
            {
                return HidingRequestResult.OutOfRange;
            }

            Transform view = avatar.ViewAnchor;
            if (view == null || GetAimError(spot, view.position, view.forward, HidingRules.ServerAimRadiusMetres) > HidingRules.ServerAimRadiusMetres)
            {
                return HidingRequestResult.NotAiming;
            }

            HidingBeginResult result = _tracker.Begin(
                participantId,
                spotIndex,
                spot.SpotType,
                SynchronizedTime,
                IsSeenByMonster(participantId),
                IsTargetedByMonster(participantId));
            switch (result)
            {
                case HidingBeginResult.Hidden:
                    _tracker.TryGet(participantId, out HidingStatus hidden);
                    ApplyHidden(avatar, hidden);
                    Publish();
                    return HidingRequestResult.Hidden;
                case HidingBeginResult.Entering:
                    avatar.SetMovementBlockedOnServer(true);
                    Publish();
                    return HidingRequestResult.Entering;
                case HidingBeginResult.OnCooldown:
                    return HidingRequestResult.OnCooldown;
                case HidingBeginResult.Occupied:
                    return HidingRequestResult.Occupied;
                default:
                    return HidingRequestResult.Busy;
            }
        }

        /// <summary>Releasing the button cancels a held entry that has not completed.</summary>
        public HidingRequestResult TryReleaseOnServer(ParticipantId participantId, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _tracker == null)
            {
                return HidingRequestResult.NotServer;
            }

            if (!participantId.IsValid || !participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return HidingRequestResult.SenderDoesNotOwnParticipant;
            }

            ResolveOnServer();
            if (!_tracker.CancelEntering(participantId))
            {
                return HidingRequestResult.NotHidden;
            }

            SetMovementBlocked(participantId, false);
            Publish();
            return HidingRequestResult.Cancelled;
        }

        /// <summary>Validates and leaves the hiding place, starting the second of vulnerability.</summary>
        public HidingRequestResult TryLeaveOnServer(ParticipantId participantId, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _tracker == null)
            {
                return HidingRequestResult.NotServer;
            }

            if (!participantId.IsValid || !participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return HidingRequestResult.SenderDoesNotOwnParticipant;
            }

            ResolveOnServer();
            if (!_tracker.Leave(participantId, SynchronizedTime, out HidingStatus left))
            {
                return HidingRequestResult.NotHidden;
            }

            ApplyLeft(left);
            Publish();
            return HidingRequestResult.Left;
        }

        private void Update()
        {
            if (IsSpawned && IsServer && _tracker != null && _tracker.Statuses.Count > 0)
            {
                ResolveOnServer();
            }
        }

        /// <summary>
        /// Ends every use by participants who can no longer act (knocked out, eliminated, escaped or
        /// the match ended), then advances held entries, expirations and vulnerabilities.
        /// </summary>
        private void ResolveOnServer()
        {
            _removed.Clear();
            IReadOnlyList<HidingStatus> statuses = _tracker.Statuses;
            for (int index = 0; index < statuses.Count; index++)
            {
                if (statuses[index].Phase != HidingPhase.None && !vitality.CanActOnServer(statuses[index].ParticipantId))
                {
                    _removed.Add(statuses[index].ParticipantId);
                }
            }

            for (int index = 0; index < _removed.Count; index++)
            {
                _tracker.Remove(_removed[index]);
                RestoreAvatar(_removed[index]);
            }

            _tracker.Advance(SynchronizedTime, _isSeenByMonster, _isTargetedByMonster, _becameHidden, _forcedOut, _recovered);
            for (int index = 0; index < _becameHidden.Count; index++)
            {
                if (TryGetAvatar(_becameHidden[index].ParticipantId, out NetworkParticipantAvatar avatar))
                {
                    ApplyHidden(avatar, _becameHidden[index]);
                }
            }

            for (int index = 0; index < _forcedOut.Count; index++)
            {
                ApplyLeft(_forcedOut[index]);
            }

            for (int index = 0; index < _recovered.Count; index++)
            {
                SetMovementBlocked(_recovered[index], false);
            }

            Publish();
        }

        private void ApplyHidden(NetworkParticipantAvatar avatar, HidingStatus hidden)
        {
            avatar.SetMovementBlockedOnServer(true);
            avatar.SetHiddenOnServer(true);
            avatar.SetMonsterTargetableOnServer(!hidden.IsProtected);
        }

        /// <summary>Visible and targetable again; walking stays blocked for the vulnerability.</summary>
        private void ApplyLeft(HidingStatus left)
        {
            if (TryGetAvatar(left.ParticipantId, out NetworkParticipantAvatar avatar))
            {
                avatar.SetHiddenOnServer(false);
                avatar.SetMonsterTargetableOnServer(true);
                avatar.SetMovementBlockedOnServer(true);
            }

            if (left.LostMonster && vitality.CanActOnServer(left.ParticipantId))
            {
                scoreboard.AddScoreOnServer(left.ParticipantId, HidingRules.SuccessScoreAward);
            }
        }

        private void RestoreAvatar(ParticipantId participantId)
        {
            if (TryGetAvatar(participantId, out NetworkParticipantAvatar avatar))
            {
                avatar.SetHiddenOnServer(false);
                avatar.SetMonsterTargetableOnServer(true);
                avatar.SetMovementBlockedOnServer(false);
            }
        }

        private void SetMovementBlocked(ParticipantId participantId, bool blocked)
        {
            if (TryGetAvatar(participantId, out NetworkParticipantAvatar avatar))
            {
                avatar.SetMovementBlockedOnServer(blocked);
            }
        }

        private bool IsSeenByMonster(ParticipantId participantId)
        {
            return TryGetAvatar(participantId, out NetworkParticipantAvatar avatar) && monster.IsDetectingOnServer(avatar);
        }

        private bool IsTargetedByMonster(ParticipantId participantId)
        {
            return monster.IsSpawned && monster.ServerTargetId == participantId;
        }

        private HidingRequestResult ValidateActor(ParticipantId participantId, ulong senderClientId, out NetworkParticipantAvatar avatar)
        {
            avatar = null;
            if (!IsSpawned || !IsServer || _tracker == null)
            {
                return HidingRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return HidingRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return HidingRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!TryGetAvatar(participantId, out avatar) || avatar.NetworkObject.OwnerClientId != senderClientId)
            {
                avatar = null;
                return HidingRequestResult.AvatarUnavailable;
            }

            return vitality.CanActOnServer(participantId) ? HidingRequestResult.Hidden : HidingRequestResult.CannotAct;
        }

        private bool TryGetAvatar(ParticipantId participantId, out NetworkParticipantAvatar avatar)
        {
            return avatarSpawner.TryGetAvatar(participantId, out avatar) && avatar != null && avatar.IsSpawned;
        }

        private void Publish()
        {
            IReadOnlyList<HidingStatus> statuses = _tracker.Statuses;
            for (int index = 0; index < statuses.Count; index++)
            {
                var record = new NetworkHidingRecord(statuses[index]);
                bool found = false;
                for (int recordIndex = 0; recordIndex < _records.Count; recordIndex++)
                {
                    if (_records[recordIndex].ParticipantId == record.ParticipantId)
                    {
                        if (!_records[recordIndex].Equals(record))
                        {
                            _records[recordIndex] = record;
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _records.Add(record);
                }
            }
        }

        private bool IsOccupiedReplicated(int spotIndex)
        {
            for (int index = 0; index < _records.Count; index++)
            {
                if (_records[index].SpotIndex == spotIndex && _records[index].ToStatus().OccupiesSpot)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithinRange(HidingSpot spot, Vector3 participantPosition)
        {
            return InteractionRules.IsWithinRange(ToNumerics(participantPosition), ToNumerics(spot.InteractionPosition));
        }

        private static float GetAimError(HidingSpot spot, Vector3 viewOrigin, Vector3 viewForward, float aimRadius)
        {
            return InteractionRules.GetAimError(ToNumerics(viewOrigin), ToNumerics(viewForward), ToNumerics(spot.AimPosition), aimRadius);
        }

        private static NumericsVector3 ToNumerics(Vector3 value)
        {
            return new NumericsVector3(value.x, value.y, value.z);
        }

        private static ParticipantId ToParticipantId(ulong value)
        {
            return value == 0 ? ParticipantId.None : new ParticipantId(value);
        }

        private void HandleRecordsChanged(NetworkListEvent<NetworkHidingRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (participantRegistry == null
                || avatarSpawner == null
                || vitality == null
                || monster == null
                || scoreboard == null
                || spots == null)
            {
                throw new InvalidOperationException(
                    "Hiding places require explicit registry, avatar spawner, vitality, monster, scoreboard and hiding place references.");
            }

            for (int index = 0; index < spots.Length; index++)
            {
                if (spots[index] == null)
                {
                    throw new InvalidOperationException("Every hiding place reference must be assigned.");
                }
            }
        }
    }
}
