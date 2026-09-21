using System;
using HouseEscape.Core.Timeline;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MatchClockTests
    {
        [Test]
        public void ConstantsMatchCanonicalTwentyMinuteClock()
        {
            Assert.That(MatchClock.MatchDurationSeconds, Is.EqualTo(1200d));
            Assert.That(MatchClock.SecondsPerInGameHour, Is.EqualTo(200d));
        }

        [TestCase(0d, 0, 0)]
        [TestCase(100d, 0, 30)]
        [TestCase(200d, 1, 0)]
        [TestCase(400d, 2, 0)]
        [TestCase(600d, 3, 0)]
        [TestCase(800d, 4, 0)]
        [TestCase(1000d, 5, 0)]
        [TestCase(1200d, 6, 0)]
        [TestCase(1300d, 6, 0)]
        public void ConvertsElapsedSecondsToInGameTime(double elapsed, int expectedHour, int expectedMinute)
        {
            InGameTime result = MatchClock.GetInGameTime(elapsed);

            Assert.That(result.Hour, Is.EqualTo(expectedHour));
            Assert.That(result.Minute, Is.EqualTo(expectedMinute));
        }

        [TestCase(199.999d, 0, 59)]
        [TestCase(399.999d, 1, 59)]
        [TestCase(599.999d, 2, 59)]
        [TestCase(799.999d, 3, 59)]
        [TestCase(999.999d, 4, 59)]
        [TestCase(1199.999d, 5, 59)]
        public void DoesNotCrossHourBeforeBoundary(double elapsed, int expectedHour, int expectedMinute)
        {
            Assert.That(MatchClock.GetInGameTime(elapsed), Is.EqualTo(new InGameTime(expectedHour, expectedMinute)));
        }

        [TestCase(0d, 1200d)]
        [TestCase(200d, 1000d)]
        [TestCase(1199d, 1d)]
        [TestCase(1200d, 0d)]
        [TestCase(1500d, 0d)]
        public void RemainingTimeClampsAtDeadline(double elapsed, double expected)
        {
            Assert.That(MatchClock.GetRemainingSeconds(elapsed), Is.EqualTo(expected));
        }

        [TestCase(-0.001d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidElapsedTimeIsRejected(double elapsed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchClock.GetInGameTime(elapsed));
        }
    }
}
