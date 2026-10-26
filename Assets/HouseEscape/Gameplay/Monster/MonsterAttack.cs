using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>A server-side attack outcome, applied to the target's health by participant vitality.</summary>
    public readonly struct MonsterAttack
    {
        public MonsterAttack(ParticipantId targetId, int damage, double performedAt)
        {
            TargetId = targetId;
            Damage = damage;
            PerformedAt = performedAt;
        }

        public ParticipantId TargetId { get; }
        public int Damage { get; }
        public double PerformedAt { get; }
    }
}
