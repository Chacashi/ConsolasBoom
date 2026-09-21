using System;
using HouseEscape.Core.Timeline;

namespace HouseEscape.Networking.Timeline
{
    internal sealed class UniformLightPhaseDurationSampler : ILightPhaseDurationSampler
    {
        private readonly Random _random;

        public UniformLightPhaseDurationSampler()
            : this(new Random())
        {
        }

        internal UniformLightPhaseDurationSampler(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public double Sample(LightPhaseDurationRange range)
        {
            return range.MinimumSeconds
                + (_random.NextDouble() * (range.MaximumSeconds - range.MinimumSeconds));
        }
    }
}
