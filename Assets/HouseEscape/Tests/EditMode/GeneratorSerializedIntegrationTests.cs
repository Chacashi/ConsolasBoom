using System.Linq;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Generators;
using HouseEscape.Presentation.Generators;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class GeneratorSerializedIntegrationTests
    {
        private const string GeneratorPrefabPath = "Assets/HouseEscape/Networking/Prefabs/NetworkGenerator.prefab";

        [Test]
        public void GeneratorPrefabIsANetworkObjectWithAimPointAndIsRegistered()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>().PrefabIdHash, Is.Not.Zero);
            Assert.That(prefab.GetComponent<NetworkGenerator>().AimPoint, Is.Not.Null);

            NetworkPrefabsList prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            Assert.That(prefabs.Contains(prefab), Is.True);
        }

        [Test]
        public void GameSceneComposesSixUniqueGeneratorsRaceDriversAndHuds()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkGenerator[] generators = Collect<NetworkGenerator>(scene);
            Assert.That(generators, Has.Length.EqualTo(6));
            Assert.That(generators.Select(generator => generator.GeneratorNumber).OrderBy(number => number),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            NetworkObject[] sceneNetworkObjects = Collect<NetworkObject>(scene);
            Assert.That(sceneNetworkObjects.Length, Is.GreaterThanOrEqualTo(9),
                "Timeline, registry, generator race and six generators are in-scene NetworkObjects.");
            uint[] hashes = sceneNetworkObjects.Select(networkObject => networkObject.PrefabIdHash).ToArray();
            Assert.That(hashes, Has.None.EqualTo(0u));
            Assert.That(hashes.Distinct().Count(), Is.EqualTo(hashes.Length),
                "Every in-scene NetworkObject needs a unique GlobalObjectIdHash.");

            NetworkGeneratorRace race = Collect<NetworkGeneratorRace>(scene).Single();
            Assert.That(race.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedRace = new SerializedObject(race);
            Assert.That(serializedRace.FindProperty("participantRegistry").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedRace.FindProperty("matchTimeline").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedRace.FindProperty("avatarSpawner").objectReferenceValue, Is.Not.Null);
            Assert.That(race.Generators, Is.EquivalentTo(generators));

            LocalParticipantGeneratorDriver[] drivers = Collect<LocalParticipantGeneratorDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantGeneratorDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("generatorRace").objectReferenceValue, Is.EqualTo(race));
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
            }

            GeneratorHudView[] huds = Collect<GeneratorHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (GeneratorHudView hud in huds)
            {
                Assert.That(new SerializedObject(hud).FindProperty("raceSource").objectReferenceValue, Is.EqualTo(race));
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("generatorHuds").arraySize, Is.EqualTo(4));
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
