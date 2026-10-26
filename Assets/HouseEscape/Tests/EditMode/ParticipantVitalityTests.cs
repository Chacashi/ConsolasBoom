using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Monster;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;

namespace HouseEscape.Tests.EditMode
{
    public sealed class ParticipantVitalityTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);
        private static readonly ParticipantId Second = new ParticipantId(2);
        private static readonly ParticipantId Third = new ParticipantId(3);

        [Test]
        public void RegisteredParticipantsStartConsciousAtFullHealthOnce()
        {
            var roster = new ParticipantVitalityRoster();

            Assert.That(roster.Register(First), Is.True);
            Assert.That(roster.Register(First), Is.False);
            Assert.That(roster.Register(ParticipantId.None), Is.False);
            Assert.That(roster.TryGet(First, out ParticipantVitality vitality), Is.True);
            Assert.That(vitality.Health, Is.EqualTo(VitalityRules.MaximumHealth));
            Assert.That(vitality.State, Is.EqualTo(VitalityState.Conscious));
            Assert.That(roster.ConsciousCount, Is.EqualTo(1));
        }

        [Test]
        public void FourMonsterAttacksKnockUnconsciousWithClampedHealthAndAThirtySecondDeadline()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);

            for (int attack = 1; attack <= 3; attack++)
            {
                Assert.That(roster.ApplyDamage(First, MonsterRules.AttackDamage, attack * 2d), Is.EqualTo(VitalityDamageResult.Damaged));
            }

            roster.TryGet(First, out ParticipantVitality injured);
            Assert.That(injured.Health, Is.EqualTo(10));

            Assert.That(roster.ApplyDamage(First, MonsterRules.AttackDamage, 8d), Is.EqualTo(VitalityDamageResult.KnockedUnconscious));
            roster.TryGet(First, out ParticipantVitality unconscious);
            Assert.That(unconscious.Health, Is.Zero, "Health clamps at zero.");
            Assert.That(unconscious.State, Is.EqualTo(VitalityState.Unconscious));
            Assert.That(unconscious.RecoveryDeadline, Is.EqualTo(8d + VitalityRules.RecoveryWindowSeconds));
            Assert.That(unconscious.GetRecoverySecondsRemaining(8.2d), Is.EqualTo(30));
            Assert.That(roster.ConsciousCount, Is.Zero);
        }

        [Test]
        public void NonPositiveOrUnknownDamageIsIgnored()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);

            Assert.That(roster.ApplyDamage(First, 0, 1d), Is.EqualTo(VitalityDamageResult.Ignored));
            Assert.That(roster.ApplyDamage(First, -10, 1d), Is.EqualTo(VitalityDamageResult.Ignored));
            Assert.That(roster.ApplyDamage(Second, 30, 1d), Is.EqualTo(VitalityDamageResult.Ignored));
            roster.TryGet(First, out ParticipantVitality vitality);
            Assert.That(vitality.Health, Is.EqualTo(VitalityRules.MaximumHealth));
        }

        [Test]
        public void UnconsciousAndEliminatedParticipantsTakeNoDamageAndKeepTheirDeadline()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.ApplyDamage(First, 500, 10d);

            Assert.That(roster.ApplyDamage(First, 30, 12d), Is.EqualTo(VitalityDamageResult.NotConscious));
            roster.TryGet(First, out ParticipantVitality unconscious);
            Assert.That(unconscious.RecoveryDeadline, Is.EqualTo(40d), "Damage never extends or resets the window.");

            roster.EliminateExpired(40d, null);
            Assert.That(roster.ApplyDamage(First, 30, 41d), Is.EqualTo(VitalityDamageResult.NotConscious));
        }

        [Test]
        public void EliminationHappensExactlyAtTheThirtySecondDeadline()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.ApplyDamage(First, 100, 100d);
            var eliminated = new List<ParticipantId>();

            Assert.That(roster.EliminateExpired(129.999d, eliminated), Is.Zero);
            roster.TryGet(First, out ParticipantVitality stillUnconscious);
            Assert.That(stillUnconscious.State, Is.EqualTo(VitalityState.Unconscious));

            Assert.That(roster.EliminateExpired(130d, eliminated), Is.EqualTo(1));
            Assert.That(eliminated, Is.EqualTo(new[] { First }));
            roster.TryGet(First, out ParticipantVitality gone);
            Assert.That(gone.State, Is.EqualTo(VitalityState.Eliminated));
            Assert.That(gone.IsAlive, Is.False);
            Assert.That(roster.EliminateExpired(200d, eliminated), Is.Zero, "Elimination is reported once.");
        }

        [Test]
        public void DefeatRequiresEveryRegisteredParticipantEliminated()
        {
            var roster = new ParticipantVitalityRoster();
            Assert.That(roster.AreAllEliminated, Is.False, "An empty roster is not a defeat.");
            roster.Register(First);
            roster.Register(Second);

            roster.ApplyDamage(First, 100, 0d);
            roster.EliminateExpired(30d, null);
            Assert.That(roster.AreAllEliminated, Is.False);

            roster.ApplyDamage(Second, 100, 31d);
            Assert.That(roster.AreAllEliminated, Is.False, "Unconscious participants are not yet eliminated.");
            roster.EliminateExpired(61d, null);
            Assert.That(roster.AreAllEliminated, Is.True);
        }

        [Test]
        public void ConsciousCountDropsBelowFourWhenOneOfFourIsUnconscious()
        {
            var roster = new ParticipantVitalityRoster();
            for (ulong id = 1; id <= 4; id++)
            {
                roster.Register(new ParticipantId(id));
            }

            Assert.That(roster.ConsciousCount, Is.EqualTo(4));
            Assert.That(MonsterRules.GetChaseSpeed(roster.ConsciousCount),
                Is.EqualTo(MonsterRules.GetChaseSpeed(1) * MonsterRules.FourParticipantSpeedBonus).Within(0.0001f));

            roster.ApplyDamage(Third, 100, 0d);
            Assert.That(roster.ConsciousCount, Is.EqualTo(3));
            Assert.That(MonsterRules.GetChaseSpeed(roster.ConsciousCount), Is.EqualTo(MonsterRules.GetChaseSpeed(1)));
        }

        [Test]
        public void LostConnectionEliminatesConsciousAndUnconsciousParticipantsImmediately()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.Register(Second);
            roster.ApplyDamage(Second, 100, 5d);

            Assert.That(roster.Eliminate(First), Is.True);
            Assert.That(roster.Eliminate(Second), Is.True);
            Assert.That(roster.Eliminate(Second), Is.False, "Already eliminated.");
            Assert.That(roster.Eliminate(Third), Is.False, "Unknown participant.");
            roster.TryGet(First, out ParticipantVitality first);
            Assert.That(first.State, Is.EqualTo(VitalityState.Eliminated));
            Assert.That(first.Health, Is.Zero);
            Assert.That(roster.HasUnconsciousParticipants, Is.False);
            Assert.That(roster.AreAllEliminated, Is.True, "Disconnect eliminations count toward defeat.");
            Assert.That(roster.EliminateExpired(100d, null), Is.Zero);
        }

        [Test]
        public void AudioFollowsFirstLivingLocalParticipantElsePlayerOne()
        {
            Assert.That(LocalAudioFocusRule.SelectSlot(
                new[] { true, true, false, false }, new[] { false, false, false, false }), Is.EqualTo(0));
            Assert.That(LocalAudioFocusRule.SelectSlot(
                new[] { true, true, true, false }, new[] { true, false, false, false }), Is.EqualTo(1));
            Assert.That(LocalAudioFocusRule.SelectSlot(
                new[] { true, true, true, false }, new[] { true, true, false, false }), Is.EqualTo(2));
            Assert.That(LocalAudioFocusRule.SelectSlot(
                new[] { true, true, false, false }, new[] { true, true, false, false }), Is.EqualTo(0),
                "With nobody alive, audio follows Player 1's spectated view.");
        }

        [Test]
        public void SpectatorCyclesLivingTeammatesInIdOrderAndSkipsSelfAndEliminated()
        {
            var participants = new List<ParticipantVitality>
            {
                new ParticipantVitality(Third, 100, VitalityState.Conscious, 0d, 100f),
                new ParticipantVitality(First, 0, VitalityState.Eliminated, 0d, 100f),
                new ParticipantVitality(Second, 0, VitalityState.Unconscious, 20d, 100f),
                new ParticipantVitality(new ParticipantId(4), 0, VitalityState.Eliminated, 0d, 100f)
            };

            Assert.That(SpectatorTargetSelector.Resolve(participants, First, ParticipantId.None), Is.EqualTo(Second),
                "Unconscious teammates are still living.");
            Assert.That(SpectatorTargetSelector.Next(participants, First, Second), Is.EqualTo(Third));
            Assert.That(SpectatorTargetSelector.Next(participants, First, Third), Is.EqualTo(Second), "Cycling wraps.");
            Assert.That(SpectatorTargetSelector.Resolve(participants, First, Third), Is.EqualTo(Third), "A valid target is kept.");
            Assert.That(SpectatorTargetSelector.Resolve(participants, First, new ParticipantId(4)), Is.EqualTo(Second),
                "An eliminated target is replaced.");
        }

        [Test]
        public void SpectatorHasNoTargetWithoutALivingTeammate()
        {
            var participants = new List<ParticipantVitality>
            {
                new ParticipantVitality(First, 0, VitalityState.Eliminated, 0d, 100f),
                new ParticipantVitality(Second, 0, VitalityState.Eliminated, 0d, 100f)
            };

            Assert.That(SpectatorTargetSelector.Resolve(participants, First, Second).IsValid, Is.False);
            Assert.That(SpectatorTargetSelector.Next(participants, First, ParticipantId.None).IsValid, Is.False);
        }

        [Test]
        public void HudTextShowsHealthCountdownSpectatingAndDefeat()
        {
            Assert.That(VitalityHudText.FormatHealth(70), Is.EqualTo("Health: 70"));
            Assert.That(VitalityHudText.FormatHealth(-5), Is.EqualTo("Health: 0"));

            var conscious = new ParticipantVitality(First, 70, VitalityState.Conscious, 0d, 100f);
            var unconscious = new ParticipantVitality(First, 0, VitalityState.Unconscious, 40d, 100f);
            var eliminated = new ParticipantVitality(First, 0, VitalityState.Eliminated, 0d, 100f);
            Assert.That(VitalityHudText.FormatStatus(conscious, 0d, ParticipantId.None), Is.Empty);
            Assert.That(VitalityHudText.FormatStatus(unconscious, 27.5d, ParticipantId.None), Is.EqualTo("Unconscious — 13s"));
            Assert.That(VitalityHudText.FormatStatus(unconscious, 45d, ParticipantId.None), Is.EqualTo("Unconscious — 0s"));
            Assert.That(VitalityHudText.FormatStatus(eliminated, 0d, Second), Is.EqualTo("Eliminated — spectating P2"));
            Assert.That(VitalityHudText.FormatStatus(eliminated, 0d, ParticipantId.None), Is.EqualTo("Eliminated"));
            Assert.That(VitalityHudText.FormatOutcome(MatchOutcome.None), Is.Empty);
            Assert.That(VitalityHudText.FormatOutcome(MatchOutcome.Defeat), Does.StartWith("DEFEAT"));
        }
    }
}
