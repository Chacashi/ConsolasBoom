using System;
using System.Collections.Generic;
using HouseEscape.Core.Timeline;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MatchTimelineMachineTests
    {
        [Test]
        public void StartsOffUsingAnAbsoluteServerTimestamp()
        {
            var sampler = new MinimumDurationSampler();
            var machine = new MatchTimelineMachine(sampler);

            machine.Start(250d);

            Assert.That(machine.Snapshot.IsStarted, Is.True);
            Assert.That(machine.Snapshot.IsComplete, Is.False);
            Assert.That(machine.Snapshot.LightPhase, Is.EqualTo(LightPhase.Off));
            Assert.That(machine.Snapshot.MatchStartedAt, Is.EqualTo(250d));
            Assert.That(machine.Snapshot.MatchDeadline, Is.EqualTo(1450d));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.EqualTo(330d));
            Assert.That(sampler.RequestedRanges[0], Is.EqualTo(new LightPhaseDurationRange(80d, 90d)));
        }

        [Test]
        public void TransitionsUsePreviousDeadlineAndDoNotDriftWithLateTicks()
        {
            var machine = new MatchTimelineMachine(new MinimumDurationSampler());
            machine.Start(0d);

            Assert.That(machine.Advance(81d), Is.True);

            Assert.That(machine.Snapshot.LightPhase, Is.EqualTo(LightPhase.On));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.EqualTo(130d));
        }

        [Test]
        public void HitchCatchesUpEveryElapsedPhaseWithoutDrift()
        {
            var machine = new MatchTimelineMachine(new MinimumDurationSampler());
            machine.Start(0d);

            Assert.That(machine.Advance(300d), Is.True);

            Assert.That(machine.Snapshot.LightPhase, Is.EqualTo(LightPhase.Off));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.EqualTo(340d));
            Assert.That(machine.Snapshot.Revision, Is.EqualTo(5u));
        }

        [Test]
        public void CatchUpSelectsBracketAtEachAbsoluteTransitionTimestamp()
        {
            var sampler = new MinimumDurationSampler();
            var machine = new MatchTimelineMachine(sampler);
            machine.Start(0d);

            machine.Advance(470d);

            LightPhaseDurationRange lastRequest = sampler.RequestedRanges[sampler.RequestedRanges.Count - 1];
            Assert.That(lastRequest, Is.EqualTo(new LightPhaseDurationRange(35d, 45d)));
            Assert.That(machine.Snapshot.LightPhase, Is.EqualTo(LightPhase.On));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.EqualTo(505d));
        }

        [Test]
        public void DeadlineWinsTieAndPreventsSixAmLightTransition()
        {
            var sampler = new MinimumDurationSampler();
            var machine = new MatchTimelineMachine(sampler);
            machine.Start(0d);

            machine.Advance(1200d);

            Assert.That(machine.Snapshot.IsComplete, Is.True);
            Assert.That(machine.Snapshot.LightPhase, Is.EqualTo(LightPhase.Off));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.EqualTo(1200d));
            Assert.That(sampler.RequestedRanges.Count, Is.EqualTo(31));
        }

        [Test]
        public void LargeHitchStopsAtDeadlineEvenWhenCurrentPhaseWouldEndLater()
        {
            var machine = new MatchTimelineMachine(new MaximumDurationSampler());
            machine.Start(100d);

            machine.Advance(5000d);

            Assert.That(machine.Snapshot.IsComplete, Is.True);
            Assert.That(machine.Snapshot.MatchDeadline, Is.EqualTo(1300d));
            Assert.That(machine.Snapshot.PhaseEndsAt, Is.GreaterThanOrEqualTo(1300d));
            Assert.That(machine.Advance(6000d), Is.False);
        }

        [Test]
        public void SamplerValueOutsideRequestedRangeIsRejected()
        {
            var machine = new MatchTimelineMachine(new InvalidDurationSampler());

            Assert.Throws<InvalidOperationException>(() => machine.Start(0d));
        }

        private sealed class MinimumDurationSampler : ILightPhaseDurationSampler
        {
            public List<LightPhaseDurationRange> RequestedRanges { get; } = new List<LightPhaseDurationRange>();

            public double Sample(LightPhaseDurationRange range)
            {
                RequestedRanges.Add(range);
                return range.MinimumSeconds;
            }
        }

        private sealed class MaximumDurationSampler : ILightPhaseDurationSampler
        {
            public double Sample(LightPhaseDurationRange range)
            {
                return range.MaximumSeconds;
            }
        }

        private sealed class InvalidDurationSampler : ILightPhaseDurationSampler
        {
            public double Sample(LightPhaseDurationRange range)
            {
                return range.MaximumSeconds + 1d;
            }
        }
    }
}
