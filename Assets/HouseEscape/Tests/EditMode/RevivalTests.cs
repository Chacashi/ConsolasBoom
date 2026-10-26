using System.Collections.Generic;
using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class RevivalTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);
        private static readonly ParticipantId Second = new ParticipantId(2);
        private static readonly ParticipantId Third = new ParticipantId(3);

        [Test]
        public void RevivalValuesMatchTheGdd()
        {
            Assert.That(VitalityRules.RevivalHoldSeconds, Is.EqualTo(10d));
            Assert.That(VitalityRules.RevivedHealth, Is.EqualTo(40));
            Assert.That(VitalityRules.RevivalScoreAward, Is.EqualTo(150));
        }

        [Test]
        public void BeginRequiresAConsciousReviverAndAnUnconsciousTeammateInsideTheWindow()
        {
            var tracker = new RevivalTracker();
            ParticipantVitality reviver = Conscious(First);
            ParticipantVitality downed = Unconscious(Second, 30d);

            Assert.That(tracker.Begin(reviver, Conscious(Second), 0d), Is.EqualTo(RevivalBeginResult.TargetNotUnconscious));
            Assert.That(tracker.Begin(reviver, reviver, 0d), Is.EqualTo(RevivalBeginResult.CannotReviveSelf));
            Assert.That(tracker.Begin(Unconscious(First, 20d), downed, 0d), Is.EqualTo(RevivalBeginResult.ReviverNotConscious));
            Assert.That(tracker.Begin(reviver, downed, 30d), Is.EqualTo(RevivalBeginResult.WindowExpired));
            Assert.That(tracker.Begin(reviver, downed, 5d), Is.EqualTo(RevivalBeginResult.Started));
            Assert.That(tracker.Begin(reviver, downed, 6d), Is.EqualTo(RevivalBeginResult.AlreadyReviving));
            Assert.That(tracker.Cancel(First), Is.True);
            Assert.That(tracker.Cancel(First), Is.False);
        }

        [Test]
        public void HoldCompletesAfterTenSecondsOnly()
        {
            var tracker = new RevivalTracker();
            var due = new List<RevivalAttempt>();
            var vitalities = new List<ParticipantVitality> { Conscious(First), Unconscious(Second, 40d) };
            tracker.Begin(vitalities[0], vitalities[1], 10d);

            tracker.Resolve(19.9d, vitalities, due);
            Assert.That(due, Is.Empty);
            Assert.That(tracker.Attempts, Has.Count.EqualTo(1));

            tracker.Resolve(20d, vitalities, due);
            Assert.That(due, Has.Count.EqualTo(1));
            Assert.That(due[0].TargetId, Is.EqualTo(Second));
            Assert.That(tracker.Attempts, Is.Empty);
        }

        [Test]
        public void AHoldThatFinishesBeforeTheDeadlineCompletesEvenIfResolvedLate()
        {
            var tracker = new RevivalTracker();
            var due = new List<RevivalAttempt>();
            var vitalities = new List<ParticipantVitality> { Conscious(First), Unconscious(Second, 30d) };
            tracker.Begin(vitalities[0], vitalities[1], 20d);

            tracker.Resolve(30.05d, vitalities, due);
            Assert.That(due, Has.Count.EqualTo(1), "Finished at exactly the deadline; the frame came later.");
        }

        [Test]
        public void AHoldThatWouldFinishAfterTheDeadlineIsDroppedWhenTheWindowExpires()
        {
            var tracker = new RevivalTracker();
            var due = new List<RevivalAttempt>();
            var vitalities = new List<ParticipantVitality> { Conscious(First), Unconscious(Second, 30d) };
            tracker.Begin(vitalities[0], vitalities[1], 25d);

            tracker.Resolve(29d, vitalities, due);
            Assert.That(tracker.Attempts, Has.Count.EqualTo(1), "Still holding before the deadline.");
            tracker.Resolve(30d, vitalities, due);
            Assert.That(due, Is.Empty);
            Assert.That(tracker.Attempts, Is.Empty);
        }

        [Test]
        public void HoldsEndWhenTheReviverOrTargetConditionChanges()
        {
            var tracker = new RevivalTracker();
            var due = new List<RevivalAttempt>();
            tracker.Begin(Conscious(First), Unconscious(Second, 40d), 0d);
            tracker.Resolve(1d, new List<ParticipantVitality> { Unconscious(First, 50d), Unconscious(Second, 40d) }, due);
            Assert.That(tracker.Attempts, Is.Empty, "A knocked-out reviver stops reviving.");

            tracker.Begin(Conscious(First), Unconscious(Second, 40d), 0d);
            tracker.Resolve(1d, new List<ParticipantVitality> { Conscious(First), Eliminated(Second) }, due);
            Assert.That(tracker.Attempts, Is.Empty, "An eliminated teammate cannot be revived.");
        }

        [Test]
        public void TheEarliestOfSeveralRevivalsOnOneTeammateWins()
        {
            var tracker = new RevivalTracker();
            var due = new List<RevivalAttempt>();
            var vitalities = new List<ParticipantVitality> { Conscious(First), Conscious(Third), Unconscious(Second, 40d) };
            tracker.Begin(vitalities[1], vitalities[2], 2d);
            tracker.Begin(vitalities[0], vitalities[2], 1d);

            tracker.Resolve(15d, vitalities, due);
            Assert.That(due, Has.Count.EqualTo(1));
            Assert.That(due[0].ReviverId, Is.EqualTo(First));
        }

        [Test]
        public void ReviveRestoresFortyHealthKeepsEnergyAndRespectsTheDeadline()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.SetEnergy(First, 33f);
            Assert.That(roster.Revive(First, 0d), Is.False, "Only unconscious participants can be revived.");
            roster.ApplyDamage(First, 100, 10d);

            Assert.That(roster.Revive(First, 40.5d), Is.False, "Past the 30 s window.");
            Assert.That(roster.Revive(First, 40d), Is.True);
            Assert.That(roster.TryGet(First, out ParticipantVitality vitality), Is.True);
            Assert.That(vitality.State, Is.EqualTo(VitalityState.Conscious));
            Assert.That(vitality.Health, Is.EqualTo(40));
            Assert.That(vitality.Energy, Is.EqualTo(33f));
            Assert.That(vitality.RecoveryDeadline, Is.Zero);
        }

        [Test]
        public void HudTextShowsRevivalProgress()
        {
            Assert.That(RevivalHudText.GetPercent(10d, 10d), Is.Zero);
            Assert.That(RevivalHudText.GetPercent(10d, 14.55d), Is.EqualTo(45));
            Assert.That(RevivalHudText.GetPercent(10d, 30d), Is.EqualTo(100));
            Assert.That(RevivalHudText.FormatReviving(Second, 45), Is.EqualTo("Reviving P2 — 45%"));
            Assert.That(RevivalHudText.FormatBeingRevived(First, 45), Is.EqualTo("Being revived by P1 — 45%"));
        }

        [Test]
        public void RevivalRecordSurvivesNetworkSerialization()
        {
            var original = new NetworkRevivalRecord(2, 3, 123.5d);
            NetworkRevivalRecord copy;
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(original);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out copy);
                }
            }

            Assert.That(copy, Is.EqualTo(original));
        }

        [Test]
        public void GameSceneComposesRevivalDriversAndHudsExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkParticipantRevival revival = Collect<NetworkParticipantRevival>(scene).Single();
            Assert.That(revival.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedRevival = new SerializedObject(revival);
            foreach (string property in new[]
            {
                "participantRegistry", "avatarSpawner", "avatarDirectory", "vitality", "inventory", "scoreboard"
            })
            {
                Assert.That(serializedRevival.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
            }

            LocalParticipantRevivalDriver[] drivers = Collect<LocalParticipantRevivalDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantRevivalDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("revival").objectReferenceValue, Is.SameAs(revival));
            }

            foreach (LocalParticipantInteractionDriver interaction in Collect<LocalParticipantInteractionDriver>(scene))
            {
                Assert.That(new SerializedObject(interaction).FindProperty("revivalDriver").objectReferenceValue, Is.Not.Null);
            }

            RevivalHudView[] huds = Collect<RevivalHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (RevivalHudView hud in huds)
            {
                var serializedHud = new SerializedObject(hud);
                Assert.That(serializedHud.FindProperty("revivalSource").objectReferenceValue, Is.SameAs(revival));
                Assert.That(serializedHud.FindProperty("revivalText").objectReferenceValue, Is.Not.Null);
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("revivalHuds").arraySize, Is.EqualTo(4));
        }

        private static ParticipantVitality Conscious(ParticipantId id)
        {
            return new ParticipantVitality(id, 100, VitalityState.Conscious, 0d, 100f);
        }

        private static ParticipantVitality Unconscious(ParticipantId id, double deadline)
        {
            return new ParticipantVitality(id, 0, VitalityState.Unconscious, deadline, 100f);
        }

        private static ParticipantVitality Eliminated(ParticipantId id)
        {
            return new ParticipantVitality(id, 0, VitalityState.Eliminated, 0d, 100f);
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
