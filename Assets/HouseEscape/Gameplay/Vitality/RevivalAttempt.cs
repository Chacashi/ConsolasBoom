using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>A reviver holding the south face button on an unconscious teammate.</summary>
    public readonly struct RevivalAttempt
    {
        public RevivalAttempt(ParticipantId reviverId, ParticipantId targetId, double startedAt)
        {
            ReviverId = reviverId;
            TargetId = targetId;
            StartedAt = startedAt;
        }

        public ParticipantId ReviverId { get; }
        public ParticipantId TargetId { get; }
        public double StartedAt { get; }
        public double CompletesAt => StartedAt + VitalityRules.RevivalHoldSeconds;
    }
}
