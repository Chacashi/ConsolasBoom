using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>Commands produced by one <see cref="MonsterBrain.Tick"/>.</summary>
    public readonly struct MonsterDecision
    {
        public MonsterDecision(
            MonsterState state,
            ParticipantId targetId,
            MonsterMovement movement,
            WorldPoint destination,
            float speed,
            bool hasAttack,
            MonsterAttack attack)
        {
            State = state;
            TargetId = targetId;
            Movement = movement;
            Destination = destination;
            Speed = speed;
            HasAttack = hasAttack;
            Attack = attack;
        }

        public MonsterState State { get; }
        public ParticipantId TargetId { get; }
        public MonsterMovement Movement { get; }
        public WorldPoint Destination { get; }
        public float Speed { get; }
        public bool HasAttack { get; }
        public MonsterAttack Attack { get; }
    }
}
