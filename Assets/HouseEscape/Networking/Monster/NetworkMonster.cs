using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Monster;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Timeline;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HouseEscape.Networking.Monster
{
    /// <summary>
    /// Server adapter for the pure <see cref="MonsterBrain"/>. The server gathers perception
    /// (radius, environment line of sight, targetability), ticks the brain and drives the
    /// <see cref="NavMeshAgent"/>. Clients only receive the server-authoritative transform
    /// (NetworkTransform) and the replicated <see cref="State"/> for presentation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NetworkMonster : NetworkBehaviour
    {
        // Technical placeholders, not defined by the GDD.
        public const float EyeHeightMetres = 1.5f;
        public const float ParticipantSightHeightMetres = 1.2f;
        public const float WanderRadiusMetres = 10f;
        public const double WanderRepathIntervalSeconds = 6d;
        /// <summary>Added to the agent stopping distance to decide a wander point was reached.</summary>
        public const float WanderArrivalToleranceMetres = 0.25f;
        public const float StoppingDistanceMetres = 1f;
        private const float ChaseRepathThresholdMetres = 0.25f;
        private const double WanderRetrySeconds = 0.25d;
        private const int WanderSampleAttempts = 8;
        private const int LineOfSightHitBufferSize = 16;
        private const float NavMeshSampleDistanceMetres = 2f;

        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private LayerMask lineOfSightObstacles = Physics.DefaultRaycastLayers;

        private readonly NetworkVariable<byte> _state = new NetworkVariable<byte>(
            (byte)MonsterState.Sleeping,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly List<MonsterCandidate> _candidates = new List<MonsterCandidate>(4);
        private readonly System.Random _random = new System.Random();
        private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[LineOfSightHitBufferSize];

        private MonsterBrain _brain;
        private IMatchTimelineSource _matchState;
        private NetworkManager _networkManager;
        private NavMeshPath _path;
        private Vector3 _lastPursuitDestination;
        private bool _hasPursuitDestination;
        private double _nextWanderRepathAt = double.NegativeInfinity;
        private double _nextWanderAttemptAt = double.NegativeInfinity;
        private bool _warnedOffNavMesh;
        private bool _warnedNoWanderPoint;

        public MonsterState State => (MonsterState)_state.Value;

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        /// <summary>Server only: the current target, or None.</summary>
        public ParticipantId ServerTargetId => _brain != null ? _brain.TargetId : default;

        /// <summary>Server only: number of wander destinations chosen since spawn (diagnostics/tests).</summary>
        internal int ServerWanderDestinationCount { get; private set; }

        /// <summary>Raised on every peer when the replicated state changes.</summary>
        public event Action<MonsterState> StateChanged;

        /// <summary>Raised on the server for every attack; participant vitality applies its damage.</summary>
        public event Action<MonsterAttack> AttackPerformedOnServer;

        public void Initialize(
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NavMeshAgent explicitAgent)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The monster must be configured before it is spawned.");
            }

            matchTimeline = explicitMatchTimeline;
            avatarSpawner = explicitAvatarSpawner;
            agent = explicitAgent;
            ValidateDependencies();
        }

        /// <summary>Composition of the prefab itself, which cannot reference scene objects.</summary>
        public void InitializeAgent(NavMeshAgent explicitAgent)
        {
            agent = explicitAgent != null ? explicitAgent : throw new ArgumentNullException(nameof(explicitAgent));
        }

        /// <summary>
        /// Replaces the authoritative match/light source on the server. The composed scene uses
        /// its <see cref="NetworkMatchTimeline"/>; integration tests supply a controllable source.
        /// </summary>
        internal void UseMatchStateSourceOnServer(IMatchTimelineSource source)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server monster can change its match state source.");
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            UnsubscribeMatchState();
            _matchState = source;
            _matchState.SnapshotChanged += HandleMatchStateChanged;
            Simulate();
        }

        /// <summary>Test seam: teleports the server agent onto the NavMesh.</summary>
        internal void WarpOnServer(Vector3 position)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server monster can be warped.");
            }

            agent.Warp(position);
            _hasPursuitDestination = false;
            _nextWanderRepathAt = double.NegativeInfinity;
            _nextWanderAttemptAt = double.NegativeInfinity;
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _state.OnValueChanged += HandleStateChanged;

            if (!IsServer)
            {
                agent.enabled = false;
                return;
            }

            _brain = new MonsterBrain();
            _path = new NavMeshPath();
            _warnedOffNavMesh = false;
            _warnedNoWanderPoint = false;
            ServerWanderDestinationCount = 0;
            agent.enabled = true;
            agent.autoBraking = true;
            agent.stoppingDistance = StoppingDistanceMetres;
            if (!agent.isOnNavMesh
                && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshSampleDistanceMetres, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            WarnIfOffNavMesh();

            _matchState = matchTimeline;
            _matchState.SnapshotChanged += HandleMatchStateChanged;
            _state.Value = (byte)MonsterState.Sleeping;
            Simulate();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeMatchState();
            _state.OnValueChanged -= HandleStateChanged;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                HoldPosition();
            }

            if (agent != null)
            {
                agent.enabled = false;
            }

            _brain = null;
            _networkManager = null;
        }

        private void Update()
        {
            if (IsSpawned && IsServer)
            {
                Simulate();
            }
        }

        private void Simulate()
        {
            if (_brain == null || _matchState == null)
            {
                return;
            }

            double now = SynchronizedTime;
            MatchTimelineSnapshot snapshot = _matchState.Snapshot;
            bool awake = MatchTimelineAvailability.IsLitMatchActive(snapshot, now);

            Vector3 monsterPosition = transform.position;
            int consciousParticipants = awake ? GatherCandidates(monsterPosition) : 0;
            if (!awake)
            {
                _candidates.Clear();
            }

            MonsterDecision decision = _brain.Tick(new MonsterPerception(
                awake,
                now,
                ToPoint(monsterPosition),
                consciousParticipants,
                _candidates));

            ApplyMovement(decision, now);
            if (_state.Value != (byte)decision.State)
            {
                _state.Value = (byte)decision.State;
            }

            if (decision.HasAttack)
            {
                AttackPerformedOnServer?.Invoke(decision.Attack);
            }
        }

        /// <summary>
        /// Fills the candidate list and returns the number of conscious participants, which drives
        /// the four-player speed bonus. Eliminated participants have no avatar; unconscious ones
        /// are incapacitated and never targetable.
        /// </summary>
        private int GatherCandidates(Vector3 monsterPosition)
        {
            _candidates.Clear();
            int conscious = 0;
            Vector3 eye = monsterPosition + Vector3.up * EyeHeightMetres;
            IReadOnlyList<NetworkParticipantAvatar> avatars = avatarSpawner.SpawnedAvatars;
            for (int index = 0; index < avatars.Count; index++)
            {
                NetworkParticipantAvatar avatar = avatars[index];
                if (avatar == null || !avatar.IsSpawned || !avatar.ParticipantId.IsValid)
                {
                    continue;
                }

                if (!avatar.IsIncapacitated)
                {
                    conscious++;
                }

                Vector3 position = avatar.transform.position;
                WorldPoint point = ToPoint(position);
                bool targetable = avatar.IsMonsterTargetable;
                bool lineOfSight = targetable
                    && MonsterRules.IsWithinDetectionRadius(ToPoint(monsterPosition), point)
                    && HasLineOfSight(eye, position + Vector3.up * ParticipantSightHeightMetres);
                _candidates.Add(new MonsterCandidate(avatar.ParticipantId, point, targetable, lineOfSight));
            }

            return conscious;
        }

        /// <summary>
        /// Server-only: the monster is awake and currently detects <paramref name="avatar"/> (a
        /// targetable participant within the detection radius with line of sight).
        /// </summary>
        public bool IsDetectingOnServer(NetworkParticipantAvatar avatar)
        {
            if (!IsSpawned || !IsServer || avatar == null || !avatar.IsSpawned || State == MonsterState.Sleeping)
            {
                return false;
            }

            Vector3 monsterPosition = transform.position;
            Vector3 position = avatar.transform.position;
            return avatar.IsMonsterTargetable
                && MonsterRules.IsWithinDetectionRadius(ToPoint(monsterPosition), ToPoint(position))
                && HasLineOfSight(monsterPosition + Vector3.up * EyeHeightMetres, position + Vector3.up * ParticipantSightHeightMetres);
        }

        /// <summary>
        /// Only environment blocks sight: hits on the monster itself or on any participant avatar
        /// (including the target) are skipped, so a wall behind them still obstructs.
        /// </summary>
        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                from,
                offset / distance,
                _lineOfSightHits,
                distance,
                lineOfSightObstacles,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hitCount; index++)
            {
                Transform hit = _lineOfSightHits[index].transform;
                if (!hit.IsChildOf(transform) && hit.GetComponentInParent<NetworkParticipantAvatar>() == null)
                {
                    return false;
                }
            }

            // A full buffer may hide further hits; treat it as obstructed.
            return hitCount < _lineOfSightHits.Length;
        }

        private void WarnIfOffNavMesh()
        {
            if (!_warnedOffNavMesh && (!agent.enabled || !agent.isOnNavMesh))
            {
                _warnedOffNavMesh = true;
                Debug.LogWarning($"{nameof(NetworkMonster)} is not on a NavMesh at {transform.position}; it cannot move.", this);
            }
        }

        private void ApplyMovement(MonsterDecision decision, double now)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                WarnIfOffNavMesh();
                return;
            }

            switch (decision.Movement)
            {
                case MonsterMovement.Hold:
                    HoldPosition();
                    break;
                case MonsterMovement.Wander:
                    agent.isStopped = false;
                    agent.speed = decision.Speed;
                    _hasPursuitDestination = false;
                    bool arrived = !agent.pathPending
                        && (!agent.hasPath
                            || agent.remainingDistance <= agent.stoppingDistance + WanderArrivalToleranceMetres);
                    if ((arrived && now >= _nextWanderAttemptAt) || now >= _nextWanderRepathAt)
                    {
                        TrySetWanderDestination(now);
                    }

                    break;
                case MonsterMovement.Pursue:
                    agent.isStopped = false;
                    agent.speed = decision.Speed;
                    _nextWanderRepathAt = double.NegativeInfinity;
                    _nextWanderAttemptAt = double.NegativeInfinity;
                    Vector3 destination = ToVector(decision.Destination);
                    if (!_hasPursuitDestination
                        || (destination - _lastPursuitDestination).sqrMagnitude
                            > ChaseRepathThresholdMetres * ChaseRepathThresholdMetres)
                    {
                        if (agent.SetDestination(destination))
                        {
                            _lastPursuitDestination = destination;
                            _hasPursuitDestination = true;
                        }
                    }

                    break;
            }
        }

        private void TrySetWanderDestination(double now)
        {
            // On failure both timers retry shortly; on success the forced repath waits longer.
            _nextWanderAttemptAt = now + WanderRetrySeconds;
            _nextWanderRepathAt = now + WanderRetrySeconds;
            Vector3 origin = transform.position;
            for (int attempt = 0; attempt < WanderSampleAttempts; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2d;
                double distance = Math.Sqrt(_random.NextDouble()) * WanderRadiusMetres;
                Vector3 candidate = origin + new Vector3(
                    (float)(Math.Cos(angle) * distance),
                    0f,
                    (float)(Math.Sin(angle) * distance));
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleDistanceMetres, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, _path)
                    && _path.status == NavMeshPathStatus.PathComplete
                    && agent.SetPath(_path))
                {
                    _nextWanderRepathAt = now + WanderRepathIntervalSeconds;
                    ServerWanderDestinationCount++;
                    return;
                }
            }

            if (!_warnedNoWanderPoint)
            {
                _warnedNoWanderPoint = true;
                Debug.LogWarning($"{nameof(NetworkMonster)} found no reachable wander point near {origin}.", this);
            }
        }

        private void HoldPosition()
        {
            _hasPursuitDestination = false;
            _nextWanderRepathAt = double.NegativeInfinity;
            _nextWanderAttemptAt = double.NegativeInfinity;
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        private void HandleMatchStateChanged(MatchTimelineSnapshot snapshot)
        {
            if (IsSpawned && IsServer)
            {
                Simulate();
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

        private void HandleStateChanged(byte previous, byte current)
        {
            StateChanged?.Invoke((MonsterState)current);
        }

        private void ValidateDependencies()
        {
            if (matchTimeline == null || avatarSpawner == null || agent == null)
            {
                throw new InvalidOperationException(
                    "The monster requires explicit match timeline, avatar spawner and NavMeshAgent references.");
            }
        }

        private static WorldPoint ToPoint(Vector3 value) => new WorldPoint(value.x, value.y, value.z);

        private static Vector3 ToVector(WorldPoint value) => new Vector3(value.X, value.Y, value.Z);
    }
}
