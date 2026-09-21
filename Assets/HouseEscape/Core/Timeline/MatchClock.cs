using System;

namespace HouseEscape.Core.Timeline
{
    public static class MatchClock
    {
        public const double MatchDurationSeconds = 1200d;
        public const double SecondsPerInGameHour = 200d;
        public const double WarningDurationSeconds = 3d;

        public static double ClampElapsed(double elapsedSeconds)
        {
            ValidateTime(elapsedSeconds, nameof(elapsedSeconds));
            return Math.Min(elapsedSeconds, MatchDurationSeconds);
        }

        public static double GetRemainingSeconds(double elapsedSeconds)
        {
            return MatchDurationSeconds - ClampElapsed(elapsedSeconds);
        }

        public static InGameTime GetInGameTime(double elapsedSeconds)
        {
            double clampedElapsed = ClampElapsed(elapsedSeconds);
            int totalMinutes = clampedElapsed >= MatchDurationSeconds
                ? 360
                : (int)Math.Floor(clampedElapsed * 60d / SecondsPerInGameHour);

            return new InGameTime(totalMinutes / 60, totalMinutes % 60);
        }

        private static void ValidateTime(double seconds, string parameterName)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
