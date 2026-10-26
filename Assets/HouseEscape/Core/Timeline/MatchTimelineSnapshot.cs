using System;

namespace HouseEscape.Core.Timeline
{
    public readonly struct MatchTimelineSnapshot : IEquatable<MatchTimelineSnapshot>
    {
        public MatchTimelineSnapshot(
            bool isStarted,
            bool isComplete,
            LightPhase lightPhase,
            double matchStartedAt,
            double matchDeadline,
            double phaseEndsAt,
            uint revision,
            MatchOutcome outcome = MatchOutcome.None,
            double completedAt = double.NaN)
        {
            IsStarted = isStarted;
            IsComplete = isComplete;
            LightPhase = lightPhase;
            MatchStartedAt = matchStartedAt;
            MatchDeadline = matchDeadline;
            PhaseEndsAt = phaseEndsAt;
            Revision = revision;
            Outcome = outcome;
            // A completed snapshot without an explicit completion time ended at the deadline.
            CompletedAt = isComplete ? (double.IsNaN(completedAt) ? matchDeadline : completedAt) : 0d;
        }

        public bool IsStarted { get; }

        public bool IsComplete { get; }

        public LightPhase LightPhase { get; }

        public bool LightsAreOn => LightPhase == LightPhase.On;

        public double MatchStartedAt { get; }

        public double MatchDeadline { get; }

        public double PhaseEndsAt { get; }

        public uint Revision { get; }

        /// <summary>The outcome that ended the match early, or None.</summary>
        public MatchOutcome Outcome { get; }

        /// <summary>Server time at which the match completed (6 AM or an early outcome); zero while running.</summary>
        public double CompletedAt { get; }

        public bool Equals(MatchTimelineSnapshot other)
        {
            return IsStarted == other.IsStarted
                && IsComplete == other.IsComplete
                && LightPhase == other.LightPhase
                && MatchStartedAt.Equals(other.MatchStartedAt)
                && MatchDeadline.Equals(other.MatchDeadline)
                && PhaseEndsAt.Equals(other.PhaseEndsAt)
                && Revision == other.Revision
                && Outcome == other.Outcome
                && CompletedAt.Equals(other.CompletedAt);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchTimelineSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IsStarted.GetHashCode();
                hashCode = (hashCode * 397) ^ IsComplete.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)LightPhase;
                hashCode = (hashCode * 397) ^ MatchStartedAt.GetHashCode();
                hashCode = (hashCode * 397) ^ MatchDeadline.GetHashCode();
                hashCode = (hashCode * 397) ^ PhaseEndsAt.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Revision;
                hashCode = (hashCode * 397) ^ (int)Outcome;
                hashCode = (hashCode * 397) ^ CompletedAt.GetHashCode();
                return hashCode;
            }
        }
    }
}
