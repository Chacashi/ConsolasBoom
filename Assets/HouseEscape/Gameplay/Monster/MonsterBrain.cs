using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>
    /// Server-side monster state machine (GDD 4). Darkness forces Sleeping, drops the target and
    /// clears pursuit exclusions. A target that becomes hidden (untargetable) is dropped at once.
    /// While awake the monster targets the nearest detected player, switching whenever another
    /// detected player is strictly nearer; drops a target after five seconds without detection or
    /// after fifteen seconds of pursuit (then excludes that player for five seconds); and attacks
    /// its detected target in range at most once every two seconds.
    /// </summary>
    public sealed class MonsterBrain
    {
        private readonly Dictionary<ParticipantId, double> _excludedUntil = new Dictionary<ParticipantId, double>(4);
        private readonly List<ParticipantId> _expiredExclusions = new List<ParticipantId>(4);

        private ParticipantId _target;
        private double _pursuitStartedAt;
        private double _lastDetectedAt;
        private WorldPoint _lastKnownPosition;
        private double _nextAttackAt = double.NegativeInfinity;

        public MonsterState State { get; private set; } = MonsterState.Sleeping;

        public ParticipantId TargetId => _target;

        public bool IsExcluded(ParticipantId participantId, double now)
        {
            return _excludedUntil.TryGetValue(participantId, out double until) && now < until;
        }

        public MonsterDecision Tick(MonsterPerception perception)
        {
            double now = perception.Now;
            WorldPoint monster = perception.MonsterPosition;
            PruneExclusions(now);

            if (!perception.IsAwake)
            {
                ClearTarget();
                _excludedUntil.Clear();
                State = MonsterState.Sleeping;
                return new MonsterDecision(State, ParticipantId.None, MonsterMovement.Hold, monster, 0f, false, default);
            }

            if (_target.IsValid && now - _pursuitStartedAt >= MonsterRules.MaximumPursuitSeconds)
            {
                _excludedUntil[_target] = now + MonsterRules.ReacquireExclusionSeconds;
                ClearTarget();
            }

            bool targetPresent = false;
            bool targetDetected = false;
            float targetDistanceSquared = float.PositiveInfinity;
            WorldPoint targetPosition = default;
            int bestIndex = -1;
            float bestDistanceSquared = float.PositiveInfinity;
            IReadOnlyList<MonsterCandidate> candidates = perception.Candidates;
            for (int index = 0; index < candidates.Count; index++)
            {
                MonsterCandidate candidate = candidates[index];
                bool isTarget = _target.IsValid && candidate.ParticipantId == _target;
                targetPresent |= isTarget && candidate.IsTargetable;
                if (!MonsterRules.IsDetected(monster, candidate))
                {
                    continue;
                }

                float distanceSquared = WorldPoint.DistanceSquared(monster, candidate.Position);
                if (isTarget)
                {
                    targetDetected = true;
                    targetDistanceSquared = distanceSquared;
                    targetPosition = candidate.Position;
                    continue;
                }

                if (IsExcluded(candidate.ParticipantId, now))
                {
                    continue;
                }

                if (distanceSquared < bestDistanceSquared
                    || (distanceSquared == bestDistanceSquared
                        && bestIndex >= 0
                        && candidate.ParticipantId.Value < candidates[bestIndex].ParticipantId.Value))
                {
                    bestIndex = index;
                    bestDistanceSquared = distanceSquared;
                }
            }

            if (_target.IsValid && !targetPresent)
            {
                // The participant left the match (avatar despawned) or became hidden.
                ClearTarget();
            }

            if (targetDetected)
            {
                _lastDetectedAt = now;
                _lastKnownPosition = targetPosition;
            }

            bool switchedTarget = false;
            if (bestIndex >= 0 && (!targetDetected || bestDistanceSquared < targetDistanceSquared))
            {
                switchedTarget = true;
                MonsterCandidate best = candidates[bestIndex];
                _target = best.ParticipantId;
                _pursuitStartedAt = now;
                _lastDetectedAt = now;
                _lastKnownPosition = best.Position;
                targetDetected = true;
                targetPosition = best.Position;
            }
            else if (_target.IsValid && !targetDetected && now - _lastDetectedAt >= MonsterRules.LoseInterestSeconds)
            {
                ClearTarget();
            }

            if (!_target.IsValid)
            {
                State = MonsterState.Searching;
                return new MonsterDecision(State, ParticipantId.None, MonsterMovement.Wander, monster, MonsterRules.WanderSpeed, false, default);
            }

            float chaseSpeed = MonsterRules.GetChaseSpeed(perception.ActiveParticipantCount);
            if (!targetDetected)
            {
                State = MonsterState.LosingInterest;
                return new MonsterDecision(State, _target, MonsterMovement.Pursue, _lastKnownPosition, chaseSpeed, false, default);
            }

            bool inAttackRange = MonsterRules.IsWithinAttackRange(monster, targetPosition);
            bool keepsAttacking = !switchedTarget
                && State == MonsterState.Attacking
                && MonsterRules.IsWithinAttackStateRange(monster, targetPosition);
            if (!inAttackRange && !keepsAttacking)
            {
                State = MonsterState.Chasing;
                return new MonsterDecision(State, _target, MonsterMovement.Pursue, targetPosition, chaseSpeed, false, default);
            }

            State = MonsterState.Attacking;
            bool attacks = inAttackRange && now >= _nextAttackAt;
            MonsterAttack attack = default;
            if (attacks)
            {
                _nextAttackAt = now + MonsterRules.AttackIntervalSeconds;
                attack = new MonsterAttack(_target, MonsterRules.AttackDamage, now);
            }

            return new MonsterDecision(State, _target, MonsterMovement.Pursue, targetPosition, chaseSpeed, attacks, attack);
        }

        private void ClearTarget()
        {
            _target = ParticipantId.None;
            _pursuitStartedAt = 0d;
            _lastDetectedAt = 0d;
            _lastKnownPosition = default;
        }

        private void PruneExclusions(double now)
        {
            if (_excludedUntil.Count == 0)
            {
                return;
            }

            _expiredExclusions.Clear();
            foreach (KeyValuePair<ParticipantId, double> exclusion in _excludedUntil)
            {
                if (now >= exclusion.Value)
                {
                    _expiredExclusions.Add(exclusion.Key);
                }
            }

            for (int index = 0; index < _expiredExclusions.Count; index++)
            {
                _excludedUntil.Remove(_expiredExclusions[index]);
            }
        }
    }
}
