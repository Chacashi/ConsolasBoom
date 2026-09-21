using HouseEscape.Core.Timeline;
using HouseEscape.Presentation.Timeline;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MatchTimelineReadModelTests
    {
        [TestCase(96.999d, false)]
        [TestCase(97d, true)]
        [TestCase(99.999d, true)]
        [TestCase(100d, false)]
        public void WarningIsDerivedFromFinalThreeSeconds(double synchronizedTime, bool expectedWarning)
        {
            MatchTimelineSnapshot snapshot = CreateSnapshot(false, 0d, 1200d, 100d);

            MatchTimelineReadModel result = MatchTimelineReadModel.Create(snapshot, synchronizedTime);

            Assert.That(result.IsLightTransitionWarning, Is.EqualTo(expectedWarning));
        }

        [Test]
        public void DoesNotWarnForPhaseDeadlineAtMatchDeadline()
        {
            MatchTimelineSnapshot snapshot = CreateSnapshot(false, 0d, 1200d, 1200d);

            MatchTimelineReadModel result = MatchTimelineReadModel.Create(snapshot, 1198d);

            Assert.That(result.IsLightTransitionWarning, Is.False);
            Assert.That(result.LightPhaseRemainingSeconds, Is.Zero);
        }

        [Test]
        public void DoesNotAdvertiseTransitionAfterMatchDeadline()
        {
            MatchTimelineSnapshot snapshot = CreateSnapshot(false, 0d, 1200d, 1225d);

            MatchTimelineReadModel result = MatchTimelineReadModel.Create(snapshot, 1190d);

            Assert.That(result.MatchRemainingSeconds, Is.EqualTo(10d));
            Assert.That(result.LightPhaseRemainingSeconds, Is.Zero);
            Assert.That(result.IsLightTransitionWarning, Is.False);
        }

        [Test]
        public void DoesNotWarnAfterAuthoritativeCompletion()
        {
            MatchTimelineSnapshot snapshot = CreateSnapshot(true, 0d, 1200d, 1201d);

            MatchTimelineReadModel result = MatchTimelineReadModel.Create(snapshot, 1200d);

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.IsLightTransitionWarning, Is.False);
            Assert.That(result.MatchRemainingSeconds, Is.Zero);
            Assert.That(result.InGameTime, Is.EqualTo(new InGameTime(6, 0)));
        }

        [Test]
        public void CountdownAndClockAreDerivedFromSynchronizedTime()
        {
            MatchTimelineSnapshot snapshot = CreateSnapshot(false, 50d, 1250d, 130d);

            MatchTimelineReadModel result = MatchTimelineReadModel.Create(snapshot, 75d);

            Assert.That(result.ElapsedSeconds, Is.EqualTo(25d));
            Assert.That(result.MatchRemainingSeconds, Is.EqualTo(1175d));
            Assert.That(result.LightPhaseRemainingSeconds, Is.EqualTo(55d));
        }

        private static MatchTimelineSnapshot CreateSnapshot(
            bool complete,
            double startedAt,
            double deadline,
            double phaseEndsAt)
        {
            return new MatchTimelineSnapshot(
                true,
                complete,
                LightPhase.Off,
                startedAt,
                deadline,
                phaseEndsAt,
                1u);
        }
    }
}
