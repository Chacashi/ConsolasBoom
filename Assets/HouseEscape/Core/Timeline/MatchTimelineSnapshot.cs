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
            uint revision)
        {
            IsStarted = isStarted;
            IsComplete = isComplete;
            LightPhase = lightPhase;
            MatchStartedAt = matchStartedAt;
            MatchDeadline = matchDeadline;
            PhaseEndsAt = phaseEndsAt;
            Revision = revision;
        }

        public bool IsStarted { get; }

        public bool IsComplete { get; }

        public LightPhase LightPhase { get; }

        public bool LightsAreOn => LightPhase == LightPhase.On;

        public double MatchStartedAt { get; }

        public double MatchDeadline { get; }

        public double PhaseEndsAt { get; }

        public uint Revision { get; }

        public bool Equals(MatchTimelineSnapshot other)
        {
            return IsStarted == other.IsStarted
                && IsComplete == other.IsComplete
                && LightPhase == other.LightPhase
                && MatchStartedAt.Equals(other.MatchStartedAt)
                && MatchDeadline.Equals(other.MatchDeadline)
                && PhaseEndsAt.Equals(other.PhaseEndsAt)
                && Revision == other.Revision;
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
                return hashCode;
            }
        }
    }
}
