using System;
using HouseEscape.Core.Timeline;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class LightCycleScheduleTests
    {
        [TestCase(0d, LightPhase.On, 50d, 60d)]
        [TestCase(399.999d, LightPhase.Off, 80d, 90d)]
        [TestCase(400d, LightPhase.On, 35d, 45d)]
        [TestCase(799.999d, LightPhase.Off, 50d, 60d)]
        [TestCase(800d, LightPhase.On, 25d, 30d)]
        [TestCase(999.999d, LightPhase.Off, 35d, 45d)]
        [TestCase(1000d, LightPhase.On, 15d, 20d)]
        [TestCase(1199.999d, LightPhase.Off, 25d, 30d)]
        public void SelectsCanonicalHalfOpenBracket(
            double elapsed,
            LightPhase phase,
            double expectedMinimum,
            double expectedMaximum)
        {
            LightPhaseDurationRange result = LightCycleSchedule.GetDurationRange(elapsed, phase);

            Assert.That(result.MinimumSeconds, Is.EqualTo(expectedMinimum));
            Assert.That(result.MaximumSeconds, Is.EqualTo(expectedMaximum));
        }

        [TestCase(0d, 50d, 60d, 80d, 90d)]
        [TestCase(400d, 35d, 45d, 50d, 60d)]
        [TestCase(800d, 25d, 30d, 35d, 45d)]
        [TestCase(1000d, 15d, 20d, 25d, 30d)]
        public void DefinesBothPhaseRangesForEveryBracket(
            double elapsed,
            double onMinimum,
            double onMaximum,
            double offMinimum,
            double offMaximum)
        {
            Assert.That(
                LightCycleSchedule.GetDurationRange(elapsed, LightPhase.On),
                Is.EqualTo(new LightPhaseDurationRange(onMinimum, onMaximum)));
            Assert.That(
                LightCycleSchedule.GetDurationRange(elapsed, LightPhase.Off),
                Is.EqualTo(new LightPhaseDurationRange(offMinimum, offMaximum)));
        }

        [TestCase(-1d)]
        [TestCase(1200d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsTimesOutsideActiveMatch(double elapsed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LightCycleSchedule.GetDurationRange(elapsed, LightPhase.Off));
        }
    }
}
