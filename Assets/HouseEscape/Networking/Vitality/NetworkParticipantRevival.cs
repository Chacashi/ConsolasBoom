using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Interaction;
using HouseEscape.Gameplay.Items;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Items;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using Unity.Netcode;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Networking.Vitality
{
    /// <summary>
    /// Server adapter for the pure <see cref="RevivalTracker"/> (GDD 7). Clients send begin and
    /// release intentions; the server validates sender ownership, a conscious reviver that is not
    /// already holding something, a carried first-aid kit, and range and aim to an unconscious
    /// teammate. The reviver cannot walk while holding. Damage to the reviver cancels its hold. On
    /// completion the kit is consumed, the teammate returns with the revived health and the reviver
    /// earns the revival points. Runs before <see cref="NetworkParticipantVitality"/> each frame so
    /// a hold that finished before the deadline is resolved before the elimination check.
    /// Holds are replicated with their server start time.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantRevival : NetworkBehaviour, IRevivalReadSource
    {
        /// <summary>Placeholder: height of an unconscious teammate's aim point above its root.</summary>
        public const float AimHeightMetres = 0.9f;

        /// <summary>Placeholder tuning of the aim volume around that point, as for generators.</summary>
        public const float AimRadiusMetres = 0.6f;

        public const float ServerAimRadiusMetres = AimRadiusMetres + 0.25f;

        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantAvatarDirectory avatarDirectory;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantInventory inventory;
        [SerializeField] private NetworkParticipantScoreboard scoreboard;

        private readonly List<ParticipantVitality> _vitalities = new List<ParticipantVitality>(ParticipantRegistry.MaximumParticipants);
        private readonly List<RevivalAttempt> _due = new List<RevivalAttempt>(ParticipantRegistry.MaximumParticipants);
        private readonly List<ParticipantId> _blockedRevivers = new List<ParticipantId>(ParticipantRegistry.MaximumParticipants);

        private NetworkList<NetworkRevivalRecord> _records;
        private RevivalTracker _tracker;
        private NetworkManager _networkManager;
        private bool _serverSubscribed;

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        public event Action Changed;

        private void Awake()
        {
            _records = new NetworkList<NetworkRevivalRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public void Initialize(
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantAvatarDirectory explicitAvatarDirectory,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantInventory explicitInventory,
            NetworkParticipantScoreboard explicitScoreboard)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Participant revival must be configured before it is spawned.");
            }

            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            avatarDirectory = explicitAvatarDirectory;
            vitality = explicitVitality;
            inventory = explicitInventory;
            scoreboard = explicitScoreboard;
            ValidateDependencies();
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _records.OnListChanged += HandleRecordsChanged;
            if (IsServer)
            {
                _tracker = new RevivalTracker();
                _records.Clear();
                vitality.ParticipantDamagedOnServer += HandleParticipantDamaged;
                _serverSubscribed = true;
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (_serverSubscribed)
            {
                vitality.ParticipantDamagedOnServer -= HandleParticipantDamaged;
                _serverSubscribed = false;
            }

            _records.OnListChanged -= HandleRecordsChanged;
            _tracker = null;
            _blockedRevivers.Clear();
            _networkManager = null;
            Changed?.Invoke();
        }

        /// <summary>The replicated hold of <paramref name="reviverId"/>, if any.</summary>
        public bool TryGetRevivalByReviver(ParticipantId reviverId, out ParticipantId targetId, out double startedAt)
        {
            if (_records != null && reviverId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].ReviverId == reviverId.Value)
                    {
                        targetId = new ParticipantId(_records[index].TargetId);
                        startedAt = _records[index].StartedAt;
                        return true;
                    }
                }
            }

            targetId = ParticipantId.None;
            startedAt = 0d;
            return false;
        }

        /// <summary>The earliest-started replicated hold on <paramref name="targetId"/>, if any.</summary>
        public bool TryGetRevivalOfTarget(ParticipantId targetId, out ParticipantId reviverId, out double startedAt)
        {
            reviverId = ParticipantId.None;
            startedAt = double.PositiveInfinity;
            if (_records != null && targetId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].TargetId == targetId.Value && _records[index].StartedAt < startedAt)
                    {
                        reviverId = new ParticipantId(_records[index].ReviverId);
                        startedAt = _records[index].StartedAt;
                    }
                }
            }

            return reviverId.IsValid;
        }

        /// <summary>
        /// Chooses the best-aimed unconscious teammate within range of <paramref name="reviverId"/>,
        /// provided the replicated inventory shows a first-aid kit. Used locally to decide what a
        /// press targets; the server repeats every check.
        /// </summary>
        public bool TryFindTarget(
            ParticipantId reviverId,
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            out ParticipantId targetId,
            out float aimError)
        {
            targetId = ParticipantId.None;
            aimError = float.PositiveInfinity;
            if (!IsSpawned
                || !inventory.TryGetItems(reviverId, out ParticipantItems items)
                || items.FirstAidKits <= 0)
            {
                return false;
            }

            IReadOnlyList<NetworkParticipantAvatar> avatars = avatarDirectory.Avatars;
            for (int index = 0; index < avatars.Count; index++)
            {
                NetworkParticipantAvatar avatar = avatars[index];
                if (avatar == null
                    || !avatar.IsSpawned
                    || avatar.ParticipantId == reviverId
                    || !vitality.TryGetVitality(avatar.ParticipantId, out ParticipantVitality target)
                    || target.State != VitalityState.Unconscious
                    || !IsWithinRange(avatar, participantPosition))
                {
                    continue;
                }

                float error = GetAimError(avatar, viewOrigin, viewForward, AimRadiusMetres);
                if (error <= AimRadiusMetres && error < aimError)
                {
                    aimError = error;
                    targetId = avatar.ParticipantId;
                }
            }

            return targetId.IsValid;
        }

        public void RequestBeginRevival(ParticipantId reviverId, ParticipantId targetId)
        {
            if (IsSpawned && IsClient && reviverId.IsValid && targetId.IsValid)
            {
                BeginRevivalRpc(reviverId.Value, targetId.Value);
            }
        }

        public void RequestReleaseRevival(ParticipantId reviverId)
        {
            if (IsSpawned && IsClient && reviverId.IsValid)
            {
                ReleaseRevivalRpc(reviverId.Value);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BeginRevivalRpc(ulong reviverId, ulong targetId, RpcParams rpcParams = default)
        {
            TryBeginRevivalOnServer(ToParticipantId(reviverId), ToParticipantId(targetId), rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseRevivalRpc(ulong reviverId, RpcParams rpcParams = default)
        {
            TryReleaseRevivalOnServer(ToParticipantId(reviverId), rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and begins a revival hold on behalf of <paramref name="senderClientId"/>.</summary>
        public RevivalRequestResult TryBeginRevivalOnServer(ParticipantId reviverId, ParticipantId targetId, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _tracker == null)
            {
                return RevivalRequestResult.NotServer;
            }

            if (!reviverId.IsValid || !targetId.IsValid)
            {
                return RevivalRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(reviverId, senderClientId))
            {
                return RevivalRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!avatarSpawner.TryGetAvatar(reviverId, out NetworkParticipantAvatar reviverAvatar)
                || reviverAvatar == null
                || !reviverAvatar.IsSpawned
                || reviverAvatar.NetworkObject.OwnerClientId != senderClientId)
            {
                return RevivalRequestResult.AvatarUnavailable;
            }

            if (!vitality.CanActOnServer(reviverId))
            {
                return RevivalRequestResult.CannotAct;
            }

            if (reviverAvatar.IsMovementBlocked || _tracker.TryGet(reviverId, out _))
            {
                return RevivalRequestResult.Busy;
            }

            if (inventory.GetCountOnServer(reviverId, ItemType.FirstAidKit) <= 0)
            {
                return RevivalRequestResult.NoFirstAidKit;
            }

            if (!avatarSpawner.TryGetAvatar(targetId, out NetworkParticipantAvatar targetAvatar)
                || targetAvatar == null
                || !targetAvatar.IsSpawned
                || !vitality.TryGetVitality(targetId, out ParticipantVitality target)
                || !vitality.TryGetVitality(reviverId, out ParticipantVitality reviver))
            {
                return RevivalRequestResult.TargetUnavailable;
            }

            if (!IsWithinRange(targetAvatar, reviverAvatar.transform.position))
            {
                return RevivalRequestResult.OutOfRange;
            }

            Transform view = reviverAvatar.ViewAnchor;
            if (view == null || GetAimError(targetAvatar, view.position, view.forward, ServerAimRadiusMetres) > ServerAimRadiusMetres)
            {
                return RevivalRequestResult.NotAiming;
            }

            double now = SynchronizedTime;
            RevivalBeginResult result = _tracker.Begin(reviver, target, now);
            if (result == RevivalBeginResult.TargetNotUnconscious || result == RevivalBeginResult.WindowExpired)
            {
                return RevivalRequestResult.TargetUnavailable;
            }

            if (result != RevivalBeginResult.Started)
            {
                return RevivalRequestResult.Rejected;
            }

            reviverAvatar.SetMovementBlockedOnServer(true);
            _blockedRevivers.Add(reviverId);
            _records.Add(new NetworkRevivalRecord(reviverId.Value, targetId.Value, now));
            return RevivalRequestResult.Started;
        }

        /// <summary>Validates and cancels the sender's revival hold.</summary>
        public RevivalRequestResult TryReleaseRevivalOnServer(ParticipantId reviverId, ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _tracker == null)
            {
                return RevivalRequestResult.NotServer;
            }

            if (!reviverId.IsValid)
            {
                return RevivalRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(reviverId, senderClientId))
            {
                return RevivalRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!_tracker.Cancel(reviverId))
            {
                return RevivalRequestResult.NoRevival;
            }

            SyncServerState();
            return RevivalRequestResult.Released;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _tracker == null || _tracker.Attempts.Count == 0)
            {
                return;
            }

            vitality.GetVitalities(_vitalities);
            _tracker.Resolve(SynchronizedTime, _vitalities, _due);
            for (int index = 0; index < _due.Count; index++)
            {
                RevivalAttempt attempt = _due[index];
                // The kit is consumed only when the revival completes; without one it fails.
                if (inventory.GetCountOnServer(attempt.ReviverId, ItemType.FirstAidKit) > 0
                    && vitality.ReviveOnServer(attempt.TargetId, attempt.CompletesAt))
                {
                    inventory.RemoveItemOnServer(attempt.ReviverId, ItemType.FirstAidKit);
                    scoreboard.AddScoreOnServer(attempt.ReviverId, VitalityRules.RevivalScoreAward);
                }
            }

            SyncServerState();
        }

        private void HandleParticipantDamaged(ParticipantId participantId)
        {
            // GDD 7: the reviver taking damage (or being knocked out) cancels its revival.
            if (_tracker != null && _tracker.Cancel(participantId))
            {
                SyncServerState();
            }
        }

        /// <summary>Drops records and walk blocks of every hold the tracker no longer has.</summary>
        private void SyncServerState()
        {
            for (int index = _records.Count - 1; index >= 0; index--)
            {
                NetworkRevivalRecord record = _records[index];
                if (!_tracker.TryGet(ToParticipantId(record.ReviverId), out RevivalAttempt attempt)
                    || attempt.StartedAt != record.StartedAt)
                {
                    _records.RemoveAt(index);
                }
            }

            for (int index = _blockedRevivers.Count - 1; index >= 0; index--)
            {
                ParticipantId reviverId = _blockedRevivers[index];
                if (_tracker.TryGet(reviverId, out _))
                {
                    continue;
                }

                _blockedRevivers.RemoveAt(index);
                if (avatarSpawner.TryGetAvatar(reviverId, out NetworkParticipantAvatar avatar)
                    && avatar != null
                    && avatar.IsSpawned)
                {
                    avatar.SetMovementBlockedOnServer(false);
                }
            }
        }

        private static bool IsWithinRange(NetworkParticipantAvatar target, Vector3 participantPosition)
        {
            return InteractionRules.IsWithinRange(ToNumerics(participantPosition), ToNumerics(target.transform.position));
        }

        private static float GetAimError(NetworkParticipantAvatar target, Vector3 viewOrigin, Vector3 viewForward, float aimRadius)
        {
            Vector3 aimPoint = target.transform.position + Vector3.up * AimHeightMetres;
            return InteractionRules.GetAimError(ToNumerics(viewOrigin), ToNumerics(viewForward), ToNumerics(aimPoint), aimRadius);
        }

        private static NumericsVector3 ToNumerics(Vector3 value)
        {
            return new NumericsVector3(value.x, value.y, value.z);
        }

        private static ParticipantId ToParticipantId(ulong value)
        {
            return value == 0 ? ParticipantId.None : new ParticipantId(value);
        }

        private void HandleRecordsChanged(NetworkListEvent<NetworkRevivalRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (participantRegistry == null
                || avatarSpawner == null
                || avatarDirectory == null
                || vitality == null
                || inventory == null
                || scoreboard == null)
            {
                throw new InvalidOperationException(
                    "Participant revival requires explicit registry, avatar spawner, avatar directory, vitality, inventory and scoreboard references.");
            }
        }
    }
}
