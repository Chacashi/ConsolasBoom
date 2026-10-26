using System.Numerics;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Generators;
using HouseEscape.Presentation.Generators;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class GeneratorInteractionRulesTests
    {
        private static readonly Vector3 Generator = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AimPoint = new Vector3(0f, 0.7f, 0f);

        [Test]
        public void RangeIsInclusiveAtTwoMetresAndRejectsBeyond()
        {
            Assert.That(GeneratorInteractionRules.IsWithinRange(new Vector3(0f, 0f, -2f), Generator), Is.True);
            Assert.That(GeneratorInteractionRules.IsWithinRange(new Vector3(1.2f, 0f, -1.2f), Generator), Is.True);
            Assert.That(GeneratorInteractionRules.IsWithinRange(new Vector3(0f, 0f, -2.01f), Generator), Is.False);
            Assert.That(GeneratorInteractionRules.IsWithinRange(new Vector3(float.NaN, 0f, 0f), Generator), Is.False);
        }

        [Test]
        public void AimRequiresTheViewRayToPassThroughTheAimVolumeInFront()
        {
            var eye = new Vector3(0f, 1.6f, -1.5f);
            Vector3 towardTarget = AimPoint - eye;

            Assert.That(GeneratorInteractionRules.IsAimingAt(eye, towardTarget, AimPoint), Is.True);
            Assert.That(GeneratorInteractionRules.IsAimingAt(eye, new Vector3(0f, 0f, 1f), AimPoint), Is.False,
                "Looking straight ahead passes 0.9 m above the aim point.");
            Assert.That(GeneratorInteractionRules.IsAimingAt(eye, -towardTarget, AimPoint), Is.False,
                "Looking away from the generator is never aiming.");
            Assert.That(GeneratorInteractionRules.IsAimingAt(eye, Vector3.Zero, AimPoint), Is.False);
            Assert.That(GeneratorInteractionRules.IsAimingAt(AimPoint, new Vector3(1f, 0f, 0f), AimPoint), Is.True,
                "A viewer inside the aim volume is aiming at it.");
        }

        [Test]
        public void CanBeginRequiresBothRangeAndAim()
        {
            var nearEye = new Vector3(0f, 1.6f, -1.5f);
            var farEye = new Vector3(0f, 1.6f, -3f);

            Assert.That(GeneratorInteractionRules.CanBeginAttempt(
                new Vector3(0f, 0f, -1.5f), Generator, nearEye, AimPoint - nearEye, AimPoint), Is.True);
            Assert.That(GeneratorInteractionRules.CanBeginAttempt(
                new Vector3(0f, 0f, -3f), Generator, farEye, AimPoint - farEye, AimPoint), Is.False);
            Assert.That(GeneratorInteractionRules.CanBeginAttempt(
                new Vector3(0f, 0f, -1.5f), Generator, nearEye, new Vector3(1f, 0f, 0f), AimPoint), Is.False);
        }

        [Test]
        public void ServerAimToleranceAcceptsSmallSkewButRangeStaysStrict()
        {
            var eye = new Vector3(0f, 0.7f, -1.5f);
            var skewedForward = new Vector3(0f, 0f, 1f);
            var skewedAimPoint = new Vector3(0.7f, 0.7f, 0f);

            Assert.That(GeneratorInteractionRules.IsAimingAt(eye, skewedForward, skewedAimPoint), Is.False,
                "The client-side check uses the strict aim radius.");
            Assert.That(GeneratorInteractionRules.IsAimingAt(
                eye, skewedForward, skewedAimPoint, GeneratorInteractionRules.ServerAimRadiusMetres), Is.True);
            Assert.That(GeneratorInteractionRules.IsAimingAt(
                eye, skewedForward, new Vector3(1.0f, 0.7f, 0f), GeneratorInteractionRules.ServerAimRadiusMetres), Is.False);
            Assert.That(GeneratorInteractionRules.ServerAimToleranceMetres, Is.GreaterThan(0f));
            Assert.That(GeneratorInteractionRules.MaximumRangeMetres, Is.EqualTo(2f));
        }

        [Test]
        public void AvailableUntilIsTheEarlierOfLightsOnEndAndMatchDeadline()
        {
            var lightsOn = new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1200d, 150d, 2u);
            var nearDeadline = new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1200d, 1250d, 3u);
            var lightsOff = new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1200d, 150d, 4u);

            Assert.That(GeneratorRaceAvailability.GetAvailableUntil(lightsOn), Is.EqualTo(150d));
            Assert.That(GeneratorRaceAvailability.GetAvailableUntil(nearDeadline), Is.EqualTo(1200d));
            Assert.That(GeneratorRaceAvailability.GetAvailableUntil(lightsOff), Is.EqualTo(double.NegativeInfinity));
            Assert.That(GeneratorRaceAvailability.GetAvailableUntil(default), Is.EqualTo(double.NegativeInfinity));
        }

        [Test]
        public void HoldProgressIsClampedOverTheTenSecondHold()
        {
            Assert.That(GeneratorInteractionRules.GetHoldProgress(100d, 99d), Is.EqualTo(0f));
            Assert.That(GeneratorInteractionRules.GetHoldProgress(100d, 105d), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(GeneratorInteractionRules.GetHoldProgress(100d, 125d), Is.EqualTo(1f));
            Assert.That(GeneratorInteractionRules.GetHoldProgress(double.NaN, 1d), Is.EqualTo(0f));
        }

        [Test]
        public void AvailabilityClosesAtPhaseAndMatchDeadlinesBeforeTheNextSnapshot()
        {
            var lightsOn = new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1200d, 150d, 2u);
            var lightsOff = new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1200d, 150d, 2u);
            var complete = new MatchTimelineSnapshot(true, true, LightPhase.On, 0d, 1200d, 1300d, 9u);

            Assert.That(GeneratorRaceAvailability.AreLightsOn(lightsOn, 149.9d), Is.True);
            Assert.That(GeneratorRaceAvailability.AreLightsOn(lightsOn, 150d), Is.False);
            Assert.That(GeneratorRaceAvailability.AreLightsOn(lightsOff, 10d), Is.False);
            Assert.That(GeneratorRaceAvailability.IsMatchActive(lightsOn, 1199.9d), Is.True);
            Assert.That(GeneratorRaceAvailability.IsMatchActive(lightsOn, 1200d), Is.False);
            Assert.That(GeneratorRaceAvailability.IsMatchActive(complete, 10d), Is.False);
            Assert.That(GeneratorRaceAvailability.IsMatchActive(default, 0d), Is.False);
        }

        [Test]
        public void HudTextShowsGlobalProgressAndAttemptPercent()
        {
            Assert.That(GeneratorHudText.FormatProgress(3), Is.EqualTo("Generators: 3/6"));
            Assert.That(GeneratorHudText.FormatProgress(9), Is.EqualTo("Generators: 6/6 — front door open"));
            Assert.That(GeneratorHudText.FormatAttempt(2, 0.456f), Is.EqualTo("Generator 2: HOLD A / CROSS 45%"));
        }
    }
}
