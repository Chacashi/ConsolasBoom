using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Flashlight;
using HouseEscape.Infrastructure;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Flashlight;
using HouseEscape.Presentation.Flashlight;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class FlashlightTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);

        [Test]
        public void FlashlightValuesMatchTheGdd()
        {
            Assert.That(FlashlightRules.MaximumBattery, Is.EqualTo(100f));
            Assert.That(FlashlightRules.DrainPerSecond, Is.EqualTo(5d));
            Assert.That(FlashlightRules.RechargePerSecond, Is.EqualTo(100d / 45d).Within(1e-9));
            Assert.That(FlashlightRules.BatteryItemGain, Is.EqualTo(50f));
            Assert.That(FlashlightRules.BeamRangeMetres, Is.EqualTo(3f));
        }

        [Test]
        public void StartsFullAndOffAndLastsTwentySecondsOfContinuousUse()
        {
            var roster = new FlashlightRoster();
            Assert.That(roster.Register(First), Is.True);
            Assert.That(roster.Register(First), Is.False);
            Assert.That(Flashlight(roster).Battery, Is.EqualTo(100f));
            Assert.That(Flashlight(roster).IsOn, Is.False);

            Assert.That(roster.Drain(First, 1d), Is.False, "An unlit flashlight does not drain.");
            Assert.That(roster.SetOn(First, true), Is.True);
            roster.Drain(First, 19.9d);
            Assert.That(Flashlight(roster).IsOn, Is.True);
            Assert.That(Flashlight(roster).Battery, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(roster.Drain(First, 0.2d), Is.True);
            Assert.That(Flashlight(roster).Battery, Is.Zero);
            Assert.That(Flashlight(roster).IsOn, Is.False, "An empty flashlight turns off.");
            Assert.That(roster.SetOn(First, true), Is.False, "It cannot turn on with an empty battery.");
        }

        [Test]
        public void ChargingFillsInFortyFiveSecondsAndBatteryItemsAddFiftyCapped()
        {
            var roster = new FlashlightRoster();
            roster.Register(First);
            roster.SetBattery(First, 0f);
            roster.Recharge(First, 22.5d);
            Assert.That(Flashlight(roster).Battery, Is.EqualTo(50f).Within(1e-3f));
            roster.Recharge(First, 30d);
            Assert.That(Flashlight(roster).Battery, Is.EqualTo(100f));

            roster.SetBattery(First, 70f);
            Assert.That(roster.AddBattery(First, FlashlightRules.BatteryItemGain), Is.True);
            Assert.That(Flashlight(roster).Battery, Is.EqualTo(100f));
            Assert.That(roster.AddBattery(First, 0f), Is.False);
            Assert.That(roster.AddBattery(new ParticipantId(5), 10f), Is.False);
        }

        [Test]
        public void HudTextShowsBatteryAndState()
        {
            Assert.That(FlashlightHudText.Format(64, false, false), Is.EqualTo("Flashlight: 64%"));
            Assert.That(FlashlightHudText.Format(64, true, false), Is.EqualTo("Flashlight: 64% (on)"));
            Assert.That(FlashlightHudText.Format(64, true, true), Is.EqualTo("Flashlight: 64% — charging"));
            Assert.That(FlashlightHudText.Format(140, false, false), Is.EqualTo("Flashlight: 100%"));
        }

        [Test]
        public void FlashlightRecordSurvivesNetworkSerialization()
        {
            var original = new NetworkFlashlightRecord(new ParticipantFlashlight(new ParticipantId(2), 37.8f, true), true);
            NetworkFlashlightRecord copy;
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(original);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out copy);
                }
            }

            Assert.That(copy, Is.EqualTo(original));
            Assert.That(copy.ToFlashlight().WholeBattery, Is.EqualTo(37));
            Assert.That(copy.IsCharging, Is.True);
        }

        [Test]
        public void ToggleIsBoundToTheRightShoulderButton()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HouseEscape/LocalMultiplayer/LocalParticipants.inputactions");
            InputAction toggle = actions.FindAction($"{LocalParticipantContext.ActionMapName}/{LocalParticipantContext.ToggleFlashlightActionName}", true);
            Assert.That(toggle.bindings.Select(binding => binding.path), Does.Contain("<Gamepad>/rightShoulder"));
        }

        [Test]
        public void GameSceneComposesFlashlightsStationsDriversAndHudsExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkParticipantFlashlights flashlights = Collect<NetworkParticipantFlashlights>(scene).Single();
            Assert.That(flashlights.GetComponent<NetworkObject>(), Is.Not.Null);
            var serialized = new SerializedObject(flashlights);
            Assert.That(serialized.FindProperty("participantRegistry").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("avatarSpawner").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("vitality").objectReferenceValue, Is.Not.Null);
            SerializedProperty stations = serialized.FindProperty("chargingStations");
            Assert.That(stations.arraySize, Is.InRange(3, 4), "GDD 3.2: three or four charging stations.");
            for (int index = 0; index < stations.arraySize; index++)
            {
                var station = (ChargingStation)stations.GetArrayElementAtIndex(index).objectReferenceValue;
                Assert.That(station, Is.Not.Null);
                Assert.That(new SerializedObject(station).FindProperty("aimPoint").objectReferenceValue, Is.Not.Null);
            }

            LocalParticipantFlashlightDriver[] drivers = Collect<LocalParticipantFlashlightDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantFlashlightDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("flashlights").objectReferenceValue, Is.SameAs(flashlights));
                Assert.That(serializedDriver.FindProperty("flashlightHud").objectReferenceValue, Is.Not.Null);
            }

            foreach (LocalParticipantInteractionDriver interaction in Collect<LocalParticipantInteractionDriver>(scene))
            {
                Assert.That(new SerializedObject(interaction).FindProperty("flashlightDriver").objectReferenceValue, Is.Not.Null);
            }

            FlashlightHudView[] huds = Collect<FlashlightHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (FlashlightHudView hud in huds)
            {
                var serializedHud = new SerializedObject(hud);
                Assert.That(serializedHud.FindProperty("flashlightSource").objectReferenceValue, Is.SameAs(flashlights));
                Assert.That(serializedHud.FindProperty("batteryText").objectReferenceValue, Is.Not.Null);
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("flashlightHuds").arraySize, Is.EqualTo(4));
        }

        [Test]
        public void AvatarPrefabCarriesADisabledThreeMetreSpotBeam()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkParticipantAvatar>("Assets/HouseEscape/Networking/Prefabs/NetworkParticipantAvatar.prefab");
            var beam = (Light)new SerializedObject(prefab).FindProperty("flashlightBeam").objectReferenceValue;
            Assert.That(beam, Is.Not.Null);
            Assert.That(beam.type, Is.EqualTo(LightType.Spot));
            Assert.That(beam.range, Is.EqualTo(FlashlightRules.BeamRangeMetres));
            Assert.That(beam.enabled, Is.False);
            Assert.That(beam.transform.IsChildOf(prefab.ViewAnchor), Is.True, "The beam follows the view.");
        }

        private static ParticipantFlashlight Flashlight(FlashlightRoster roster)
        {
            Assert.That(roster.TryGet(First, out ParticipantFlashlight flashlight), Is.True);
            return flashlight;
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
