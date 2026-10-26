using System.Collections.Generic;
using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Match;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Networking.Match;
using HouseEscape.Presentation.Generators;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class EscapeTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);
        private static readonly ParticipantId Second = new ParticipantId(2);

        [Test]
        public void EscapeValuesMatchTheGdd()
        {
            Assert.That(EscapeRules.EscapeScoreAward, Is.EqualTo(500));
            Assert.That(EscapeRules.IsDoorOpen(5, 6), Is.False);
            Assert.That(EscapeRules.IsDoorOpen(6, 6), Is.True);
        }

        [Test]
        public void OnlyConsciousParticipantsEscapeAndEscapeIsPermanent()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.Register(Second);
            roster.ApplyDamage(Second, 100, 0d);

            Assert.That(roster.Escape(Second), Is.False, "Unconscious players cannot walk out.");
            Assert.That(roster.Escape(First), Is.True);
            Assert.That(roster.Escape(First), Is.False);
            Assert.That(roster.Eliminate(First), Is.False, "A disconnect after escaping does not eliminate.");
            Assert.That(roster.TryGet(First, out ParticipantVitality escaped), Is.True);
            Assert.That(escaped.State, Is.EqualTo(VitalityState.Escaped));
            Assert.That(escaped.IsAlive, Is.True);
            Assert.That(escaped.IsInHouse, Is.False);
            Assert.That(escaped.IsConscious, Is.False);
            Assert.That(roster.ConsciousCount, Is.Zero);
        }

        [Test]
        public void VictoryNeedsAnEscapeAndNoConsciousParticipantInside()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.Register(Second);
            Assert.That(roster.IsEscapeComplete, Is.False);

            roster.Escape(First);
            Assert.That(roster.IsEscapeComplete, Is.False, "A conscious teammate is still inside.");

            roster.ApplyDamage(Second, 100, 0d);
            Assert.That(roster.IsEscapeComplete, Is.True, "Unconscious players inside do not hold the victory back.");
            Assert.That(roster.AreAllEliminated, Is.False);

            var solo = new ParticipantVitalityRoster();
            solo.Register(First);
            solo.ApplyDamage(First, 100, 0d);
            Assert.That(solo.IsEscapeComplete, Is.False, "Without an escape there is no victory.");
        }

        [Test]
        public void SpectatorsNeverFollowAnEscapedTeammate()
        {
            var participants = new List<ParticipantVitality>
            {
                new ParticipantVitality(First, 0, VitalityState.Eliminated, 0d, 0f),
                new ParticipantVitality(Second, 100, VitalityState.Escaped, 0d, 50f)
            };

            Assert.That(SpectatorTargetSelector.Resolve(participants, First, ParticipantId.None).IsValid, Is.False);
        }

        [Test]
        public void HudShowsEscapeAndVictory()
        {
            var escaped = new ParticipantVitality(First, 100, VitalityState.Escaped, 0d, 50f);
            Assert.That(VitalityHudText.FormatStatus(escaped, 0d, Second), Is.EqualTo("Escaped — spectating P2"));
            Assert.That(VitalityHudText.FormatStatus(escaped, 0d, ParticipantId.None), Is.EqualTo("Escaped"));
            Assert.That(VitalityHudText.FormatOutcome(MatchOutcome.Victory), Does.StartWith("VICTORY"));
            Assert.That(GeneratorHudText.FormatProgress(6), Does.EndWith("front door open"));
        }

        [Test]
        public void GameSceneComposesTheFrontDoorExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            FrontDoorEscape door = Collect<FrontDoorEscape>(scene).Single();
            var serializedDoor = new SerializedObject(door);
            foreach (string property in new[]
            {
                "exitPoint", "matchTimeline", "generatorRace", "avatarSpawner", "vitality", "scoreboard"
            })
            {
                Assert.That(serializedDoor.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
            }

            FrontDoorView view = Collect<FrontDoorView>(scene).Single();
            var serializedView = new SerializedObject(view);
            Assert.That(serializedView.FindProperty("generatorSource").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedView.FindProperty("closedIndicator").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedView.FindProperty("openIndicator").objectReferenceValue, Is.Not.Null);
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
