using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Movement;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;
using UnityEngine;

namespace HouseEscape.Tests.EditMode
{
    public sealed class EnergyRulesTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);

        [TestCase(100f, 1f)]
        [TestCase(60f, 1f)]
        [TestCase(59.9f, 0.8f)]
        [TestCase(30f, 0.8f)]
        [TestCase(29.9f, 0.6f)]
        [TestCase(10f, 0.6f)]
        [TestCase(9.9f, 0.4f)]
        [TestCase(0f, 0.4f)]
        public void SpeedPenaltyFollowsWholePercentageBrackets(float energy, float expected)
        {
            Assert.That(EnergyRules.GetSpeedMultiplier(energy), Is.EqualTo(expected));
        }

        [Test]
        public void RunningMultipliesWalkSpeedAndStacksWithPenaltyButNotBelowTenEnergy()
        {
            Assert.That(EnergyRules.GetMovementSpeed(100f, false), Is.EqualTo(MovementMotor.WalkSpeed));
            Assert.That(EnergyRules.GetMovementSpeed(100f, true), Is.EqualTo(MovementMotor.WalkSpeed * 1.6f).Within(1e-5f));
            Assert.That(EnergyRules.GetMovementSpeed(45f, true), Is.EqualTo(MovementMotor.WalkSpeed * 1.6f * 0.8f).Within(1e-5f));
            Assert.That(EnergyRules.CanRun(10f), Is.True);
            Assert.That(EnergyRules.CanRun(9.99f), Is.False);
            Assert.That(EnergyRules.GetMovementSpeed(5f, true), Is.EqualTo(MovementMotor.WalkSpeed * 0.4f).Within(1e-5f),
                "Below 10 energy a run request is ignored.");
        }

        [Test]
        public void DrainIsAdditiveAndOnlyWhileConsciousInAnActiveMatch()
        {
            double passive = 0.5d / 60d;
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Conscious, 100, false, true), Is.EqualTo(passive).Within(1e-9));
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Conscious, 100, true, true), Is.EqualTo(passive + 2d).Within(1e-9));
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Conscious, 49, true, true), Is.EqualTo(passive + 3d).Within(1e-9));
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Conscious, 50, false, true), Is.EqualTo(passive).Within(1e-9),
                "Injured means health below 50.");
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Conscious, 100, true, false), Is.Zero);
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Unconscious, 0, false, true), Is.Zero);
            Assert.That(EnergyRules.GetDrainPerSecond(VitalityState.Eliminated, 0, false, true), Is.Zero);
        }

        [Test]
        public void RosterStartsAtFullEnergyAndDrainsClampingAtZero()
        {
            var roster = new ParticipantVitalityRoster();
            Assert.That(roster.Register(First), Is.True);
            Assert.That(Energy(roster), Is.EqualTo(100f));

            Assert.That(roster.DrainEnergy(First, 10d, true, true), Is.True);
            Assert.That(Energy(roster), Is.EqualTo(100f - 20f - 10f * (0.5f / 60f)).Within(1e-4f));

            Assert.That(roster.DrainEnergy(First, 0.1d, false, true), Is.False,
                "A sub-percent change does not change the replicated whole percentage.");
            Assert.That(roster.DrainEnergy(First, 1000d, true, true), Is.True);
            Assert.That(Energy(roster), Is.Zero);
            Assert.That(roster.DrainEnergy(First, 1d, true, true), Is.False);
            Assert.That(roster.DrainEnergy(First, -1d, true, true), Is.False);
            Assert.That(roster.DrainEnergy(First, double.NaN, true, true), Is.False);
        }

        [Test]
        public void RosterKeepsEnergyAcrossIncapacitationAndStopsDraining()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.DrainEnergy(First, 5d, true, true);
            float before = Energy(roster);

            roster.ApplyDamage(First, 100, 10d);
            Assert.That(Energy(roster), Is.EqualTo(before));
            Assert.That(roster.DrainEnergy(First, 10d, true, true), Is.False);
            Assert.That(Energy(roster), Is.EqualTo(before));
            Assert.That(roster.RestoreEnergy(First, 10f), Is.False, "Unconscious participants cannot gain energy.");
        }

        [Test]
        public void RestoreEnergyIsCappedAndRejectsInvalidAmounts()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.SetEnergy(First, 75f);

            Assert.That(roster.RestoreEnergy(First, 40f), Is.True);
            Assert.That(Energy(roster), Is.EqualTo(100f));
            Assert.That(roster.RestoreEnergy(First, 0f), Is.False);
            Assert.That(roster.RestoreEnergy(First, float.NaN), Is.False);
            Assert.That(roster.RestoreEnergy(new ParticipantId(2), 10f), Is.False);
            Assert.That(roster.SetEnergy(First, 250f), Is.True);
            Assert.That(Energy(roster), Is.EqualTo(100f));
        }

        [Test]
        public void HudShowsWholeClampedEnergy()
        {
            Assert.That(VitalityHudText.FormatEnergy(42), Is.EqualTo("Energy: 42"));
            Assert.That(VitalityHudText.FormatEnergy(-3), Is.EqualTo("Energy: 0"));
            Assert.That(VitalityHudText.FormatEnergy(140), Is.EqualTo("Energy: 100"));
            Assert.That(new ParticipantVitality(First, 100, VitalityState.Conscious, 0d, 59.9f).WholeEnergy, Is.EqualTo(59));
        }

        [Test]
        public void CameraFieldOfViewNarrowsOnlyFromTenToTwentyNineWhileConscious()
        {
            Assert.That(EnergyCameraEffects.GetFieldOfView(VitalityState.Conscious, 30), Is.EqualTo(70f));
            Assert.That(EnergyCameraEffects.GetFieldOfView(VitalityState.Conscious, 29), Is.EqualTo(60f));
            Assert.That(EnergyCameraEffects.GetFieldOfView(VitalityState.Conscious, 10), Is.EqualTo(60f));
            Assert.That(EnergyCameraEffects.GetFieldOfView(VitalityState.Unconscious, 20), Is.EqualTo(70f));
        }

        [Test]
        public void CameraSwaysOnlyBelowTenEnergyWhileConscious()
        {
            Assert.That(EnergyCameraEffects.IsSwaying(VitalityState.Conscious, 9), Is.True);
            Assert.That(EnergyCameraEffects.IsSwaying(VitalityState.Conscious, 10), Is.False);
            Assert.That(EnergyCameraEffects.IsSwaying(VitalityState.Unconscious, 0), Is.False);
            Assert.That(EnergyCameraEffects.GetSway(VitalityState.Conscious, 50, 0.4d), Is.EqualTo(Quaternion.identity));

            Quaternion sway = EnergyCameraEffects.GetSway(VitalityState.Conscious, 5, 0.4d);
            Assert.That(Quaternion.Angle(sway, Quaternion.identity), Is.GreaterThan(0f));
            Assert.That(Quaternion.Angle(sway, Quaternion.identity),
                Is.LessThanOrEqualTo(EnergyCameraEffects.SwayRollDegrees + EnergyCameraEffects.SwayPitchDegrees));
        }

        private static float Energy(ParticipantVitalityRoster roster)
        {
            Assert.That(roster.TryGet(First, out ParticipantVitality vitality), Is.True);
            return vitality.Energy;
        }
    }
}
