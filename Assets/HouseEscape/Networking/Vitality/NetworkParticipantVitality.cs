using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Monster;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Monster;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Vitality
{
    /// <summary>
    /// Server adapter for the pure <see cref="ParticipantVitalityRoster"/>. Revival holds are
    /// resolved by <see cref="NetworkParticipantRevival"/>, which runs earlier each frame so a
    /// revival that finished before the deadline wins over elimination. Registers every
    /// spawned participant avatar, applies monster attacks, incapacitates unconscious avatars,
    /// despawns permanently eliminated ones (after the 30 s window, or immediately when their
    /// console's connection is lost) and ends the match in defeat once every participant is
    /// eliminated. Health, state and the server-time recovery deadline are replicated per
    /// participant in a <see cref="NetworkList{T}"/> that outlives the avatars.
    /// Energy (GDD 3.3) is drained here every server frame from the exact roster value and
    /// pushed to the participant's avatar for its speed; the record carries only the whole
    /// percentage, so it is rewritten only when that integer changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantVitality : NetworkBehaviour, IParticipantVitalityReadSource
    {
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkMonster monster;

        private readonly List<ParticipantId> _eliminated = new List<ParticipantId>(ParticipantRegistry.MaximumParticipants);

        private NetworkList<NetworkParticipantVitalityRecord> _records;
        private ParticipantVitalityRoster _roster;
        private IMatchTimelineSource _matchState;
        private NetworkManager _networkManager;
        private double _lastEnergyUpdateTime;
        private bool _serverSubscribed;

        public double SynchronizedTime => _networkManager != null ? _networkManager.ServerTime.Time : 0d;

        public MatchOutcome Outcome => matchTimeline != null ? matchTimeline.Snapshot.Outcome : MatchOutcome.None;

        public int Count => _records?.Count ?? 0;

        /// <summary>Raised on every peer when replicated vitality or the match outcome changes.</summary>
        public event Action Changed;

        /// <summary>Server-only: a participant took damage (including the hit that knocked it out).</summary>
        public event Action<ParticipantId> ParticipantDamagedOnServer;

        private void Awake()
        {
            _records = new NetworkList<NetworkParticipantVitalityRecord>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        }

        public void Initialize(
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkMonster explicitMonster)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("Participant vitality must be configured before it is spawned.");
            }

            matchTimeline = explicitMatchTimeline;
            participantRegistry = explicitParticipantRegistry;
            avatarSpawner = explicitAvatarSpawner;
            monster = explicitMonster;
            ValidateDependencies();
        }

        /// <summary>
        /// Test seam: replaces the source that decides whether the match is active on the server.
        /// Defeat is still recorded on the authoritative <see cref="NetworkMatchTimeline"/>.
        /// </summary>
        internal void UseMatchStateSourceOnServer(IMatchTimelineSource source)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server vitality can change its match state source.");
            }

            _matchState = source ?? throw new ArgumentNullException(nameof(source));
        }

        public override void OnNetworkSpawn()
        {
            ValidateDependencies();
            _networkManager = NetworkObject.NetworkManager;
            _records.OnListChanged += HandleRecordsChanged;
            matchTimeline.SnapshotChanged += HandleSnapshotChanged;

            if (IsServer)
            {
                _roster = new ParticipantVitalityRoster();
                _matchState = matchTimeline;
                _lastEnergyUpdateTime = SynchronizedTime;
                _records.Clear();
                avatarSpawner.AvatarSpawned += HandleAvatarSpawned;
                monster.AttackPerformedOnServer += HandleMonsterAttack;
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                _serverSubscribed = true;
                IReadOnlyList<NetworkParticipantAvatar> avatars = avatarSpawner.SpawnedAvatars;
                for (int index = avatars.Count - 1; index >= 0; index--)
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
                monster.AttackPerformedOnServer -= HandleMonsterAttack;
                if (_networkManager != null)
                {
                    _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                }

                _serverSubscribed = false;
            }

            matchTimeline.SnapshotChanged -= HandleSnapshotChanged;
            _records.OnListChanged -= HandleRecordsChanged;
            _roster = null;
            _matchState = null;
            _networkManager = null;
            Changed?.Invoke();
        }

        public bool TryGetVitality(ParticipantId participantId, out ParticipantVitality vitality)
        {
            if (_records != null && participantId.IsValid)
            {
                for (int index = 0; index < _records.Count; index++)
                {
                    if (_records[index].ParticipantId == participantId.Value)
                    {
                        vitality = _records[index].ToVitality();
                        return true;
                    }
                }
            }

            vitality = default;
            return false;
        }

        public void GetVitalities(List<ParticipantVitality> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (_records == null)
            {
                return;
            }

            for (int index = 0; index < _records.Count; index++)
            {
                results.Add(_records[index].ToVitality());
            }
        }

        /// <summary>
        /// Server-only: applies damage from any authoritative source (currently monster attacks)
        /// while the match is active.
        /// </summary>
        public VitalityDamageResult ApplyDamageOnServer(ParticipantId participantId, int damage)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can apply damage.");
            }

            double now = SynchronizedTime;
            if (!IsMatchActive(now))
            {
                return VitalityDamageResult.Ignored;
            }

            VitalityDamageResult result = _roster.ApplyDamage(participantId, damage, now);
            if (result != VitalityDamageResult.Damaged && result != VitalityDamageResult.KnockedUnconscious)
            {
                return result;
            }

            PublishRecord(participantId);
            if (result == VitalityDamageResult.KnockedUnconscious
                && avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.SetIncapacitatedOnServer(true);
            }

            ParticipantDamagedOnServer?.Invoke(participantId);
            return result;
        }

        /// <summary>
        /// Server-only: returns an unconscious participant to consciousness with the revived
        /// health, provided <paramref name="revivedAt"/> is not past its recovery deadline and the
        /// match is active. Returns false, changing nothing, otherwise.
        /// </summary>
        public bool ReviveOnServer(ParticipantId participantId, double revivedAt)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can revive participants.");
            }

            if (!IsMatchActive(SynchronizedTime) || !_roster.Revive(participantId, revivedAt))
            {
                return false;
            }

            PublishRecord(participantId);
            if (avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.SetIncapacitatedOnServer(false);
            }

            return true;
        }

        /// <summary>
        /// Server-only: adds energy to a conscious participant during an active match (for
        /// example from food), capped at 100. Returns false when nothing changed.
        /// </summary>
        public bool AddEnergyOnServer(ParticipantId participantId, float amount)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can add energy.");
            }

            if (!IsMatchActive(SynchronizedTime) || !_roster.RestoreEnergy(participantId, amount))
            {
                return false;
            }

            PublishEnergy(participantId);
            return true;
        }

        /// <summary>
        /// Server-only: whether <paramref name="participantId"/> is conscious in an active match,
        /// the precondition for acting (for example consuming an item).
        /// </summary>
        public bool CanActOnServer(ParticipantId participantId)
        {
            return IsSpawned
                && IsServer
                && _roster != null
                && IsMatchActive(SynchronizedTime)
                && _roster.TryGet(participantId, out ParticipantVitality vitality)
                && vitality.IsConscious;
        }

        /// <summary>
        /// Server-only: applies an item's health and energy gains (each capped at 100) to a
        /// conscious participant in an active match. Returns false, changing nothing, otherwise.
        /// </summary>
        public bool RestoreOnServer(ParticipantId participantId, int health, float energy)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can restore participants.");
            }

            if (!CanActOnServer(participantId))
            {
                return false;
            }

            if (health > 0)
            {
                _roster.RestoreHealth(participantId, health);
            }

            if (energy > 0f)
            {
                _roster.RestoreEnergy(participantId, energy);
            }

            PublishEnergy(participantId);
            return true;
        }

        /// <summary>
        /// Server-only: a conscious participant leaves the house through the front door. Its avatar
        /// is despawned and it spectates. Returns false, changing nothing, unless conscious in an
        /// active match.
        /// </summary>
        public bool EscapeOnServer(ParticipantId participantId)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can record escapes.");
            }

            if (!CanActOnServer(participantId) || !_roster.Escape(participantId))
            {
                return false;
            }

            PublishRecord(participantId);
            if (avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.NetworkObject.Despawn(true);
            }

            return true;
        }

        /// <summary>Server-only: every conscious participant escaped and at least one did.</summary>
        public bool IsEscapeCompleteOnServer => _roster != null && _roster.IsEscapeComplete;

        /// <summary>Test seam: authoritative energy override on the server.</summary>
        internal void SetEnergyOnServer(ParticipantId participantId, float energy)
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                throw new InvalidOperationException("Only the spawned server vitality can set energy.");
            }

            if (_roster.SetEnergy(participantId, energy))
            {
                PublishEnergy(participantId);
            }
        }

        /// <summary>
        /// Server-only (GDD 7): a lost console connection permanently eliminates every participant
        /// that connection owns, immediately. Mid-match reconnection is not available.
        /// </summary>
        internal void HandleClientDisconnected(ulong clientId)
        {
            // The host's own connection ending is a session shutdown, not a lost console.
            if (!IsSpawned || !IsServer || _roster == null || _networkManager == null || _networkManager.ShutdownInProgress)
            {
                return;
            }

            _eliminated.Clear();
            IReadOnlyList<ParticipantVitality> participants = _roster.Participants;
            for (int index = 0; index < participants.Count; index++)
            {
                ParticipantId participantId = participants[index].ParticipantId;
                if (participantRegistry.IsParticipantOwnedBy(participantId, clientId) && participants[index].IsInHouse)
                {
                    _eliminated.Add(participantId);
                }
            }

            for (int index = 0; index < _eliminated.Count; index++)
            {
                _roster.Eliminate(_eliminated[index]);
            }

            CompleteEliminations();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _roster == null)
            {
                return;
            }

            double now = SynchronizedTime;
            double elapsed = now - _lastEnergyUpdateTime;
            _lastEnergyUpdateTime = now;
            if (!IsMatchActive(now))
            {
                return;
            }

            if (elapsed > 0d)
            {
                DrainEnergy(elapsed);
            }

            if (!_roster.HasUnconsciousParticipants)
            {
                return;
            }

            _eliminated.Clear();
            if (_roster.EliminateExpired(now, _eliminated) > 0)
            {
                CompleteEliminations();
            }
        }

        private void DrainEnergy(double elapsed)
        {
            IReadOnlyList<ParticipantVitality> participants = _roster.Participants;
            for (int index = 0; index < participants.Count; index++)
            {
                ParticipantId participantId = participants[index].ParticipantId;
                bool hasAvatar = avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                    && avatar != null
                    && avatar.IsSpawned;
                bool running = hasAvatar && avatar.IsRunningOnServer;
                if (_roster.DrainEnergy(participantId, elapsed, running, true))
                {
                    PublishRecord(participantId);
                }

                if (hasAvatar)
                {
                    avatar.SetEnergyOnServer(participants[index].Energy);
                }
            }
        }

        /// <summary>Replicates the new energy and pushes the exact value to the avatar.</summary>
        private void PublishEnergy(ParticipantId participantId)
        {
            PublishRecord(participantId);
            if (_roster.TryGet(participantId, out ParticipantVitality vitality)
                && avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                && avatar != null
                && avatar.IsSpawned)
            {
                avatar.SetEnergyOnServer(vitality.Energy);
            }
        }

        /// <summary>Publishes and despawns the participants in <see cref="_eliminated"/>, then checks for defeat.</summary>
        private void CompleteEliminations()
        {
            if (_eliminated.Count == 0)
            {
                return;
            }

            for (int index = 0; index < _eliminated.Count; index++)
            {
                ParticipantId participantId = _eliminated[index];
                PublishRecord(participantId);
                if (avatarSpawner.TryGetAvatar(participantId, out NetworkParticipantAvatar avatar)
                    && avatar != null
                    && avatar.IsSpawned)
                {
                    avatar.NetworkObject.Despawn(true);
                }
            }

            _eliminated.Clear();
            if (_roster.AreAllEliminated && !matchTimeline.Snapshot.IsComplete)
            {
                matchTimeline.EndMatchOnServer(MatchOutcome.Defeat);
            }
        }

        private void HandleAvatarSpawned(NetworkParticipantAvatar avatar)
        {
            if (avatar == null || !avatar.IsSpawned || !avatar.ParticipantId.IsValid || _roster == null)
            {
                return;
            }

            if (_roster.Register(avatar.ParticipantId))
            {
                PublishRecord(avatar.ParticipantId);
                return;
            }

            if (!_roster.TryGet(avatar.ParticipantId, out ParticipantVitality existing))
            {
                return;
            }

            if (!existing.IsInHouse)
            {
                // Elimination and escape are permanent: an avatar must never return for this participant.
                avatar.NetworkObject.Despawn(true);
                return;
            }

            avatar.SetIncapacitatedOnServer(existing.State == VitalityState.Unconscious);
            avatar.SetEnergyOnServer(existing.Energy);
        }

        private void HandleMonsterAttack(MonsterAttack attack)
        {
            if (IsSpawned && IsServer && _roster != null)
            {
                ApplyDamageOnServer(attack.TargetId, attack.Damage);
            }
        }

        private bool IsMatchActive(double now)
        {
            return _matchState != null && MatchTimelineAvailability.IsMatchActive(_matchState.Snapshot, now);
        }

        private void PublishRecord(ParticipantId participantId)
        {
            if (!_roster.TryGet(participantId, out ParticipantVitality vitality))
            {
                return;
            }

            var record = new NetworkParticipantVitalityRecord(vitality);
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

        private void HandleRecordsChanged(NetworkListEvent<NetworkParticipantVitalityRecord> changeEvent)
        {
            Changed?.Invoke();
        }

        private void HandleSnapshotChanged(MatchTimelineSnapshot snapshot)
        {
            Changed?.Invoke();
        }

        private void ValidateDependencies()
        {
            if (matchTimeline == null || participantRegistry == null || avatarSpawner == null || monster == null)
            {
                throw new InvalidOperationException(
                    "Participant vitality requires explicit match timeline, participant registry, avatar spawner and monster references.");
            }
        }
    }
}
