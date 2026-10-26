using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>
    /// One participant as perceived by the server this tick. <see cref="IsTargetable"/> is false
    /// for players that may never be targeted (for example hidden players, once hiding exists).
    /// <see cref="HasLineOfSight"/> is the adapter's environment raycast result.
    /// </summary>
    public readonly struct MonsterCandidate
    {
        public MonsterCandidate(ParticipantId participantId, WorldPoint position, bool isTargetable, bool hasLineOfSight)
        {
            ParticipantId = participantId;
            Position = position;
            IsTargetable = isTargetable;
            HasLineOfSight = hasLineOfSight;
        }

        public ParticipantId ParticipantId { get; }
        public WorldPoint Position { get; }
        public bool IsTargetable { get; }
        public bool HasLineOfSight { get; }
    }
}
