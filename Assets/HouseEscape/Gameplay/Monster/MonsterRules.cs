using HouseEscape.Gameplay.Movement;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>Monster tuning. GDD 4.2/4.3 values unless marked as a technical placeholder.</summary>
    public static class MonsterRules
    {
        public const float DetectionRadiusMetres = 8f;
        public const int AttackDamage = 30;
        public const double AttackIntervalSeconds = 2d;
        public const double LoseInterestSeconds = 5d;
        public const double MaximumPursuitSeconds = 15d;
        public const double ReacquireExclusionSeconds = 5d;
        public const float ChaseSpeedMultiplier = 1.2f;
        public const float FourParticipantSpeedBonus = 1.15f;
        public const int SpeedBonusParticipantCount = 4;

        public const float AttackRangeMetres = 1.5f;
        public const float WanderSpeed = MovementMotor.WalkSpeed;

        /// <summary>
        /// Technical: once Attacking, the state is kept until the target is this much beyond the
        /// attack range, so the replicated state does not flap at the boundary. Attacks still
        /// require the target inside <see cref="AttackRangeMetres"/>.
        /// </summary>
        public const float AttackStateExitHysteresisMetres = 0.25f;

        /// <summary>Technical: attack range is horizontal; this bounds height (e.g. other floors).</summary>
        public const float AttackMaximumHeightDifferenceMetres = 1.5f;

        public static float GetChaseSpeed(int activeParticipantCount)
        {
            float speed = MovementMotor.WalkSpeed * ChaseSpeedMultiplier;
            return activeParticipantCount >= SpeedBonusParticipantCount ? speed * FourParticipantSpeedBonus : speed;
        }

        public static bool IsWithinDetectionRadius(WorldPoint monster, WorldPoint participant)
        {
            return WorldPoint.DistanceSquared(monster, participant) <= DetectionRadiusMetres * DetectionRadiusMetres;
        }

        public static bool IsWithinAttackRange(WorldPoint monster, WorldPoint participant)
        {
            return IsWithinHorizontalReach(monster, participant, AttackRangeMetres);
        }

        /// <summary>Range used to stay in the Attacking state (attack range plus hysteresis).</summary>
        public static bool IsWithinAttackStateRange(WorldPoint monster, WorldPoint participant)
        {
            return IsWithinHorizontalReach(monster, participant, AttackRangeMetres + AttackStateExitHysteresisMetres);
        }

        private static bool IsWithinHorizontalReach(WorldPoint monster, WorldPoint participant, float reach)
        {
            float dx = monster.X - participant.X;
            float dz = monster.Z - participant.Z;
            float dy = monster.Y - participant.Y;
            return dx * dx + dz * dz <= reach * reach
                && dy <= AttackMaximumHeightDifferenceMetres
                && dy >= -AttackMaximumHeightDifferenceMetres;
        }

        /// <summary>Detected: targetable, inside the 360-degree radius and in line of sight.</summary>
        public static bool IsDetected(WorldPoint monster, MonsterCandidate candidate)
        {
            return candidate.ParticipantId.IsValid
                && candidate.IsTargetable
                && candidate.HasLineOfSight
                && IsWithinDetectionRadius(monster, candidate.Position);
        }
    }
}
