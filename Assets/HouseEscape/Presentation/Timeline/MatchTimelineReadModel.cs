using System;
using HouseEscape.Core.Timeline;

namespace HouseEscape.Presentation.Timeline
{
    public readonly struct MatchTimelineReadModel : IEquatable<MatchTimelineReadModel>
    {
        private MatchTimelineReadModel(
            bool isAvailable,
            bool isComplete,
            bool lightsAreOn,
            bool isLightTransitionWarning,
            double elapsedSeconds,
            double matchRemainingSeconds,
            double lightPhaseRemainingSeconds,
            InGameTime inGameTime,
            double matchStartedAt,
            uint revision)
        {
            IsAvailable = isAvailable;
            IsComplete = isComplete;
            LightsAreOn = lightsAreOn;
            IsLightTransitionWarning = isLightTransitionWarning;
            ElapsedSeconds = elapsedSeconds;
            MatchRemainingSeconds = matchRemainingSeconds;
            LightPhaseRemainingSeconds = lightPhaseRemainingSeconds;
            InGameTime = inGameTime;
            MatchStartedAt = matchStartedAt;
            Revision = revision;
        }

        public bool IsAvailable { get; }

        public bool IsComplete { get; }

        public bool LightsAreOn { get; }

        public bool IsLightTransitionWarning { get; }

        public double ElapsedSeconds { get; }

        public double MatchRemainingSeconds { get; }

        public double LightPhaseRemainingSeconds { get; }

        public InGameTime InGameTime { get; }

        public uint Revision { get; }

        public double MatchStartedAt { get; }

        public static MatchTimelineReadModel Create(MatchTimelineSnapshot snapshot, double synchronizedTime)
        {
            if (!snapshot.IsStarted)
            {
                return default;
            }

            double timelineNow = Math.Max(snapshot.MatchStartedAt, synchronizedTime);
            if (snapshot.IsComplete)
            {
                // A finished match (6 AM or an early outcome) freezes the clock at completion.
                timelineNow = Math.Max(snapshot.MatchStartedAt, Math.Min(timelineNow, snapshot.CompletedAt));
            }

            double elapsed = Math.Min(timelineNow - snapshot.MatchStartedAt, MatchClock.MatchDurationSeconds);
            double matchRemaining = Math.Max(0d, snapshot.MatchDeadline - timelineNow);
            bool transitionOccursBeforeDeadline = snapshot.PhaseEndsAt < snapshot.MatchDeadline;
            double phaseRemaining = transitionOccursBeforeDeadline && !snapshot.IsComplete
                ? Math.Max(0d, snapshot.PhaseEndsAt - timelineNow)
                : 0d;
            bool warning = !snapshot.IsComplete
                && transitionOccursBeforeDeadline
                && phaseRemaining > 0d
                && phaseRemaining <= MatchClock.WarningDurationSeconds;

            return new MatchTimelineReadModel(
                true,
                snapshot.IsComplete,
                snapshot.LightsAreOn,
                warning,
                elapsed,
                matchRemaining,
                phaseRemaining,
                MatchClock.GetInGameTime(elapsed),
                snapshot.MatchStartedAt,
                snapshot.Revision);
        }

        public bool Equals(MatchTimelineReadModel other)
        {
            return IsAvailable == other.IsAvailable
                && IsComplete == other.IsComplete
                && LightsAreOn == other.LightsAreOn
                && IsLightTransitionWarning == other.IsLightTransitionWarning
                && ElapsedSeconds.Equals(other.ElapsedSeconds)
                && MatchRemainingSeconds.Equals(other.MatchRemainingSeconds)
                && LightPhaseRemainingSeconds.Equals(other.LightPhaseRemainingSeconds)
                && InGameTime.Equals(other.InGameTime)
                && MatchStartedAt.Equals(other.MatchStartedAt)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is MatchTimelineReadModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IsAvailable.GetHashCode();
                hashCode = (hashCode * 397) ^ IsComplete.GetHashCode();
                hashCode = (hashCode * 397) ^ LightsAreOn.GetHashCode();
                hashCode = (hashCode * 397) ^ IsLightTransitionWarning.GetHashCode();
                hashCode = (hashCode * 397) ^ ElapsedSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ MatchRemainingSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ LightPhaseRemainingSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ InGameTime.GetHashCode();
                hashCode = (hashCode * 397) ^ MatchStartedAt.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Revision;
                return hashCode;
            }
        }
    }
}
