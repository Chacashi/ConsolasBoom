using System;

namespace HouseEscape.Core.Timeline
{
    public static class LightCycleSchedule
    {
        public static LightPhaseDurationRange GetDurationRange(double matchElapsedSeconds, LightPhase phase)
        {
            if (double.IsNaN(matchElapsedSeconds) || double.IsInfinity(matchElapsedSeconds) || matchElapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(matchElapsedSeconds));
            }

            if (matchElapsedSeconds >= MatchClock.MatchDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(matchElapsedSeconds), "No light phase begins at or after 6 AM.");
            }

            if (matchElapsedSeconds < 400d)
            {
                return phase == LightPhase.On
                    ? new LightPhaseDurationRange(50d, 60d)
                    : new LightPhaseDurationRange(80d, 90d);
            }

            if (matchElapsedSeconds < 800d)
            {
                return phase == LightPhase.On
                    ? new LightPhaseDurationRange(35d, 45d)
                    : new LightPhaseDurationRange(50d, 60d);
            }

            if (matchElapsedSeconds < 1000d)
            {
                return phase == LightPhase.On
                    ? new LightPhaseDurationRange(25d, 30d)
                    : new LightPhaseDurationRange(35d, 45d);
            }

            return phase == LightPhase.On
                ? new LightPhaseDurationRange(15d, 20d)
                : new LightPhaseDurationRange(25d, 30d);
        }
    }
}
