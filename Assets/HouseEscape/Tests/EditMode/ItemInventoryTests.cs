using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Items;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Items;
using HouseEscape.Presentation.Items;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class ItemInventoryTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);
        private static readonly ParticipantId Second = new ParticipantId(2);

        [Test]
        public void ItemValuesMatchTheGdd()
        {
            Assert.That(ItemRules.GetHealthGain(ItemType.Apple), Is.EqualTo(15));
            Assert.That(ItemRules.GetEnergyGain(ItemType.Apple), Is.EqualTo(10f));
            Assert.That(ItemRules.GetHealthGain(ItemType.Sandwich), Is.EqualTo(25));
            Assert.That(ItemRules.GetEnergyGain(ItemType.Sandwich), Is.EqualTo(20f));
            Assert.That(ItemRules.GetHealthGain(ItemType.EnergyDrink), Is.Zero);
            Assert.That(ItemRules.GetEnergyGain(ItemType.EnergyDrink), Is.EqualTo(40f));
            Assert.That(ItemRules.GetHealthGain(ItemType.FirstAidKit), Is.EqualTo(40));
            Assert.That(ItemRules.GetEnergyGain(ItemType.FirstAidKit), Is.Zero);
            Assert.That(ItemRules.GetBatteryGain(ItemType.FlashlightBattery), Is.EqualTo(50f));
            Assert.That(ItemRules.GetBatteryGain(ItemType.Apple), Is.Zero);
            Assert.That(ItemRules.IsUsable(ItemType.FlashlightBattery), Is.True);
            Assert.That(ItemRules.IsUsable(ItemType.None), Is.False);
        }

        [TestCase(0, ItemType.Apple, 2)]
        [TestCase(1, ItemType.FirstAidKit, 1)]
        [TestCase(2, ItemType.FlashlightBattery, 1)]
        [TestCase(3, ItemType.Sandwich, 1)]
        public void SpawnSlotsStartWithTheirSupplies(int slot, ItemType type, int count)
        {
            var roster = new ItemInventoryRoster();
            Assert.That(roster.Register(First, slot), Is.True);
            Assert.That(roster.TryGet(First, out ParticipantItems items), Is.True);
            int total = items.Apples + items.Sandwiches + items.EnergyDrinks + items.FirstAidKits + items.FlashlightBatteries;
            Assert.That(items.GetCount(type), Is.EqualTo(count));
            Assert.That(total, Is.EqualTo(count), "Only the slot's own supplies.");
        }

        [Test]
        public void RegistrationIsOncePerValidParticipant()
        {
            var roster = new ItemInventoryRoster();
            Assert.That(roster.Register(First, 0), Is.True);
            Assert.That(roster.Register(First, 1), Is.False);
            Assert.That(roster.Register(ParticipantId.None, 1), Is.False);
            Assert.That(roster.GetCount(First, ItemType.Apple), Is.EqualTo(2));
        }

        [Test]
        public void UseChecksCountAndUsabilityThenRemovesExactlyOne()
        {
            var roster = new ItemInventoryRoster();
            roster.Register(First, 0);
            roster.Register(Second, 2);

            Assert.That(roster.CanUse(First, ItemType.Apple), Is.EqualTo(ItemUseResult.Used));
            Assert.That(roster.TryRemove(First, ItemType.Apple), Is.True);
            Assert.That(roster.GetCount(First, ItemType.Apple), Is.EqualTo(1));
            Assert.That(roster.TryRemove(First, ItemType.Apple), Is.True);
            Assert.That(roster.CanUse(First, ItemType.Apple), Is.EqualTo(ItemUseResult.NoneLeft));
            Assert.That(roster.TryRemove(First, ItemType.Apple), Is.False);
            Assert.That(roster.GetCount(First, ItemType.Apple), Is.Zero);

            Assert.That(roster.CanUse(Second, ItemType.FlashlightBattery), Is.EqualTo(ItemUseResult.Used));
            Assert.That(roster.CanUse(Second, ItemType.None), Is.EqualTo(ItemUseResult.InvalidItem));
            Assert.That(roster.CanUse(new ParticipantId(9), ItemType.Apple), Is.EqualTo(ItemUseResult.UnknownParticipant));
        }

        [Test]
        public void CountsAreUnlimited()
        {
            var roster = new ItemInventoryRoster();
            roster.Register(First, 3);
            for (int index = 0; index < 300; index++)
            {
                Assert.That(roster.Add(First, ItemType.EnergyDrink), Is.True);
            }

            Assert.That(roster.GetCount(First, ItemType.EnergyDrink), Is.EqualTo(300));
            Assert.That(roster.Add(First, ItemType.None), Is.False);
            Assert.That(roster.Add(Second, ItemType.Apple), Is.False);
        }

        [Test]
        public void SelectionCyclesEveryTypeInBothDirections()
        {
            Assert.That(ItemRules.Cycle(ItemType.Apple, 1), Is.EqualTo(ItemType.Sandwich));
            Assert.That(ItemRules.Cycle(ItemType.FlashlightBattery, 1), Is.EqualTo(ItemType.Apple));
            Assert.That(ItemRules.Cycle(ItemType.Apple, -1), Is.EqualTo(ItemType.FlashlightBattery));
            Assert.That(ItemRules.Cycle(ItemType.None, 1), Is.EqualTo(ItemType.Sandwich));
        }

        [Test]
        public void HealthRestoreIsCappedAndOnlyForConsciousParticipants()
        {
            var roster = new ParticipantVitalityRoster();
            roster.Register(First);
            roster.ApplyDamage(First, 70, 0d);
            Assert.That(roster.RestoreHealth(First, 40), Is.True);
            Assert.That(Health(roster), Is.EqualTo(70));
            Assert.That(roster.RestoreHealth(First, 40), Is.True);
            Assert.That(Health(roster), Is.EqualTo(100));
            Assert.That(roster.RestoreHealth(First, 0), Is.False);

            roster.ApplyDamage(First, 100, 1d);
            Assert.That(roster.RestoreHealth(First, 40), Is.False, "Unconscious players need a teammate's revival.");
            Assert.That(Health(roster), Is.Zero);
        }

        [Test]
        public void HudTextShowsSelectedTypeAndCount()
        {
            Assert.That(ItemHudText.FormatSelection(ItemType.FirstAidKit, 2), Is.EqualTo("< First-aid kit x2 >"));
            Assert.That(ItemHudText.FormatSelection(ItemType.Apple, -1), Is.EqualTo("< Apple x0 >"));
            Assert.That(ItemHudText.FormatSelection(ItemType.None, 1), Is.Empty);
        }

        [Test]
        public void InventoryRecordSurvivesNetworkSerialization()
        {
            var original = new NetworkItemInventoryRecord(new ParticipantItems(new ParticipantId(4), 2, 0, 3, 1, 700));
            NetworkItemInventoryRecord copy;
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(original);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out copy);
                }
            }

            Assert.That(copy, Is.EqualTo(original));
            Assert.That(copy.ToItems(), Is.EqualTo(original.ToItems()));
            Assert.That(copy.ToItems().FlashlightBatteries, Is.EqualTo(700));
        }

        [Test]
        public void GameSceneComposesInventoryHudsAndItemDriversExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkParticipantInventory inventory = Collect<NetworkParticipantInventory>(scene).Single();
            Assert.That(inventory.GetComponent<NetworkObject>(), Is.Not.Null);
            var serializedInventory = new SerializedObject(inventory);
            Assert.That(serializedInventory.FindProperty("participantRegistry").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedInventory.FindProperty("avatarSpawner").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedInventory.FindProperty("vitality").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedInventory.FindProperty("flashlights").objectReferenceValue, Is.Not.Null);

            ItemHudView[] huds = Collect<ItemHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (ItemHudView hud in huds)
            {
                var serializedHud = new SerializedObject(hud);
                Assert.That(serializedHud.FindProperty("inventorySource").objectReferenceValue, Is.SameAs(inventory));
                Assert.That(serializedHud.FindProperty("selectionText").objectReferenceValue, Is.Not.Null);
            }

            LocalParticipantItemDriver[] drivers = Collect<LocalParticipantItemDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantItemDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("inventory").objectReferenceValue, Is.SameAs(inventory));
                Assert.That(serializedDriver.FindProperty("itemHud").objectReferenceValue, Is.Not.Null);
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("itemHuds").arraySize, Is.EqualTo(4));
        }

        private static int Health(ParticipantVitalityRoster roster)
        {
            Assert.That(roster.TryGet(First, out ParticipantVitality vitality), Is.True);
            return vitality.Health;
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
