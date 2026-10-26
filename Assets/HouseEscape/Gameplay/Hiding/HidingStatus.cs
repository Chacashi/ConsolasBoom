using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Hiding
{
    /// <summary>
    /// One participant's use of hiding places. <see cref="PhaseStartedAt"/> is when the current
    /// phase began and <see cref="PhaseEndsAt"/> when it ends on its own (entry completes, maximum
    /// time forces the player out, vulnerability ends). <see cref="CooldownEndsAt"/> is when the
    /// participant may hide again.
    /// </summary>
    public readonly struct HidingStatus
    {
        public HidingStatus(
            ParticipantId participantId,
            HidingPhase phase,
            int spotIndex,
            HidingSpotType spotType,
            double phaseStartedAt,
            double phaseEndsAt,
            double cooldownEndsAt,
            bool isProtected,
            bool lostMonster)
        {
            ParticipantId = participantId;
            Phase = phase;
            SpotIndex = spotIndex;
            SpotType = spotType;
            PhaseStartedAt = phaseStartedAt;
            PhaseEndsAt = phaseEndsAt;
            CooldownEndsAt = cooldownEndsAt;
            IsProtected = isProtected;
            LostMonster = lostMonster;
        }

        public ParticipantId ParticipantId { get; }
        public HidingPhase Phase { get; }
        public int SpotIndex { get; }
        public HidingSpotType SpotType { get; }
        public double PhaseStartedAt { get; }
        public double PhaseEndsAt { get; }
        public double CooldownEndsAt { get; }

        /// <summary>Hidden and not a valid monster target.</summary>
        public bool IsProtected { get; }

        /// <summary>The monster was targeting this participant when the hiding place protected them.</summary>
        public bool LostMonster { get; }

        /// <summary>Occupies its hiding place (entering or hidden).</summary>
        public bool OccupiesSpot => Phase == HidingPhase.Entering || Phase == HidingPhase.Hidden;

        /// <summary>Walking (and acting) is blocked while entering, hidden or vulnerable.</summary>
        public bool BlocksMovement => Phase != HidingPhase.None;
    }
}
