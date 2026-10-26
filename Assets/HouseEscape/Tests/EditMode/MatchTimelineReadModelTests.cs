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

        [Test]
        public void EarlyDefeatFreezesClockAndRemovesTheLightCountdown()
        {
            var snapshot = new MatchTimelineSnapshot(
                true, true, LightPhase.Off, 0d, 1200d, 450d, 7u, MatchOutcome.Defeat, 400d);

            MatchTimelineReadModel atEnd = MatchTimelineReadModel.Create(snapshot, 400d);
            MatchTimelineReadModel later = MatchTimelineReadModel.Create(snapshot, 700d);

            Assert.That(later.ElapsedSeconds, Is.EqualTo(400d), "Elapsed time stops at completion.");
            Assert.That(later.MatchRemainingSeconds, Is.EqualTo(800d));
            Assert.That(later.InGameTime, Is.EqualTo(atEnd.InGameTime));
            Assert.That(later.LightPhaseRemainingSeconds, Is.Zero, "No light countdown after completion.");
            Assert.That(atEnd.LightPhaseRemainingSeconds, Is.Zero);
            Assert.That(later.IsLightTransitionWarning, Is.False);
        }

        [Test]
        public void MachineEarlyEndProducesAFrozenReadModel()
        {
            var machine = new MatchTimelineMachine(new FixedSampler());
            machine.Start(10d);
            machine.EndEarly(MatchOutcome.Defeat, 70d);

            MatchTimelineReadModel model = MatchTimelineReadModel.Create(machine.Snapshot, 500d);

            Assert.That(model.IsComplete, Is.True);
            Assert.That(model.ElapsedSeconds, Is.EqualTo(60d));
            Assert.That(model.MatchRemainingSeconds, Is.EqualTo(MatchClock.MatchDurationSeconds - 60d));
            Assert.That(model.LightPhaseRemainingSeconds, Is.Zero);
        }

        private sealed class FixedSampler : ILightPhaseDurationSampler
        {
            public double Sample(LightPhaseDurationRange range) => range.MinimumSeconds;
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
