using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Generators;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Timeline;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Generators
{
    /// <summary>
    /// Server adapter for the pure <see cref="GeneratorRace"/>. Clients send begin/release
    /// intentions; the server validates sender ownership, rate, match and light state, range
    /// and aim, and completes holds on server time. Generator states are replicated by each
    /// <see cref="NetworkGenerator"/>; attempts are replicated here and activation points are
    /// awarded through the shared <see cref="NetworkParticipantScoreboard"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGeneratorRace : NetworkBehaviour, IGeneratorRaceReadSource
    {
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkGenerator[] generators = Array.Empty<NetworkGenerator>();
        [SerializeField] private NetworkParticipantScoreboard scoreboard;

        private readonly List<GeneratorAttempt> _serverAttempts = new List<GeneratorAttempt>(4);
        private readonly List<GeneratorCompletion> _completions = new List<GeneratorCompletion>(2);
        private readonly GeneratorBeginRateLimiter _beginRateLimiter = new GeneratorBeginRateLimiter();

        private NetworkList<NetworkGeneratorAttemptRecord> _attempts;
        private GeneratorRace _race;
        private IMatchTimelineSource _matchState;
        private NetworkManager _networkManager;
        private bool _subscribedToDisconnects;
        private double _availableUntil = double.NegativeInfinity;

        public IReadOnlyList<NetworkGenerator> Generators => generators;
        public int TotalGeneratorCount => GeneratorRace.TotalGeneratorCount;
        public int InProgressAttemptCount => _attempts?.Count ?? 0;

        public int ActiveGeneratorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < generators.Length; index++)
                {
                    if (generators[index] != null && generators[index].IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        public event Action Changed;

        private void Awake()
        {
            _attempts = new NetworkList<NetworkGeneratorAttemptRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public void Initialize(
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkGenerator[] explicitGenerators,
            NetworkParticipantScoreboard explicitScoreboard)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The generator race must be configured before it is spawned.");
            }

            participantRegistry = explicitParticipantRegistry;
            matchTimeline = explicitMatchTimeline;
            avatarSpawner = explicitAvatarSpawner;
            generators = explicitGenerators;
            scoreboard = explicitScoreboard;
            ValidateDependencies();
        }

        /// <summary>
        /// Replaces the authoritative match/light source on the server. The composed scene uses
        /// its <see cref="NetworkMatchTimeline"/>; integration tests supply a controllable source.
        /// </summary>
        internal void UseMatchStateSourceOnServer(IMatchTimelineSource source)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server race can change its match state source.");
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            UnsubscribeMatchState();
            _matchState = source;
            _matchState.SnapshotChanged += HandleMatchStateChanged;
            ResolveServerAttempts(SynchronizedTime);
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _attempts.OnListChanged += HandleAttemptsChanged;
            scoreboard.Changed += HandleScoresChanged;
            for (int index = 0; index < generators.Length; index++)
            {
                generators[index].ActiveChanged += HandleGeneratorActiveChanged;
            }

            if (IsServer)
            {
                _race = new GeneratorRace();
                _beginRateLimiter.Clear();
                _serverAttempts.Clear();
                _attempts.Clear();
                _matchState = matchTimeline;
                _matchState.SnapshotChanged += HandleMatchStateChanged;
                _availableUntil = GeneratorRaceAvailability.GetAvailableUntil(_matchState.Snapshot);
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                _subscribedToDisconnects = true;
            }

            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _race != null)
            {
                _race.ApplyMatchAndLightState(false, false);
                SyncServerAttempts();
            }

            UnsubscribeMatchState();
            if (_subscribedToDisconnects && _networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            _subscribedToDisconnects = false;
            _attempts.OnListChanged -= HandleAttemptsChanged;
            scoreboard.Changed -= HandleScoresChanged;
            for (int index = 0; index < generators.Length; index++)
            {
                if (generators[index] != null)
                {
                    generators[index].ActiveChanged -= HandleGeneratorActiveChanged;
                }
            }

            _race = null;
            _beginRateLimiter.Clear();
            _availableUntil = double.NegativeInfinity;
            _networkManager = null;
            Changed?.Invoke();
        }

        public bool TryGetGenerator(int generatorNumber, out NetworkGenerator generator)
        {
            for (int index = 0; index < generators.Length; index++)
            {
                if (generators[index] != null && generators[index].GeneratorNumber == generatorNumber)
                {
                    generator = generators[index];
                    return true;
                }
            }

            generator = null;
            return false;
        }

        /// <summary>
        /// Chooses the nearest inactive generator that the participant is within range of and
        /// aiming at. Used locally to decide which generator a press targets.
        /// </summary>
        public bool TryFindTarget(
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            out int generatorNumber)
        {
            generatorNumber = 0;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int index = 0; index < generators.Length; index++)
            {
                NetworkGenerator generator = generators[index];
                if (generator == null
                    || generator.IsActive
                    || !GeneratorInteractionQuery.CanBeginAttempt(generator, participantPosition, viewOrigin, viewForward))
                {
                    continue;
                }

                float distanceSquared = (generator.InteractionPosition - participantPosition).sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    generatorNumber = generator.GeneratorNumber;
                }
            }

            return generatorNumber != 0;
        }

        public bool TryGetAttempt(ParticipantId participantId, out int generatorNumber, out double startedAt)
        {
            if (_attempts != null && participantId.IsValid)
            {
                for (int index = 0; index < _attempts.Count; index++)
                {
                    NetworkGeneratorAttemptRecord record = _attempts[index];
                    if (record.ParticipantId == participantId.Value)
                    {
                        generatorNumber = record.GeneratorNumber;
                        startedAt = record.StartedAt;
                        return true;
                    }
                }
            }

            generatorNumber = 0;
            startedAt = 0d;
            return false;
        }

        public int GetScore(ParticipantId participantId)
        {
            return scoreboard != null ? scoreboard.GetScore(participantId) : 0;
        }

        public void RequestBeginAttempt(ParticipantId participantId, int generatorNumber)
        {
            if (CanSendIntention(participantId, generatorNumber))
            {
                BeginAttemptRpc(participantId.Value, (byte)generatorNumber);
            }
        }

        public void RequestReleaseAttempt(ParticipantId participantId, int generatorNumber)
        {
            if (CanSendIntention(participantId, generatorNumber))
            {
                ReleaseAttemptRpc(participantId.Value, (byte)generatorNumber);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BeginAttemptRpc(ulong requestedParticipantId, byte generatorNumber, RpcParams rpcParams = default)
        {
            TryBeginAttemptOnServer(
                ToParticipantId(requestedParticipantId),
                generatorNumber,
                rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseAttemptRpc(ulong requestedParticipantId, byte generatorNumber, RpcParams rpcParams = default)
        {
            TryReleaseAttemptOnServer(
                ToParticipantId(requestedParticipantId),
                generatorNumber,
                rpcParams.Receive.SenderClientId);
        }

        /// <summary>Validates and begins an attempt on behalf of <paramref name="senderClientId"/>.</summary>
        public GeneratorAttemptRequestResult TryBeginAttemptOnServer(
            ParticipantId participantId,
            int generatorNumber,
            ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _race == null)
            {
                return GeneratorAttemptRequestResult.NotServer;
            }

            GeneratorAttemptRequestResult ownership = ValidateOwnership(
                participantId,
                senderClientId,
                out NetworkParticipantAvatar avatar);
            if (ownership != GeneratorAttemptRequestResult.Started)
            {
                return ownership;
            }

            if (avatar.IsIncapacitated)
            {
                return GeneratorAttemptRequestResult.ParticipantIncapacitated;
            }

            double now = SynchronizedTime;
            if (!_beginRateLimiter.TryAccept(participantId, now))
            {
                return GeneratorAttemptRequestResult.BeginTooSoon;
            }

            // Already holding a generator, or another hold interaction (a revival) blocks walking.
            if (FindServerAttemptIndex(participantId) >= 0 || avatar.IsMovementBlocked)
            {
                return GeneratorAttemptRequestResult.AlreadyAttempting;
            }

            if (!TryGetGenerator(generatorNumber, out NetworkGenerator generator))
            {
                return GeneratorAttemptRequestResult.InvalidGenerator;
            }

            MatchTimelineSnapshot snapshot = _matchState.Snapshot;
            bool matchActive = GeneratorRaceAvailability.IsMatchActive(snapshot, now);
            bool lightsOn = GeneratorRaceAvailability.AreLightsOn(snapshot, now);
            if (!matchActive)
            {
                return GeneratorAttemptRequestResult.MatchNotActive;
            }

            if (!lightsOn)
            {
                return GeneratorAttemptRequestResult.LightsOff;
            }

            if (generator.IsActive)
            {
                return GeneratorAttemptRequestResult.GeneratorAlreadyActive;
            }

            if (!GeneratorInteractionQuery.IsWithinRange(generator, avatar.transform.position))
            {
                return GeneratorAttemptRequestResult.OutOfRange;
            }

            // Aim is checked against the server's own avatar view: authoritative yaw on the
            // avatar root and authoritative pitch (replicated as a NetworkVariable) on the view
            // anchor, which is the transform the participant camera is attached to. A small
            // technical tolerance absorbs replication skew between client and server views.
            Transform view = avatar.ViewAnchor;
            if (view == null
                || !GeneratorInteractionQuery.IsAimingAtWithServerTolerance(generator, view.position, view.forward))
            {
                return GeneratorAttemptRequestResult.NotAiming;
            }

            BeginGeneratorAttemptResult result = _race.BeginAttempt(
                generatorNumber,
                participantId,
                now,
                matchActive,
                lightsOn,
                out GeneratorAttempt attempt);
            switch (result)
            {
                case BeginGeneratorAttemptResult.Started:
                    break;
                case BeginGeneratorAttemptResult.GeneratorAlreadyActive:
                    return GeneratorAttemptRequestResult.GeneratorAlreadyActive;
                case BeginGeneratorAttemptResult.LightsOff:
                    return GeneratorAttemptRequestResult.LightsOff;
                case BeginGeneratorAttemptResult.MatchNotActive:
                    return GeneratorAttemptRequestResult.MatchNotActive;
                default:
                    return GeneratorAttemptRequestResult.Rejected;
            }

            _availableUntil = GeneratorRaceAvailability.GetAvailableUntil(snapshot);
            _serverAttempts.Add(attempt);
            _attempts.Add(new NetworkGeneratorAttemptRecord(
                attempt.Id.Value,
                participantId.Value,
                (byte)generatorNumber,
                attempt.StartedAt));
            avatar.SetMovementBlockedOnServer(true);
            return GeneratorAttemptRequestResult.Started;
        }

        /// <summary>Validates and cancels the sender's attempt, resetting its progress.</summary>
        public GeneratorAttemptRequestResult TryReleaseAttemptOnServer(
            ParticipantId participantId,
            int generatorNumber,
            ulong senderClientId)
        {
            if (!IsSpawned || !IsServer || _race == null)
            {
                return GeneratorAttemptRequestResult.NotServer;
            }

            if (!participantId.IsValid)
            {
                return GeneratorAttemptRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return GeneratorAttemptRequestResult.SenderDoesNotOwnParticipant;
            }

            int index = FindServerAttemptIndex(participantId);
            if (index < 0)
            {
                return GeneratorAttemptRequestResult.NoAttempt;
            }

            if (_serverAttempts[index].GeneratorNumber != generatorNumber)
            {
                return GeneratorAttemptRequestResult.GeneratorMismatch;
            }

            _race.CancelAttempt(_serverAttempts[index].Id, participantId);
            SyncServerAttempts();
            return GeneratorAttemptRequestResult.Released;
        }

        private void Update()
        {
            if (IsSpawned && IsServer && _race != null && _serverAttempts.Count > 0)
            {
                ResolveServerAttempts(SynchronizedTime);
            }
        }

        /// <summary>
        /// Order matters: attempts whose avatar vanished, or whose participant was knocked out
        /// before the hold was due, are canceled; then every hold that finished inside the last
        /// lights-on window completes (even if this frame is already past lights-off, match end
        /// or the knockout); only then are remaining attempts of knocked-out participants canceled,
        /// followed by any the match or lights no longer permit.
        /// </summary>
        private void ResolveServerAttempts(double now)
        {
            if (_race == null || _matchState == null)
            {
                return;
            }

            CancelUnavailableParticipantAttempts(false);

            _completions.Clear();
            _race.CompleteDueAttempts(now, _availableUntil, _completions);
            for (int index = 0; index < _completions.Count; index++)
            {
                GeneratorCompletion completion = _completions[index];
                if (TryGetGenerator(completion.Attempt.GeneratorNumber, out NetworkGenerator generator))
                {
                    generator.ActivateOnServer();
                }

                scoreboard.AddScoreOnServer(completion.Attempt.ParticipantId, completion.ScoreAward);
            }

            CancelUnavailableParticipantAttempts(true);

            MatchTimelineSnapshot snapshot = _matchState.Snapshot;
            bool matchActive = GeneratorRaceAvailability.IsMatchActive(snapshot, now);
            bool lightsOn = GeneratorRaceAvailability.AreLightsOn(snapshot, now);
            _race.ApplyMatchAndLightState(matchActive, lightsOn);
            _availableUntil = matchActive && lightsOn
                ? GeneratorRaceAvailability.GetAvailableUntil(snapshot)
                : double.NegativeInfinity;
            SyncServerAttempts();
        }

        /// <summary>
        /// Cancels attempts whose avatar vanished or whose participant is unconscious. Before
        /// completions run (<paramref name="afterCompletions"/> false), a hold already due at the
        /// knockout's server time is kept so it can still complete.
        /// </summary>
        private void CancelUnavailableParticipantAttempts(bool afterCompletions)
        {
            for (int index = 0; index < _serverAttempts.Count; index++)
            {
                GeneratorAttempt attempt = _serverAttempts[index];
                if (!_race.IsAttemptInProgress(attempt.Id))
                {
                    continue;
                }

                bool cancel;
                if (!avatarSpawner.TryGetAvatar(attempt.ParticipantId, out NetworkParticipantAvatar avatar)
                    || avatar == null
                    || !avatar.IsSpawned)
                {
                    cancel = true;
                }
                else
                {
                    cancel = avatar.IsIncapacitated
                        && (afterCompletions || attempt.CompletesAt > avatar.IncapacitatedAt);
                }

                if (cancel)
                {
                    _race.CancelAttempt(attempt.Id, attempt.ParticipantId);
                }
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer || _race == null)
            {
                return;
            }

            for (int index = 0; index < _serverAttempts.Count; index++)
            {
                GeneratorAttempt attempt = _serverAttempts[index];
                if (participantRegistry.IsParticipantOwnedBy(attempt.ParticipantId, clientId))
                {
                    _race.CancelAttempt(attempt.Id, attempt.ParticipantId);
                }
            }

            SyncServerAttempts();
        }

        private GeneratorAttemptRequestResult ValidateOwnership(
            ParticipantId participantId,
            ulong senderClientId,
            out NetworkParticipantAvatar avatar)
        {
            avatar = null;
            if (!participantId.IsValid)
            {
                return GeneratorAttemptRequestResult.InvalidParticipant;
            }

            if (!participantRegistry.IsParticipantOwnedBy(participantId, senderClientId))
            {
                return GeneratorAttemptRequestResult.SenderDoesNotOwnParticipant;
            }

            if (!avatarSpawner.TryGetAvatar(participantId, out avatar)
                || avatar == null
                || !avatar.IsSpawned
                || avatar.NetworkObject.OwnerClientId != senderClientId)
            {
                avatar = null;
                return GeneratorAttemptRequestResult.AvatarUnavailable;
            }

            return GeneratorAttemptRequestResult.Started;
        }

        /// <summary>Drops adapter/replicated state for every attempt the pure race has ended.</summary>
        private void SyncServerAttempts()
        {
            for (int index = _serverAttempts.Count - 1; index >= 0; index--)
            {
                if (_race == null || !_race.IsAttemptInProgress(_serverAttempts[index].Id))
                {
                    RemoveServerAttemptAt(index);
                }
            }
        }

        private void RemoveServerAttemptAt(int index)
        {
            GeneratorAttempt attempt = _serverAttempts[index];
            _serverAttempts.RemoveAt(index);
            for (int recordIndex = _attempts.Count - 1; recordIndex >= 0; recordIndex--)
            {
                if (_attempts[recordIndex].AttemptId == attempt.Id.Value)
                {
                    _attempts.RemoveAt(recordIndex);
                }
            }

            if (avatarSpawner != null
                && avatarSpawner.TryGetAvatar(attempt.ParticipantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.SetMovementBlockedOnServer(false);
            }
        }

        private int FindServerAttemptIndex(ParticipantId participantId)
        {
            for (int index = 0; index < _serverAttempts.Count; index++)
            {
                if (_serverAttempts[index].ParticipantId == participantId)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool CanSendIntention(ParticipantId participantId, int generatorNumber)
        {
            return IsSpawned
                && IsClient
                && participantId.IsValid
                && generatorNumber >= 1
                && generatorNumber <= GeneratorRace.TotalGeneratorCount;
        }

        private void HandleMatchStateChanged(MatchTimelineSnapshot snapshot)
        {
            if (IsSpawned && IsServer)
            {
                ResolveServerAttempts(SynchronizedTime);
            }
        }

        private void UnsubscribeMatchState()
        {
            if (_matchState != null)
            {
                _matchState.SnapshotChanged -= HandleMatchStateChanged;
                _matchState = null;
            }
        }

        private void HandleAttemptsChanged(NetworkListEvent<NetworkGeneratorAttemptRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void HandleScoresChanged()
        {
            Changed?.Invoke();
        }

        private void HandleGeneratorActiveChanged(NetworkGenerator generator)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (participantRegistry == null
                || matchTimeline == null
                || avatarSpawner == null
                || scoreboard == null
                || generators == null
                || generators.Length != GeneratorRace.TotalGeneratorCount)
            {
                throw new InvalidOperationException(
                    "The generator race requires explicit registry, timeline, avatar spawner, scoreboard and six generators.");
            }

            var seen = new bool[GeneratorRace.TotalGeneratorCount];
            for (int index = 0; index < generators.Length; index++)
            {
                NetworkGenerator generator = generators[index];
                if (generator == null)
                {
                    throw new InvalidOperationException("Every generator reference must be assigned.");
                }

                int number = generator.GeneratorNumber;
                if (number < 1 || number > GeneratorRace.TotalGeneratorCount || seen[number - 1])
                {
                    throw new InvalidOperationException("Generators must use unique numbers 1 through 6.");
                }

                seen[number - 1] = true;
            }
        }

        private static ParticipantId ToParticipantId(ulong value)
        {
            return value == 0 ? ParticipantId.None : new ParticipantId(value);
        }
    }
}
