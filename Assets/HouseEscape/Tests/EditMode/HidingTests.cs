using System.Collections.Generic;
using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Hiding;
using HouseEscape.Gameplay.Match;
using HouseEscape.Infrastructure;
using HouseEscape.Networking.Hiding;
using HouseEscape.Networking.Scoring;
using HouseEscape.Presentation.Hiding;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseEscape.Tests.EditMode
{
    public sealed class HidingTests
    {
        private static readonly ParticipantId First = new ParticipantId(1);
        private static readonly ParticipantId Second = new ParticipantId(2);

        private readonly List<HidingStatus> _becameHidden = new List<HidingStatus>();
        private readonly List<HidingStatus> _forcedOut = new List<HidingStatus>();
        private readonly List<ParticipantId> _recovered = new List<ParticipantId>();

        [Test]
        public void HidingValuesMatchTheGdd()
        {
            Assert.That(HidingRules.GetMaximumSeconds(HidingSpotType.Wardrobe), Is.EqualTo(30d));
            Assert.That(HidingRules.GetMaximumSeconds(HidingSpotType.Bed), Is.EqualTo(20d));
            Assert.That(HidingRules.GetMaximumSeconds(HidingSpotType.Chest), Is.EqualTo(45d));
            Assert.That(HidingRules.GetMaximumSeconds(HidingSpotType.SmallCloset), Is.EqualTo(15d));
            Assert.That(HidingRules.GetEnterHoldSeconds(HidingSpotType.Chest), Is.EqualTo(2d));
            Assert.That(HidingRules.GetEnterHoldSeconds(HidingSpotType.Wardrobe), Is.Zero);
            Assert.That(HidingRules.ExitVulnerabilitySeconds, Is.EqualTo(1d));
            Assert.That(HidingRules.CooldownSeconds, Is.EqualTo(10d));
            Assert.That(HidingRules.SuccessScoreAward, Is.EqualTo(75));
            Assert.That(HidingRules.Protects(HidingSpotType.Bed, true), Is.False);
            Assert.That(HidingRules.Protects(HidingSpotType.Bed, false), Is.True);
            Assert.That(HidingRules.Protects(HidingSpotType.Wardrobe, true), Is.True);
        }

        [Test]
        public void OnePlayerPerHidingPlaceAndOnePlaceAtATime()
        {
            var tracker = new HidingTracker();
            Assert.That(tracker.Begin(First, 0, HidingSpotType.Wardrobe, 0d, false, false), Is.EqualTo(HidingBeginResult.Hidden));
            Assert.That(tracker.IsSpotOccupied(0), Is.True);
            Assert.That(tracker.Begin(Second, 0, HidingSpotType.Wardrobe, 0d, false, false), Is.EqualTo(HidingBeginResult.Occupied));
            Assert.That(tracker.Begin(First, 1, HidingSpotType.Wardrobe, 0d, false, false), Is.EqualTo(HidingBeginResult.Busy));
            Assert.That(tracker.Begin(Second, 1, HidingSpotType.SmallCloset, 0d, false, false), Is.EqualTo(HidingBeginResult.Hidden));
        }

        [Test]
        public void LeavingStartsOneSecondOfVulnerabilityAndATenSecondCooldown()
        {
            var tracker = new HidingTracker();
            tracker.Begin(First, 0, HidingSpotType.Wardrobe, 0d, false, false);
            Assert.That(tracker.Leave(First, 5d, out HidingStatus left), Is.True);
            Assert.That(left.Phase, Is.EqualTo(HidingPhase.Hidden));
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Exiting));
            Assert.That(Status(tracker).BlocksMovement, Is.True);
            Assert.That(tracker.IsSpotOccupied(0), Is.False, "The hiding place is free as soon as the player leaves.");

            Advance(tracker, 5.9d);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Exiting));
            Advance(tracker, 6d);
            Assert.That(_recovered, Does.Contain(First));
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.None));

            Assert.That(tracker.Begin(First, 0, HidingSpotType.Wardrobe, 14.9d, false, false), Is.EqualTo(HidingBeginResult.OnCooldown));
            Assert.That(tracker.Begin(First, 0, HidingSpotType.Wardrobe, 15d, false, false), Is.EqualTo(HidingBeginResult.Hidden));
        }

        [Test]
        public void TheMaximumTimeForcesThePlayerOutAtItsDeadline()
        {
            var tracker = new HidingTracker();
            tracker.Begin(First, 0, HidingSpotType.SmallCloset, 0d, false, false);
            Advance(tracker, 14.9d);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Hidden));

            Advance(tracker, 15.5d);
            Assert.That(_forcedOut.Select(status => status.ParticipantId), Does.Contain(First));
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Exiting));
            Assert.That(Status(tracker).PhaseEndsAt, Is.EqualTo(16d), "Vulnerability starts at the deadline.");
            Assert.That(Status(tracker).CooldownEndsAt, Is.EqualTo(25d));
        }

        [Test]
        public void AChestNeedsTwoSecondsHeldAndReleasingCancels()
        {
            var tracker = new HidingTracker();
            Assert.That(tracker.Begin(First, 2, HidingSpotType.Chest, 0d, false, false), Is.EqualTo(HidingBeginResult.Entering));
            Assert.That(tracker.IsSpotOccupied(2), Is.True);
            Assert.That(tracker.CancelEntering(First), Is.True);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.None));
            Assert.That(tracker.Begin(First, 2, HidingSpotType.Chest, 1d, false, false), Is.EqualTo(HidingBeginResult.Entering),
                "Cancelling an entry starts no cooldown.");

            Advance(tracker, 2.9d, targeted: true);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Entering));
            Advance(tracker, 3d, targeted: true);
            Assert.That(_becameHidden, Has.Count.EqualTo(1));
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Hidden));
            Assert.That(Status(tracker).PhaseEndsAt, Is.EqualTo(48d));
            Assert.That(Status(tracker).LostMonster, Is.True, "Protection and the lost monster are decided on becoming hidden.");
        }

        [Test]
        public void ABedSeenBeingEnteredDoesNotProtectOrAward()
        {
            var tracker = new HidingTracker();
            tracker.Begin(First, 0, HidingSpotType.Bed, 0d, true, true);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.Hidden));
            Assert.That(Status(tracker).IsProtected, Is.False);
            Assert.That(Status(tracker).LostMonster, Is.False);

            tracker.Begin(Second, 1, HidingSpotType.Wardrobe, 0d, true, true);
            Assert.That(tracker.TryGet(Second, out HidingStatus wardrobe), Is.True);
            Assert.That(wardrobe.IsProtected, Is.True);
            Assert.That(wardrobe.LostMonster, Is.True);
        }

        [Test]
        public void RemovingEndsTheUseWithoutVulnerability()
        {
            var tracker = new HidingTracker();
            tracker.Begin(First, 0, HidingSpotType.Wardrobe, 0d, false, false);
            Assert.That(tracker.Remove(First), Is.True);
            Assert.That(Status(tracker).Phase, Is.EqualTo(HidingPhase.None));
            Assert.That(tracker.IsSpotOccupied(0), Is.False);
            Assert.That(tracker.Remove(First), Is.False);
        }

        [Test]
        public void SurvivingALightsOnPhaseMeansReachingLightsOffInARunningMatch()
        {
            var on = new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1200d, 200d, 4u);
            var off = new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1200d, 300d, 5u);
            var ended = new MatchTimelineSnapshot(true, true, LightPhase.On, 0d, 1200d, 200d, 5u, MatchOutcome.Defeat, 150d);
            Assert.That(SurvivalRules.IsLightsOnPhaseSurvived(on, off), Is.True);
            Assert.That(SurvivalRules.IsLightsOnPhaseSurvived(off, on), Is.False);
            Assert.That(SurvivalRules.IsLightsOnPhaseSurvived(on, ended), Is.False);
            Assert.That(SurvivalRules.LightsOnPhaseSurvivedAward, Is.EqualTo(50));
        }

        [Test]
        public void HudShowsEntryHiddenTimeVulnerabilityAndCooldown()
        {
            var entering = new HidingStatus(First, HidingPhase.Entering, 0, HidingSpotType.Chest, 10d, 12d, 0d, false, false);
            var hidden = new HidingStatus(First, HidingPhase.Hidden, 0, HidingSpotType.Wardrobe, 10d, 40d, 0d, true, false);
            var exiting = new HidingStatus(First, HidingPhase.Exiting, -1, HidingSpotType.Wardrobe, 40d, 41d, 50d, false, false);
            var idle = new HidingStatus(First, HidingPhase.None, -1, HidingSpotType.Wardrobe, 0d, 0d, 50d, false, false);
            Assert.That(HidingHudText.Format(entering, 11d), Is.EqualTo("Getting into the chest — 50%"));
            Assert.That(HidingHudText.Format(hidden, 16.5d), Is.EqualTo("Hidden (wardrobe) — 24s — A / CROSS to leave"));
            Assert.That(HidingHudText.Format(exiting, 40.5d), Does.StartWith("Leaving"));
            Assert.That(HidingHudText.Format(idle, 43.2d), Is.EqualTo("Hide again in 7s"));
            Assert.That(HidingHudText.Format(idle, 50d), Is.Empty);
        }

        [Test]
        public void HidingRecordSurvivesNetworkSerialization()
        {
            var original = new NetworkHidingRecord(
                new HidingStatus(Second, HidingPhase.Hidden, 3, HidingSpotType.Bed, 12.5d, 32.5d, 4d, true, true));
            NetworkHidingRecord copy;
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(original);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out copy);
                }
            }

            Assert.That(copy, Is.EqualTo(original));
            HidingStatus status = copy.ToStatus();
            Assert.That(status.SpotType, Is.EqualTo(HidingSpotType.Bed));
            Assert.That(status.SpotIndex, Is.EqualTo(3));
            Assert.That(status.LostMonster, Is.False, "Server-only detail.");
        }

        [Test]
        public void GameSceneComposesHidingPlacesDriversHudsAndSurvivalScoringExplicitly()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            NetworkHidingPlaces places = Collect<NetworkHidingPlaces>(scene).Single();
            Assert.That(places.GetComponent<NetworkObject>(), Is.Not.Null);
            var serialized = new SerializedObject(places);
            foreach (string property in new[] { "participantRegistry", "avatarSpawner", "vitality", "monster", "scoreboard" })
            {
                Assert.That(serialized.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
            }

            HidingSpot[] spots = Enumerable.Range(0, serialized.FindProperty("spots").arraySize)
                .Select(index => (HidingSpot)serialized.FindProperty("spots").GetArrayElementAtIndex(index).objectReferenceValue)
                .ToArray();
            Assert.That(spots.All(spot => spot != null), Is.True);
            Assert.That(spots.Count(spot => spot.SpotType == HidingSpotType.Wardrobe), Is.InRange(4, 5));
            Assert.That(spots.Count(spot => spot.SpotType == HidingSpotType.Bed), Is.InRange(3, 4));
            Assert.That(spots.Count(spot => spot.SpotType == HidingSpotType.Chest), Is.InRange(2, 3));
            Assert.That(spots.Count(spot => spot.SpotType == HidingSpotType.SmallCloset), Is.InRange(2, 3));

            LocalParticipantHidingDriver[] drivers = Collect<LocalParticipantHidingDriver>(scene);
            Assert.That(drivers, Has.Length.EqualTo(4));
            foreach (LocalParticipantHidingDriver driver in drivers)
            {
                var serializedDriver = new SerializedObject(driver);
                Assert.That(serializedDriver.FindProperty("context").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("avatarDriver").objectReferenceValue, Is.Not.Null);
                Assert.That(serializedDriver.FindProperty("hidingPlaces").objectReferenceValue, Is.SameAs(places));
            }

            foreach (LocalParticipantInteractionDriver interaction in Collect<LocalParticipantInteractionDriver>(scene))
            {
                Assert.That(new SerializedObject(interaction).FindProperty("hidingDriver").objectReferenceValue, Is.Not.Null);
            }

            HidingHudView[] huds = Collect<HidingHudView>(scene);
            Assert.That(huds, Has.Length.EqualTo(4));
            foreach (HidingHudView hud in huds)
            {
                var serializedHud = new SerializedObject(hud);
                Assert.That(serializedHud.FindProperty("hidingSource").objectReferenceValue, Is.SameAs(places));
                Assert.That(serializedHud.FindProperty("hidingText").objectReferenceValue, Is.Not.Null);
            }

            var composition = new SerializedObject(Collect<LocalParticipantComposition>(scene).Single());
            Assert.That(composition.FindProperty("hidingHuds").arraySize, Is.EqualTo(4));

            var survival = new SerializedObject(Collect<LightsOnSurvivalScoring>(scene).Single());
            Assert.That(survival.FindProperty("matchTimeline").objectReferenceValue, Is.Not.Null);
            Assert.That(survival.FindProperty("vitality").objectReferenceValue, Is.Not.Null);
            Assert.That(survival.FindProperty("scoreboard").objectReferenceValue, Is.Not.Null);
        }

        private void Advance(HidingTracker tracker, double now, bool seen = false, bool targeted = false)
        {
            tracker.Advance(now, _ => seen, _ => targeted, _becameHidden, _forcedOut, _recovered);
        }

        private static HidingStatus Status(HidingTracker tracker)
        {
            Assert.That(tracker.TryGet(First, out HidingStatus status), Is.True);
            return status;
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
