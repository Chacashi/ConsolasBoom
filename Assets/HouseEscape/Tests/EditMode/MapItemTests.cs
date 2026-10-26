using System;
using System.Collections.Generic;
using System.Linq;
using HouseEscape.Gameplay.Interaction;
using HouseEscape.Gameplay.Items;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Generators;
using HouseEscape.Networking.Items;
using HouseEscape.Networking.Scoring;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Tests.EditMode
{
    public sealed class MapItemTests
    {
        [Test]
        public void PlansApprovedQuantitiesOnDistinctPositionsWithStrongHealingNearGenerators()
        {
            var placements = new List<MapItemPlacement>();
            for (int seed = 0; seed < 200; seed++)
            {
                MapItemPlacementPlanner.Plan(new System.Random(seed), 12, 24, placements);

                for (ItemType type = ItemRules.FirstType; type <= ItemRules.LastType; type++)
                {
                    int count = placements.Count(placement => placement.Type == type);
                    Assert.That(count, Is.InRange(MapItemRules.GetMinimumQuantity(type), MapItemRules.GetMaximumQuantity(type)));
                }

                Assert.That(placements.Select(placement => placement.PositionIndex).Distinct().Count(), Is.EqualTo(placements.Count));
                Assert.That(placements.All(placement => placement.PositionIndex >= 0 && placement.PositionIndex < 36), Is.True);
                Assert.That(
                    placements.Where(placement => MapItemRules.IsStrongHealing(placement.Type)).All(placement => placement.PositionIndex < 12),
                    Is.True,
                    "First-aid kits and sandwiches use near-generator positions.");
            }
        }

        [Test]
        public void QuantitiesMatchTheGdd()
        {
            Assert.That(MapItemRules.GetMinimumQuantity(ItemType.Apple), Is.EqualTo(8));
            Assert.That(MapItemRules.GetMaximumQuantity(ItemType.Apple), Is.EqualTo(10));
            Assert.That(MapItemRules.GetMinimumQuantity(ItemType.Sandwich), Is.EqualTo(5));
            Assert.That(MapItemRules.GetMaximumQuantity(ItemType.Sandwich), Is.EqualTo(6));
            Assert.That(MapItemRules.GetMinimumQuantity(ItemType.EnergyDrink), Is.EqualTo(3));
            Assert.That(MapItemRules.GetMaximumQuantity(ItemType.EnergyDrink), Is.EqualTo(4));
            Assert.That(MapItemRules.GetMinimumQuantity(ItemType.FirstAidKit), Is.EqualTo(2));
            Assert.That(MapItemRules.GetMaximumQuantity(ItemType.FirstAidKit), Is.EqualTo(3));
            Assert.That(MapItemRules.GetMinimumQuantity(ItemType.FlashlightBattery), Is.EqualTo(4));
            Assert.That(MapItemRules.GetMaximumQuantity(ItemType.FlashlightBattery), Is.EqualTo(5));
            Assert.That(MapItemRules.MaximumTotalQuantity, Is.EqualTo(28));
            Assert.That(MapItemRules.PickupScoreAward, Is.EqualTo(25));
        }

        [Test]
        public void PlanningRequiresEnoughPositions()
        {
            var placements = new List<MapItemPlacement>();
            Assert.Throws<ArgumentException>(() => MapItemPlacementPlanner.Plan(new System.Random(1), 6, 21, placements));

            MapItemPlacementPlanner.Plan(new System.Random(1), 0, 28, placements);
            Assert.That(placements.Count, Is.InRange(22, 28), "Without near-generator positions, strong items use general ones.");
        }

        [Test]
        public void AimErrorPrefersTheTargetClosestToTheViewCentre()
        {
            var origin = new NumericsVector3(0f, 1.6f, 0f);
            var forward = new NumericsVector3(0f, 0f, 1f);
            Assert.That(InteractionRules.GetAimError(origin, forward, new NumericsVector3(0f, 1.6f, 1.5f), 0.5f), Is.Zero.Within(1e-5f));
            Assert.That(InteractionRules.GetAimError(origin, forward, new NumericsVector3(0.3f, 1.6f, 1.5f), 0.5f), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(InteractionRules.GetAimError(origin, forward, new NumericsVector3(0f, 1.6f, -1.5f), 0.5f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(InteractionRules.GetAimError(origin, forward, new NumericsVector3(0.2f, 1.6f, 0f), 0.5f), Is.Zero, "Inside the aim volume.");
            Assert.That(InteractionRules.IsAimingAt(origin, forward, new NumericsVector3(0.6f, 1.6f, 1.5f), 0.5f), Is.False);
            Assert.That(InteractionRules.IsWithinRange(NumericsVector3.Zero, new NumericsVector3(2f, 0f, 0f)), Is.True);
            Assert.That(InteractionRules.IsWithinRange(NumericsVector3.Zero, new NumericsVector3(2.01f, 0f, 0f)), Is.False);
        }

        [Test]
        public void GameSceneComposesScoreboardMapItemsAndInteractionDriversExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkParticipantScoreboard scoreboard = Collect<NetworkParticipantScoreboard>(scene).Single();
            Assert.That(scoreboard.GetComponent<NetworkObject>(), Is.Not.Null);
            var race = new SerializedObject(Collect<NetworkGeneratorRace>(scene).Single());
            Assert.That(race.FindProperty("scoreboard").objectReferenceValue, Is.SameAs(scoreboard));

            NetworkMapItemField field = Collect<NetworkMapItemField>(scene).Single();
            Assert.That(field.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedField = new SerializedObject(field);
            foreach (string property in new[]
            {
                "networkManager", "matchTimeline", "participantRegistry", "avatarSpawner", "vitality", "inventory", "itemPrefab"
            })
            {
                Assert.That(serializedField.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
            }

            Assert.That(serializedField.FindProperty("scoreboard").objectReferenceValue, Is.SameAs(scoreboard));
            Assert.That(serializedField.FindProperty("nearGeneratorPositions").arraySize, Is.EqualTo(12));
            Assert.That(
                serializedField.FindProperty("nearGeneratorPositions").arraySize + serializedField.FindProperty("generalPositions").arraySize,
                Is.GreaterThanOrEqualTo(MapItemRules.MaximumTotalQuantity),
                "Enough valid house positions for the largest planned item count.");
            var prefab = (NetworkMapItem)serializedField.FindProperty("itemPrefab").objectReferenceValue;
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedPrefab = new SerializedObject(prefab);
            Assert.That(serializedPrefab.FindProperty("aimPoint").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedPrefab.FindProperty("visual").objectReferenceValue, Is.Not.Null);

            NetworkPrefabsList prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            Assert.That(prefabs.Contains(prefab.gameObject), Is.True, "The map item prefab is a registered network prefab.");

            LocalParticipantInteractionDriver[] drivers = Collect<LocalParticipantInteractionDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantInteractionDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("generatorDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("mapItemField").objectReferenceValue, Is.SameAs(field));
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
