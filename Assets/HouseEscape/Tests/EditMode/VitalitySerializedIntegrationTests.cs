using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class VitalitySerializedIntegrationTests
    {
        [Test]
        public void GameSceneComposesVitalityHudsAndSpectatorDriversExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkParticipantVitality vitality = Collect<NetworkParticipantVitality>(scene).Single();
            Assert.That(vitality.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedVitality = new SerializedObject(vitality);
            Assert.That(serializedVitality.FindProperty("matchTimeline").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVitality.FindProperty("avatarSpawner").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVitality.FindProperty("monster").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVitality.FindProperty("participantRegistry").objectReferenceValue, Is.Not.Null);

            NetworkParticipantAvatarDirectory directory = Collect<NetworkParticipantAvatarDirectory>(scene).Single();
            var serializedDirectory = new SerializedObject(directory);
            Assert.That(serializedDirectory.FindProperty("networkManager").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedDirectory.FindProperty("avatarPrefab").objectReferenceValue, Is.Not.Null);
            var spawner = new SerializedObject(Collect<NetworkParticipantAvatarSpawner>(scene).Single());
            Assert.That(spawner.FindProperty("avatarDirectory").objectReferenceValue, Is.SameAs(directory));

            ParticipantVitalityHudView[] huds = Collect<ParticipantVitalityHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (ParticipantVitalityHudView hud in huds)
            {
                var serializedHud = new SerializedObject(hud);
                Assert.That(serializedHud.FindProperty("vitalitySource").objectReferenceValue, Is.SameAs(vitality));
                Assert.That(serializedHud.FindProperty("healthText").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedHud.FindProperty("energyText").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedHud.FindProperty("statusText").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedHud.FindProperty("outcomeText").objectReferenceValue, Is.Not.Null);
            }

            LocalParticipantSpectatorDriver[] spectators = Collect<LocalParticipantSpectatorDriver>(scene);
            Assert.That(spectators, Has.Length.EqualTo(4));
            foreach (LocalParticipantSpectatorDriver spectator in spectators)
            {
                var serializedSpectator = new SerializedObject(spectator);
                Assert.That(serializedSpectator.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedSpectator.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedSpectator.FindProperty("vitality").objectReferenceValue, Is.SameAs(vitality));
                Assert.That(serializedSpectator.FindProperty("avatarDirectory").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedSpectator.FindProperty("vitalityHud").objectReferenceValue, Is.Not.Null);
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("vitalityHuds").arraySize, Is.EqualTo(4));
            Assert.That(composition.FindProperty("avatarDirectory").objectReferenceValue, Is.SameAs(directory));
            Assert.That(composition.FindProperty("vitality").objectReferenceValue, Is.SameAs(vitality));
        }

        [Test]
        public void VitalityRecordSurvivesNetworkSerialization()
        {
            var original = new NetworkParticipantVitalityRecord(
                new ParticipantVitality(new ParticipantId(3), 0, VitalityState.Unconscious, 123.25d, 37f));

            NetworkParticipantVitalityRecord copy = RoundTrip(original);

            Assert.That(copy, Is.EqualTo(original));
            Assert.That(copy.ToVitality(), Is.EqualTo(original.ToVitality()));
        }

        [Test]
        public void TimelineSnapshotWithOutcomeAndCompletionSurvivesNetworkSerialization()
        {
            var core = new MatchTimelineSnapshot(true, true, LightPhase.On, 5d, 1205d, 300d, 9u, MatchOutcome.Defeat, 250.5d);
            var original = new NetworkMatchTimelineSnapshot(core);

            NetworkMatchTimelineSnapshot copy = RoundTrip(original);

            Assert.That(copy, Is.EqualTo(original));
            Assert.That(copy.ToCoreSnapshot(), Is.EqualTo(core));
            Assert.That(copy.ToCoreSnapshot().Outcome, Is.EqualTo(MatchOutcome.Defeat));
            Assert.That(copy.ToCoreSnapshot().CompletedAt, Is.EqualTo(250.5d));
        }

        private static T RoundTrip<T>(T value) where T : struct, INetworkSerializable
        {
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(value);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out T copy);
                    return copy;
                }
            }
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
