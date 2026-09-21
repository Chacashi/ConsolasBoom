using System;

namespace HouseEscape.Core.Timeline
{
    public readonly struct LightPhaseDurationRange : IEquatable<LightPhaseDurationRange>
    {
        public LightPhaseDurationRange(double minimumSeconds, double maximumSeconds)
        {
            if (double.IsNaN(minimumSeconds) || double.IsInfinity(minimumSeconds) || minimumSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSeconds));
            }

            if (double.IsNaN(maximumSeconds) || double.IsInfinity(maximumSeconds) || maximumSeconds < minimumSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
            }

            MinimumSeconds = minimumSeconds;
            MaximumSeconds = maximumSeconds;
        }

        public double MinimumSeconds { get; }

        public double MaximumSeconds { get; }

        public bool Contains(double seconds)
        {
            return seconds >= MinimumSeconds && seconds <= MaximumSeconds;
        }

        public bool Equals(LightPhaseDurationRange other)
        {
            return MinimumSeconds.Equals(other.MinimumSeconds) && MaximumSeconds.Equals(other.MaximumSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is LightPhaseDurationRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MinimumSeconds.GetHashCode() * 397) ^ MaximumSeconds.GetHashCode();
            }
        }
    }
}
