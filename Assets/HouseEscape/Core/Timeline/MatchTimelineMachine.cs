using System;

namespace HouseEscape.Core.Timeline
{
    public sealed class MatchTimelineMachine
    {
        private readonly ILightPhaseDurationSampler _durationSampler;
        private MatchTimelineSnapshot _snapshot;

        public MatchTimelineMachine(ILightPhaseDurationSampler durationSampler)
        {
            _durationSampler = durationSampler ?? throw new ArgumentNullException(nameof(durationSampler));
        }

        public MatchTimelineSnapshot Snapshot => _snapshot;

        public void Start(double absoluteTime)
        {
            ValidateAbsoluteTime(absoluteTime);
            if (_snapshot.IsStarted)
            {
                throw new InvalidOperationException("The match timeline has already started.");
            }

            LightPhaseDurationRange initialRange = LightCycleSchedule.GetDurationRange(0d, LightPhase.Off);
            double initialDuration = SampleValidated(initialRange);
            _snapshot = new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.Off,
                absoluteTime,
                absoluteTime + MatchClock.MatchDurationSeconds,
                absoluteTime + initialDuration,
                1u);
        }

        public bool Advance(double absoluteTime)
        {
            ValidateAbsoluteTime(absoluteTime);
            if (!_snapshot.IsStarted || _snapshot.IsComplete)
            {
                return false;
            }

            if (absoluteTime < _snapshot.MatchStartedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteTime), "Time cannot precede the match start.");
            }

            bool changed = false;
            while (true)
            {
                // The match deadline wins ties, so no light transition can occur at 6 AM.
                if (_snapshot.MatchDeadline <= _snapshot.PhaseEndsAt
                    && absoluteTime >= _snapshot.MatchDeadline)
                {
                    _snapshot = CopyWithCompletion(_snapshot);
                    return true;
                }

                if (absoluteTime < _snapshot.PhaseEndsAt)
                {
                    return changed;
                }

                TransitionAtPhaseDeadline();
                changed = true;
            }
        }

        private void TransitionAtPhaseDeadline()
        {
            double transitionTime = _snapshot.PhaseEndsAt;
            double elapsed = transitionTime - _snapshot.MatchStartedAt;
            LightPhase nextPhase = _snapshot.LightPhase == LightPhase.Off ? LightPhase.On : LightPhase.Off;
            LightPhaseDurationRange range = LightCycleSchedule.GetDurationRange(elapsed, nextPhase);
            double duration = SampleValidated(range);

            _snapshot = new MatchTimelineSnapshot(
                true,
                false,
                nextPhase,
                _snapshot.MatchStartedAt,
                _snapshot.MatchDeadline,
                transitionTime + duration,
                _snapshot.Revision + 1u);
        }

        private double SampleValidated(LightPhaseDurationRange range)
        {
            double duration = _durationSampler.Sample(range);
            if (double.IsNaN(duration) || double.IsInfinity(duration) || !range.Contains(duration))
            {
                throw new InvalidOperationException("The duration sampler returned a value outside the requested range.");
            }

            return duration;
        }

        private static MatchTimelineSnapshot CopyWithCompletion(MatchTimelineSnapshot snapshot)
        {
            return new MatchTimelineSnapshot(
                true,
                true,
                snapshot.LightPhase,
                snapshot.MatchStartedAt,
                snapshot.MatchDeadline,
                snapshot.PhaseEndsAt,
                snapshot.Revision + 1u);
        }

        private static void ValidateAbsoluteTime(double absoluteTime)
        {
            if (double.IsNaN(absoluteTime) || double.IsInfinity(absoluteTime) || absoluteTime < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteTime));
            }
        }
    }
}
