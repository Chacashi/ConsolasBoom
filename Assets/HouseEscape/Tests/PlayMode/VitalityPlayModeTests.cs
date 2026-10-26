using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Flashlight;
using HouseEscape.Gameplay.Hiding;
using HouseEscape.Gameplay.Items;
using HouseEscape.Gameplay.Match;
using HouseEscape.Gameplay.Monster;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Infrastructure;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Flashlight;
using HouseEscape.Networking.Generators;
using HouseEscape.Networking.Hiding;
using HouseEscape.Networking.Items;
using HouseEscape.Networking.Match;
using HouseEscape.Networking.Monster;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Scoring;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Flashlight;
using HouseEscape.Presentation.Generators;
using HouseEscape.Presentation.Hiding;
using HouseEscape.Presentation.Items;
using HouseEscape.Presentation.Vitality;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    /// <summary>
    /// Host-side NGO integration of participant health, unconsciousness, elimination, spectating
    /// and total-elimination defeat in the composed Game scene. The real timeline drives match
    /// activity (it starts with lights off for 80-90 s); the monster is pointed at a controllable
    /// lights-on source where attacks are needed. Elimination tests wait the real 30 s window.
    /// </summary>
    public sealed class VitalityPlayModeTests : InputTestFixture
    {
        private static readonly MatchTimelineSnapshot LightsOn =
            new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1_000_000d, 1_000_000d, 300u);
        private static readonly MatchTimelineSnapshot LightsOff =
            new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1_000_000d, 1_000_000d, 301u);

        private readonly List<MonsterAttack> _attacks = new List<MonsterAttack>();
        private readonly List<Participant> _participants = new List<Participant>();
        private NetworkMonster _monster;
        private NetworkParticipantVitality _vitality;
        private NetworkParticipantAvatarSpawner _spawner;
        private NetworkMatchTimeline _timeline;
        private FakeMatchTimelineSource _matchState;

        [UnityTearDown]
        public IEnumerator TearDownNetworkScene()
        {
            if (_monster != null)
            {
                _monster.AttackPerformedOnServer -= _attacks.Add;
            }

            _attacks.Clear();
            _participants.Clear();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
            }

            yield return null;

            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
                yield return null;
            }

            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                Scene temporaryScene = SceneManager.CreateScene("Vitality test cleanup");
                SceneManager.SetActiveScene(temporaryScene);
                yield return SceneManager.UnloadSceneAsync(gameScene);
            }
        }

        [UnityTest]
        public IEnumerator MonsterAttacksKnockOutThenEliminationDespawnsAndSoloMatchEndsInDefeat()
        {
            yield return StartMatch(1);
            Participant solo = _participants[0];
            ParticipantId id = solo.Context.ParticipantId;
            ParticipantVitalityHudView hud = FindHud(id);
            Assert.That(hud.HealthText, Is.EqualTo("Health: 100"));

            UseControllableMonsterLights(LightsOff);
            _monster.WarpOnServer(solo.Avatar.transform.position + new Vector3(1f, 0f, 0f));
            yield return null;
            _matchState.SetSnapshot(LightsOn);

            yield return WaitUntil(() => _attacks.Count >= 1, 3f);
            Assert.That(Vitality(id).Health, Is.EqualTo(VitalityRules.MaximumHealth - MonsterRules.AttackDamage));
            yield return null;
            Assert.That(hud.HealthText, Is.EqualTo("Health: 70"));

            yield return WaitUntil(() => Vitality(id).State == VitalityState.Unconscious, 12f);
            Assert.That(Vitality(id).State, Is.EqualTo(VitalityState.Unconscious));
            Assert.That(Vitality(id).Health, Is.Zero);
            Assert.That(_attacks, Has.Count.EqualTo(4), "Four 30-damage attacks reach zero health.");
            Assert.That(solo.Avatar.IsIncapacitated, Is.True);
            Assert.That(solo.Avatar.IsMonsterTargetable, Is.False);
            yield return null;
            yield return null;
            Assert.That(_monster.ServerTargetId.IsValid, Is.False, "The monster never targets unconscious players.");
            StringAssert.StartsWith("Unconscious — ", hud.StatusText);

            double deadline = Vitality(id).RecoveryDeadline;
            Assert.That(deadline - _attacks[3].PerformedAt, Is.EqualTo(VitalityRules.RecoveryWindowSeconds).Within(0.001d));
            yield return WaitServerSeconds(2.5d);
            Assert.That(_attacks, Has.Count.EqualTo(4), "No further attacks on an unconscious player.");

            yield return WaitUntil(() => _spawner.SpawnedAvatars.Count == 0, 35f);
            Assert.That(_spawner.SpawnedAvatars, Is.Empty, "The eliminated participant's avatar is removed.");
            Assert.That(_vitality.SynchronizedTime, Is.GreaterThanOrEqualTo(deadline));
            Assert.That(Vitality(id).State, Is.EqualTo(VitalityState.Eliminated));

            MatchTimelineSnapshot snapshot = _timeline.Snapshot;
            Assert.That(snapshot.IsComplete, Is.True);
            Assert.That(snapshot.Outcome, Is.EqualTo(MatchOutcome.Defeat));
            Assert.That(MatchTimelineAvailability.IsMatchActive(snapshot, _timeline.SynchronizedTime), Is.False);
            yield return null;
            StringAssert.StartsWith("DEFEAT", hud.OutcomeText);
            Assert.That(hud.StatusText, Is.EqualTo("Eliminated"));

            // Back on the authoritative timeline the defeated match keeps the monster asleep.
            _monster.UseMatchStateSourceOnServer(_timeline);
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Sleeping));
        }

        [UnityTest]
        public IEnumerator EliminatedParticipantSpectatesCyclesMovesAudioAndLostConnectionEndsInDefeatOnce()
        {
            yield return StartMatch(3);
            Participant eliminated = _participants[0];
            Participant second = _participants[1];
            Participant third = _participants[2];
            LocalParticipantSpectatorDriver spectator = FindComponents<LocalParticipantSpectatorDriver>()
                .Single(driver => driver.Context == eliminated.Context);
            LocalParticipantGeneratorDriver generatorDriver = FindComponents<LocalParticipantGeneratorDriver>()
                .Single(driver => driver.Context == eliminated.Context);
            LocalParticipantComposition composition = FindComponents<LocalParticipantComposition>().Single();
            LocalParticipantJoinManager joinManager = FindComponents<LocalParticipantJoinManager>().Single();
            AudioListener listener = FindComponents<AudioListener>().Single(candidate => candidate.enabled);
            ParticipantVitalityHudView hud = FindHud(eliminated.Context.ParticipantId);
            Camera camera = eliminated.Context.ParticipantCamera;
            Assert.That(composition.AudioFocusSlot, Is.EqualTo(0));
            Assert.That(listener.transform.parent, Is.SameAs(camera.transform));

            Assert.That(_vitality.ApplyDamageOnServer(eliminated.Context.ParticipantId, VitalityRules.MaximumHealth),
                Is.EqualTo(VitalityDamageResult.KnockedUnconscious));
            yield return null;
            StringAssert.StartsWith("Unconscious — 30s", hud.StatusText);
            Assert.That(spectator.SpectatedParticipantId.IsValid, Is.False, "Unconscious players do not spectate.");
            Assert.That(composition.AudioFocusSlot, Is.EqualTo(0), "Unconscious players are still alive for audio.");

            yield return WaitUntil(() => _spawner.SpawnedAvatars.Count == 2, 35f);
            yield return null;
            yield return null;
            Assert.That(camera != null, Is.True, "The participant camera survives its avatar.");
            Assert.That(spectator.SpectatedParticipantId, Is.EqualTo(second.Context.ParticipantId));
            Assert.That(camera.transform.parent, Is.SameAs(second.Avatar.ViewAnchor));
            Assert.That(hud.StatusText, Is.EqualTo($"Eliminated — spectating P{second.Context.ParticipantId.Value}"));
            Assert.That(_timeline.Snapshot.IsComplete, Is.False, "Living teammates keep the match running.");
            Assert.That(composition.AudioFocusSlot, Is.EqualTo(1), "Audio moves to the first living local participant.");
            Assert.That(listener.transform.parent, Is.SameAs(second.Context.ParticipantCamera.transform));
            Assert.That(FindComponents<AudioListener>().Count(candidate => candidate.enabled), Is.EqualTo(1));

            double matchStartedAt = _timeline.Snapshot.MatchStartedAt;
            yield return PressSouth(eliminated.Gamepad);
            Assert.That(spectator.SpectatedParticipantId, Is.EqualTo(third.Context.ParticipantId));
            Assert.That(camera.transform.parent, Is.SameAs(third.Avatar.ViewAnchor));
            Assert.That(generatorDriver.RequestedGeneratorNumber, Is.Zero, "An eliminated player's A press requests nothing.");
            Assert.That(joinManager.IsJoiningOpen, Is.False);
            Assert.That(_timeline.Snapshot.MatchStartedAt, Is.EqualTo(matchStartedAt), "A does not restart the match.");
            Assert.That(_spawner.SpawnedAvatars, Has.Count.EqualTo(2), "A does not respawn avatars mid-match.");

            yield return PressSouth(eliminated.Gamepad);
            Assert.That(spectator.SpectatedParticipantId, Is.EqualTo(second.Context.ParticipantId), "Cycling wraps.");

            // Another participant's south button never cycles this view.
            yield return PressSouth(third.Gamepad);
            Assert.That(spectator.SpectatedParticipantId, Is.EqualTo(second.Context.ParticipantId));

            // GDD 7: losing the console connection eliminates every participant it owns at once.
            _vitality.HandleClientDisconnected(NetworkManager.Singleton.LocalClientId);
            Assert.That(_vitality.TryGetVitality(second.Context.ParticipantId, out ParticipantVitality lost), Is.True);
            Assert.That(lost.State, Is.EqualTo(VitalityState.Eliminated));
            Assert.That(_timeline.Snapshot.Outcome, Is.EqualTo(MatchOutcome.Defeat));
            Assert.That(_timeline.Snapshot.IsComplete, Is.True);
            Assert.That(_timeline.EndMatchOnServer(MatchOutcome.Defeat), Is.False, "Defeat is recorded once.");
            yield return null;
            yield return null;
            Assert.That(_spawner.SpawnedAvatars, Is.Empty);
            Assert.That(camera != null, Is.True, "With no living teammate the view is kept.");
            Assert.That(spectator.SpectatedParticipantId.IsValid, Is.False);
            Assert.That(hud.StatusText, Is.EqualTo("Eliminated"));
            StringAssert.StartsWith("DEFEAT", hud.OutcomeText);
            Assert.That(composition.AudioFocusSlot, Is.EqualTo(0), "With nobody alive, audio follows Player 1's view.");
            Assert.That(listener.transform.parent, Is.SameAs(camera.transform));
        }

        [UnityTest]
        public IEnumerator NoEliminationOrDefeatAfterTheMatchCompletesAtSixAm()
        {
            yield return StartMatch(1);
            ParticipantId id = _participants[0].Context.ParticipantId;
            var matchState = new GameObject("Controllable Vitality Match").AddComponent<FakeMatchTimelineSource>();
            matchState.SetSnapshot(LightsOff);
            _vitality.UseMatchStateSourceOnServer(matchState);
            Assert.That(_vitality.ApplyDamageOnServer(id, VitalityRules.MaximumHealth),
                Is.EqualTo(VitalityDamageResult.KnockedUnconscious));

            matchState.SetSnapshot(new MatchTimelineSnapshot(true, true, LightPhase.Off, 0d, 1_000d, 1_000d, 302u));
            Assert.That(_vitality.ApplyDamageOnServer(id, 30), Is.EqualTo(VitalityDamageResult.Ignored));
            yield return WaitServerSeconds(VitalityRules.RecoveryWindowSeconds + 1d);

            Assert.That(Vitality(id).State, Is.EqualTo(VitalityState.Unconscious), "The finished match freezes elimination.");
            Assert.That(_spawner.SpawnedAvatars, Has.Count.EqualTo(1));
            Assert.That(_timeline.Snapshot.Outcome, Is.EqualTo(MatchOutcome.None));
            Assert.That(_timeline.Snapshot.IsComplete, Is.False);
        }

        [UnityTest]
        public IEnumerator FourPlayerSpeedBonusRequiresFourConsciousParticipants()
        {
            yield return StartMatch(4);
            NavMeshAgent agent = _monster.GetComponent<NavMeshAgent>();
            UseControllableMonsterLights(LightsOff);
            _monster.WarpOnServer(_participants[0].Avatar.transform.position + new Vector3(-6f, 0f, 0f));
            yield return null;

            _matchState.SetSnapshot(LightsOn);
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Chasing));
            Assert.That(agent.speed, Is.EqualTo(MonsterRules.GetChaseSpeed(4)).Within(0.0001f));
            Assert.That(MonsterRules.GetChaseSpeed(4), Is.GreaterThan(MonsterRules.GetChaseSpeed(3)));

            ParticipantId target = _monster.ServerTargetId;
            Participant bystander = _participants.First(participant => participant.Context.ParticipantId != target);
            _vitality.ApplyDamageOnServer(bystander.Context.ParticipantId, VitalityRules.MaximumHealth);
            yield return null;
            yield return null;

            Assert.That(_monster.State, Is.EqualTo(MonsterState.Chasing));
            Assert.That(agent.speed, Is.EqualTo(MonsterRules.GetChaseSpeed(3)).Within(0.0001f),
                "With one of four unconscious, the bonus no longer applies.");
        }

        [UnityTest]
        public IEnumerator StartingItemsAreSelectedWithTheDpadAndConsumedWithTheWestButton()
        {
            yield return StartMatch(2);
            UseControllableMonsterLights(LightsOff);
            NetworkParticipantInventory inventory = FindComponents<NetworkParticipantInventory>().Single();
            Participant first = _participants[0];
            Participant second = _participants[1];
            ParticipantId firstId = first.Context.ParticipantId;
            ParticipantId secondId = second.Context.ParticipantId;
            ItemHudView firstHud = FindComponents<ItemHudView>().Single(hud => hud.ParticipantId == firstId);
            yield return null;

            Assert.That(Items(inventory, firstId).Apples, Is.EqualTo(2), "Player 1 starts with two apples.");
            Assert.That(Items(inventory, secondId).FirstAidKits, Is.EqualTo(1), "Player 2 starts with a first-aid kit.");
            Assert.That(firstHud.SelectionText, Is.EqualTo("< Apple x2 >"));

            _vitality.SetEnergyOnServer(firstId, 50f);
            yield return PressWest(first.Gamepad);
            Assert.That(Items(inventory, firstId).Apples, Is.EqualTo(1));
            Assert.That(Vitality(firstId).WholeEnergy, Is.InRange(59, 60), "An apple restores 10 energy.");
            Assert.That(firstHud.SelectionText, Is.EqualTo("< Apple x1 >"));

            Press(first.Gamepad.dpad.right);
            yield return null;
            Release(first.Gamepad.dpad.right);
            yield return null;
            Assert.That(firstHud.SelectionText, Is.EqualTo("< Sandwich x0 >"));
            yield return PressWest(first.Gamepad);
            Assert.That(Items(inventory, firstId).Apples, Is.EqualTo(1), "Using an item type that is not carried does nothing.");

            _vitality.ApplyDamageOnServer(secondId, 70);
            for (int step = 0; step < 3; step++)
            {
                Press(second.Gamepad.dpad.right);
                yield return null;
                Release(second.Gamepad.dpad.right);
                yield return null;
            }

            yield return PressWest(second.Gamepad);
            Assert.That(Vitality(secondId).Health, Is.EqualTo(70), "A first-aid kit restores 40 health.");
            Assert.That(Items(inventory, secondId).FirstAidKits, Is.Zero);

            Assert.That(
                inventory.TryUseItemOnServer(firstId, ItemType.Apple, NetworkManager.ServerClientId + 99),
                Is.EqualTo(ItemUseRequestResult.SenderDoesNotOwnParticipant));
            _vitality.ApplyDamageOnServer(firstId, VitalityRules.MaximumHealth);
            yield return PressWest(first.Gamepad);
            Assert.That(Items(inventory, firstId).Apples, Is.EqualTo(1), "Unconscious players cannot use items.");
            Assert.That(
                inventory.TryUseItemOnServer(firstId, ItemType.Apple, NetworkManager.ServerClientId),
                Is.EqualTo(ItemUseRequestResult.CannotAct));
        }

        [UnityTest]
        public IEnumerator MapItemsSpawnAtMatchStartAndAnAimedSouthPressPicksOneUpForPoints()
        {
            yield return StartMatch(1);
            UseControllableMonsterLights(LightsOff);
            NetworkMapItemField field = FindComponents<NetworkMapItemField>().Single();
            NetworkParticipantInventory inventory = FindComponents<NetworkParticipantInventory>().Single();
            NetworkParticipantScoreboard scoreboard = FindComponents<NetworkParticipantScoreboard>().Single();
            Participant solo = _participants[0];
            ParticipantId id = solo.Context.ParticipantId;
            yield return null;

            Assert.That(field.Items.Count, Is.InRange(22, MapItemRules.MaximumTotalQuantity), "Map items spawn at match start.");
            int spawnedCount = field.Items.Count;

            // A floating energy drink straight ahead of the participant's view.
            Transform view = solo.Avatar.ViewAnchor;
            Vector3 aimPoint = view.position + view.forward * 1.2f;
            NetworkMapItem drink = field.SpawnItemOnServer(ItemType.EnergyDrink, aimPoint - new Vector3(0f, 0.3f, 0f));
            yield return null;
            Assert.That(field.Items.Count, Is.EqualTo(spawnedCount + 1));

            yield return PressSouth(solo.Gamepad);
            Assert.That(Items(inventory, id).EnergyDrinks, Is.EqualTo(1));
            Assert.That(scoreboard.GetScore(id), Is.EqualTo(MapItemRules.PickupScoreAward));
            Assert.That(drink == null || !drink.IsSpawned, Is.True, "A picked-up item despawns.");
            Assert.That(field.Items.Count, Is.EqualTo(spawnedCount));

            NetworkMapItem far = field.SpawnItemOnServer(ItemType.Apple, solo.Avatar.transform.position + new Vector3(0f, 0f, 2.5f));
            yield return null;
            Assert.That(field.TryPickupOnServer(id, far, NetworkManager.ServerClientId), Is.EqualTo(MapItemPickupRequestResult.OutOfRange));
            Assert.That(field.TryPickupOnServer(id, far, NetworkManager.ServerClientId + 99),
                Is.EqualTo(MapItemPickupRequestResult.SenderDoesNotOwnParticipant));

            NetworkMapItem again = field.SpawnItemOnServer(ItemType.Apple, aimPoint - new Vector3(0f, 0.3f, 0f));
            _vitality.ApplyDamageOnServer(id, VitalityRules.MaximumHealth);
            yield return null;
            Assert.That(field.TryPickupOnServer(id, again, NetworkManager.ServerClientId), Is.EqualTo(MapItemPickupRequestResult.CannotAct));
            yield return PressSouth(solo.Gamepad);
            Assert.That(again.IsSpawned, Is.True, "Unconscious players cannot pick up items.");
            Assert.That(scoreboard.GetScore(id), Is.EqualTo(MapItemRules.PickupScoreAward));
        }

        [UnityTest]
        public IEnumerator HoldingSouthOnAnUnconsciousTeammateWithAFirstAidKitRevivesThem()
        {
            yield return StartMatch(2);
            UseControllableMonsterLights(LightsOff);
            NetworkParticipantRevival revival = FindComponents<NetworkParticipantRevival>().Single();
            NetworkParticipantInventory inventory = FindComponents<NetworkParticipantInventory>().Single();
            NetworkParticipantScoreboard scoreboard = FindComponents<NetworkParticipantScoreboard>().Single();
            Participant downed = _participants[0];
            Participant reviver = _participants[1];
            ParticipantId downedId = downed.Context.ParticipantId;
            ParticipantId reviverId = reviver.Context.ParticipantId;
            ulong host = NetworkManager.ServerClientId;
            Assert.That(Items(inventory, reviverId).FirstAidKits, Is.EqualTo(1));

            _vitality.ApplyDamageOnServer(downedId, VitalityRules.MaximumHealth);
            yield return null;
            Assert.That(Vitality(downedId).State, Is.EqualTo(VitalityState.Unconscious));
            Assert.That(revival.TryBeginRevivalOnServer(reviverId, downedId, host), Is.EqualTo(RevivalRequestResult.OutOfRange));
            Assert.That(revival.TryBeginRevivalOnServer(downedId, reviverId, host), Is.EqualTo(RevivalRequestResult.CannotAct));
            Assert.That(revival.TryBeginRevivalOnServer(reviverId, downedId, host + 99),
                Is.EqualTo(RevivalRequestResult.SenderDoesNotOwnParticipant));

            Vector3 downedAim = downed.Avatar.transform.position + Vector3.up * NetworkParticipantRevival.AimHeightMetres;
            yield return SteerAndAim(reviver, downed.Avatar.transform.position, downedAim, NetworkParticipantRevival.AimRadiusMetres);

            // Releasing early cancels; the kit is kept.
            Press(reviver.Gamepad.buttonSouth);
            yield return WaitUntil(() => revival.TryGetRevivalByReviver(reviverId, out _, out _), 2f);
            Assert.That(revival.TryGetRevivalByReviver(reviverId, out ParticipantId target, out _), Is.True);
            Assert.That(target, Is.EqualTo(downedId));
            Assert.That(reviver.Avatar.IsMovementBlocked, Is.True, "The reviver cannot walk while holding.");
            yield return null;
            StringAssert.StartsWith($"Reviving P{downedId.Value}", FindComponents<RevivalHudView>().Single(hud => hud.ParticipantId == reviverId).RevivalText);
            StringAssert.StartsWith("Being revived", FindComponents<RevivalHudView>().Single(hud => hud.ParticipantId == downedId).RevivalText);
            Release(reviver.Gamepad.buttonSouth);
            yield return WaitUntil(() => !revival.TryGetRevivalByReviver(reviverId, out _, out _), 2f);
            Assert.That(reviver.Avatar.IsMovementBlocked, Is.False);
            Assert.That(Items(inventory, reviverId).FirstAidKits, Is.EqualTo(1));

            // Damage to the reviver cancels.
            Press(reviver.Gamepad.buttonSouth);
            yield return WaitUntil(() => revival.TryGetRevivalByReviver(reviverId, out _, out _), 2f);
            _vitality.ApplyDamageOnServer(reviverId, 10);
            Assert.That(revival.TryGetRevivalByReviver(reviverId, out _, out _), Is.False, "Taking damage cancels the revival.");
            Release(reviver.Gamepad.buttonSouth);
            yield return null;
            yield return null;

            // A full ten-second hold revives with 40 health, spends the kit and awards 150 points.
            Press(reviver.Gamepad.buttonSouth);
            yield return WaitUntil(() => revival.TryGetRevivalByReviver(reviverId, out _, out _), 2f);
            Assert.That(revival.TryGetRevivalByReviver(reviverId, out _, out _), Is.True);
            yield return WaitUntil(() => Vitality(downedId).State == VitalityState.Conscious, 12f);
            Release(reviver.Gamepad.buttonSouth);
            Assert.That(Vitality(downedId).State, Is.EqualTo(VitalityState.Conscious));
            Assert.That(Vitality(downedId).Health, Is.EqualTo(VitalityRules.RevivedHealth));
            Assert.That(Items(inventory, reviverId).FirstAidKits, Is.Zero);
            Assert.That(scoreboard.GetScore(reviverId), Is.EqualTo(VitalityRules.RevivalScoreAward));
            Assert.That(downed.Avatar.IsIncapacitated, Is.False);
            Assert.That(reviver.Avatar.IsMovementBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator FlashlightTogglesWithR1DrainsChargesAtAStationAndGoesOutWhenUnconscious()
        {
            yield return StartMatch(1);
            UseControllableMonsterLights(LightsOff);
            NetworkParticipantFlashlights flashlights = FindComponents<NetworkParticipantFlashlights>().Single();
            NetworkParticipantInventory inventory = FindComponents<NetworkParticipantInventory>().Single();
            Participant solo = _participants[0];
            ParticipantId id = solo.Context.ParticipantId;
            FlashlightHudView hud = FindComponents<FlashlightHudView>().Single(view => view.ParticipantId == id);
            Light beam = solo.Avatar.ViewAnchor.GetComponentInChildren<Light>(true);
            yield return null;
            Assert.That(Flashlight(flashlights, id).WholeBattery, Is.EqualTo(100));
            Assert.That(hud.BatteryText, Is.EqualTo("Flashlight: 100%"));
            Assert.That(beam.enabled, Is.False);

            yield return PressShoulder(solo.Gamepad);
            Assert.That(Flashlight(flashlights, id).IsOn, Is.True);
            Assert.That(solo.Avatar.IsFlashlightOn, Is.True);
            Assert.That(beam.enabled, Is.True, "The beam is visible while on.");
            yield return WaitServerSeconds(1.2d);
            Assert.That(Flashlight(flashlights, id).WholeBattery, Is.InRange(93, 95), "5% per second while on.");

            flashlights.SetBatteryOnServer(id, 0.4f);
            yield return WaitUntil(() => !Flashlight(flashlights, id).IsOn, 2f);
            Assert.That(Flashlight(flashlights, id).IsOn, Is.False, "An empty flashlight turns off.");
            Assert.That(beam.enabled, Is.False);
            Assert.That(flashlights.TryToggleOnServer(id, NetworkManager.ServerClientId), Is.EqualTo(FlashlightRequestResult.EmptyBattery));

            inventory.AddItemOnServer(id, ItemType.FlashlightBattery);
            Assert.That(inventory.TryUseItemOnServer(id, ItemType.FlashlightBattery, NetworkManager.ServerClientId),
                Is.EqualTo(ItemUseRequestResult.Used));
            Assert.That(Flashlight(flashlights, id).WholeBattery, Is.EqualTo(50), "A battery item adds 50%.");

            ChargingStation station = flashlights.ChargingStations[0];
            Assert.That(flashlights.TryBeginChargingOnServer(id, 0, NetworkManager.ServerClientId),
                Is.EqualTo(FlashlightRequestResult.OutOfRange));
            yield return SteerAndAim(solo, station.InteractionPosition, station.AimPosition, FlashlightRules.StationAimRadiusMetres);
            Press(solo.Gamepad.buttonSouth);
            yield return WaitUntil(() => flashlights.IsCharging(id), 2f);
            Assert.That(flashlights.IsCharging(id), Is.True);
            Assert.That(solo.Avatar.IsMovementBlocked, Is.True, "The player cannot walk while charging.");
            yield return WaitServerSeconds(2.5d);
            Release(solo.Gamepad.buttonSouth);
            yield return WaitUntil(() => !flashlights.IsCharging(id), 2f);
            int charged = Flashlight(flashlights, id).WholeBattery;
            Assert.That(charged, Is.InRange(54, 57), "About 2.2% per second while holding.");
            Assert.That(solo.Avatar.IsMovementBlocked, Is.False);
            yield return WaitServerSeconds(0.5d);
            Assert.That(Flashlight(flashlights, id).WholeBattery, Is.EqualTo(charged), "Releasing keeps the charge.");

            yield return PressShoulder(solo.Gamepad);
            Assert.That(Flashlight(flashlights, id).IsOn, Is.True);
            _vitality.ApplyDamageOnServer(id, VitalityRules.MaximumHealth);
            yield return null;
            yield return null;
            Assert.That(Flashlight(flashlights, id).IsOn, Is.False, "Unconscious players' flashlights go out.");
            Assert.That(flashlights.TryToggleOnServer(id, NetworkManager.ServerClientId), Is.EqualTo(FlashlightRequestResult.CannotAct));
        }

        [UnityTest]
        public IEnumerator WalkingOutTheOpenFrontDoorEscapesAndTheLastConsciousPlayerOutWinsTheMatch()
        {
            yield return StartMatch(2);
            UseControllableMonsterLights(LightsOff);
            FrontDoorEscape door = FindComponents<FrontDoorEscape>().Single();
            FrontDoorView doorView = FindComponents<FrontDoorView>().Single();
            NetworkGeneratorRace race = FindComponents<NetworkGeneratorRace>().Single();
            NetworkParticipantScoreboard scoreboard = FindComponents<NetworkParticipantScoreboard>().Single();
            Participant runner = _participants[0];
            Participant stayer = _participants[1];
            ParticipantId runnerId = runner.Context.ParticipantId;
            ParticipantId stayerId = stayer.Context.ParticipantId;

            // The door is closed until every generator is active: standing in it does nothing.
            yield return WalkTowards(runner, door.ExitPosition, () => (runner.Avatar.transform.position - door.ExitPosition).magnitude < 1f);
            yield return null;
            Assert.That(Vitality(runnerId).State, Is.EqualTo(VitalityState.Conscious));
            Assert.That(doorView.IsShownOpen, Is.False);

            int beforeGenerators = scoreboard.GetScore(runnerId);
            foreach (NetworkGenerator generator in race.Generators)
            {
                generator.ActivateOnServer();
            }

            yield return WaitUntil(() => Vitality(runnerId).State == VitalityState.Escaped, 2f);
            Assert.That(Vitality(runnerId).State, Is.EqualTo(VitalityState.Escaped));
            Assert.That(doorView.IsShownOpen, Is.True);
            Assert.That(scoreboard.GetScore(runnerId), Is.EqualTo(beforeGenerators + EscapeRules.EscapeScoreAward));
            Assert.That(_spawner.TryGetAvatar(runnerId, out _), Is.False, "An escaped player leaves the house.");
            Assert.That(_timeline.Snapshot.IsComplete, Is.False, "A conscious teammate is still inside.");

            ParticipantVitalityHudView runnerHud = FindHud(runnerId);
            yield return WaitUntil(() => runnerHud.StatusText.StartsWith("Escaped — spectating"), 2f);
            Assert.That(runnerHud.StatusText, Is.EqualTo($"Escaped — spectating P{stayerId.Value}"));

            _vitality.ApplyDamageOnServer(stayerId, VitalityRules.MaximumHealth);
            yield return null;
            yield return null;
            Assert.That(_timeline.Snapshot.IsComplete, Is.True);
            Assert.That(_timeline.Snapshot.Outcome, Is.EqualTo(MatchOutcome.Victory),
                "No conscious player remains inside and one escaped.");
            Assert.That(Vitality(stayerId).State, Is.EqualTo(VitalityState.Unconscious), "Unconscious players do not escape.");
            yield return null;
            StringAssert.StartsWith("VICTORY", runnerHud.OutcomeText);
        }

        [UnityTest]
        public IEnumerator HidingInAWardrobeMakesTheMonsterLoseThePlayerAndAwardsPointsOnLeaving()
        {
            yield return StartMatch(1);
            UseControllableMonsterLights(LightsOff);
            NetworkHidingPlaces places = FindComponents<NetworkHidingPlaces>().Single();
            NetworkParticipantFlashlights flashlights = FindComponents<NetworkParticipantFlashlights>().Single();
            NetworkParticipantScoreboard scoreboard = FindComponents<NetworkParticipantScoreboard>().Single();
            Participant solo = _participants[0];
            ParticipantId id = solo.Context.ParticipantId;
            ulong host = NetworkManager.ServerClientId;
            int wardrobe = Enumerable.Range(0, places.Spots.Count).First(index => places.Spots[index].SpotType == HidingSpotType.Wardrobe);
            HidingSpot spot = places.Spots[wardrobe];
            Assert.That(places.TryBeginOnServer(id, wardrobe, host), Is.EqualTo(HidingRequestResult.OutOfRange));

            yield return SteerAndAim(solo, spot.InteractionPosition, spot.AimPosition, HidingRules.AimRadiusMetres);

            // Hiding with the south button, then leaving with it again.
            Press(solo.Gamepad.buttonSouth);
            yield return WaitUntil(() => Hiding(places, id).Phase == HidingPhase.Hidden, 2f);
            Release(solo.Gamepad.buttonSouth);
            yield return null;
            Assert.That(Hiding(places, id).Phase, Is.EqualTo(HidingPhase.Hidden));
            Assert.That(solo.Avatar.IsHidden, Is.True);
            Assert.That(solo.Avatar.IsMonsterTargetable, Is.False);
            Assert.That(solo.Avatar.IsMovementBlocked, Is.True);
            Assert.That(solo.Avatar.GetComponentsInChildren<Renderer>().All(avatarRenderer => !avatarRenderer.enabled), Is.True,
                "A hidden avatar is not rendered.");
            Assert.That(flashlights.TryToggleOnServer(id, host), Is.EqualTo(FlashlightRequestResult.Busy));
            yield return PressShoulder(solo.Gamepad);
            Assert.That(Flashlight(flashlights, id).IsOn, Is.False, "A hidden player cannot use the flashlight.");
            StringAssert.StartsWith("Hidden (wardrobe)", FindComponents<HidingHudView>().Single(hud => hud.ParticipantId == id).HidingText);

            yield return PressSouth(solo.Gamepad);
            Assert.That(Hiding(places, id).Phase, Is.EqualTo(HidingPhase.Exiting));
            Assert.That(solo.Avatar.IsHidden, Is.False);
            Assert.That(solo.Avatar.IsMovementBlocked, Is.True, "One second of vulnerability after leaving.");
            yield return WaitUntil(() => Hiding(places, id).Phase == HidingPhase.None, 2f);
            Assert.That(solo.Avatar.IsMovementBlocked, Is.False);
            Assert.That(places.TryBeginOnServer(id, wardrobe, host), Is.EqualTo(HidingRequestResult.OnCooldown));
            Assert.That(scoreboard.GetScore(id), Is.Zero, "Hiding without being hunted awards nothing.");

            // Hunted: the monster targets the player, who hides and makes it lose them.
            yield return WaitServerSeconds(HidingRules.CooldownSeconds);
            Vector3 away = solo.Avatar.transform.position - spot.InteractionPosition;
            away.y = 0f;
            Assert.That(NavMesh.SamplePosition(solo.Avatar.transform.position + away.normalized * 4f, out NavMeshHit monsterSpot, 2f, NavMesh.AllAreas), Is.True);
            _monster.WarpOnServer(monsterSpot.position);
            yield return null;
            _matchState.SetSnapshot(LightsOn);
            yield return WaitUntil(() => _monster.ServerTargetId == id, 2f);
            Assert.That(_monster.ServerTargetId, Is.EqualTo(id));
            Assert.That(places.TryBeginOnServer(id, wardrobe, host), Is.EqualTo(HidingRequestResult.Hidden));
            yield return null;
            yield return null;
            Assert.That(_monster.ServerTargetId.IsValid, Is.False, "A hidden player stops being a valid target.");
            Assert.That(places.TryLeaveOnServer(id, host), Is.EqualTo(HidingRequestResult.Left));
            Assert.That(scoreboard.GetScore(id), Is.EqualTo(HidingRules.SuccessScoreAward));
        }

        [UnityTest]
        public IEnumerator HouseFurnitureAndWallsBlockTheServerAvatarWhichStaysOnTheFloor()
        {
            yield return StartMatch(1);
            UseControllableMonsterLights(LightsOff);
            Participant solo = _participants[0];
            Vector3 start = solo.Avatar.transform.position;
            CharacterController body = solo.Avatar.GetComponent<CharacterController>();
            Vector3 bottom = start + body.center + Vector3.down * (body.height * 0.5f - body.radius - 0.05f);
            Vector3 top = start + body.center + Vector3.up * (body.height * 0.5f - body.radius);
            body.enabled = false;
            bool blocked = Physics.CapsuleCast(bottom, top, body.radius, Vector3.forward, out RaycastHit obstacle, 12f);
            body.enabled = true;
            Assert.That(blocked, Is.True, "Dining furniture lies straight ahead of spawn 1.");

            Set(solo.Gamepad.leftStick, Vector2.up);
            yield return WaitServerSeconds(4d);
            Set(solo.Gamepad.leftStick, Vector2.zero);
            yield return null;

            Vector3 end = solo.Avatar.transform.position;
            Assert.That(end.z, Is.LessThan(obstacle.point.z), "The avatar cannot walk through furniture.");
            Assert.That(end.z - start.z, Is.GreaterThan(1f), "It did walk until it hit the obstacle.");
            Assert.That(Mathf.Abs(end.y - start.y), Is.LessThan(0.1f), "It stays on the floor.");
        }

        [UnityTest]
        public IEnumerator RunningDrainsEnergyAndLowEnergyBlocksRunAndChangesTheLocalCamera()
        {
            yield return StartMatch(1);
            UseControllableMonsterLights(LightsOff);
            Participant solo = _participants[0];
            ParticipantId id = solo.Context.ParticipantId;
            ParticipantVitalityHudView hud = FindHud(id);
            Camera camera = solo.Context.ParticipantCamera;
            yield return null;
            Assert.That(Vitality(id).WholeEnergy, Is.GreaterThanOrEqualTo(99), "Only the passive drain applies at the start.");
            Assert.That(hud.EnergyText, Is.EqualTo($"Energy: {Vitality(id).WholeEnergy}"));
            Assert.That(camera.fieldOfView, Is.EqualTo(EnergyCameraEffects.NormalFieldOfView));

            Press(solo.Gamepad.leftStickButton);
            Set(solo.Gamepad.leftStick, new Vector2(0f, 1f));
            yield return WaitUntil(() => solo.Avatar.IsRunningOnServer, 2f);
            Assert.That(solo.Avatar.IsRunningOnServer, Is.True);
            yield return WaitServerSeconds(1.6d);
            Assert.That(Vitality(id).WholeEnergy, Is.LessThanOrEqualTo(97), "Running drains 2% per second.");
            yield return null;
            Assert.That(hud.EnergyText, Is.EqualTo($"Energy: {Vitality(id).WholeEnergy}"));

            _vitality.SetEnergyOnServer(id, 20f);
            yield return null;
            yield return null;
            Assert.That(camera.fieldOfView, Is.EqualTo(EnergyCameraEffects.LowEnergyFieldOfView));

            _vitality.SetEnergyOnServer(id, 5f);
            yield return WaitUntil(() => !solo.Avatar.IsRunningOnServer, 2f);
            Assert.That(solo.Avatar.IsRunningOnServer, Is.False, "Below 10 energy the player cannot run.");
            yield return WaitUntil(() => Quaternion.Angle(camera.transform.localRotation, Quaternion.identity) > 0.01f, 2f);
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.identity), Is.GreaterThan(0.01f),
                "Below 10 energy the local camera sways.");
            Assert.That(camera.fieldOfView, Is.EqualTo(EnergyCameraEffects.NormalFieldOfView));

            Release(solo.Gamepad.leftStickButton);
            Set(solo.Gamepad.leftStick, Vector2.zero);
            _vitality.SetEnergyOnServer(id, 80f);
            yield return null;
            yield return null;
            Assert.That(camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(camera.fieldOfView, Is.EqualTo(EnergyCameraEffects.NormalFieldOfView));
        }

        private ParticipantVitality Vitality(ParticipantId id)
        {
            Assert.That(_vitality.TryGetVitality(id, out ParticipantVitality vitality), Is.True);
            return vitality;
        }

        private void UseControllableMonsterLights(MatchTimelineSnapshot initial)
        {
            _matchState = new GameObject("Controllable Monster Lights").AddComponent<FakeMatchTimelineSource>();
            _matchState.SetSnapshot(initial);
            _monster.UseMatchStateSourceOnServer(_matchState);
        }

        /// <summary>Walks straight towards a ground point until <paramref name="arrived"/> or 15 s pass.</summary>
        /// <summary>
        /// Walks along the house NavMesh path towards a ground point (the house has walls) until
        /// <paramref name="arrived"/> or 30 s pass; the last leg heads straight for the point.
        /// </summary>
        private IEnumerator WalkTowards(Participant participant, Vector3 groundTarget, System.Func<bool> arrived)
        {
            var path = new NavMeshPath();
            Vector3[] corners = System.Array.Empty<Vector3>();
            if (NavMesh.SamplePosition(participant.Avatar.transform.position, out NavMeshHit from, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(groundTarget, out NavMeshHit to, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path))
            {
                corners = path.corners;
            }

            int corner = 1;
            float timeout = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < timeout && participant.Avatar != null && participant.Avatar.IsSpawned && !arrived())
            {
                Vector3 waypoint = corner < corners.Length - 1 ? corners[corner] : groundTarget;
                Vector3 offset = waypoint - participant.Avatar.transform.position;
                offset.y = 0f;
                if (corner < corners.Length - 1 && offset.magnitude < 0.4f)
                {
                    corner++;
                    continue;
                }

                Vector3 local = Quaternion.Inverse(participant.Avatar.transform.rotation) * offset.normalized;
                Set(participant.Gamepad.leftStick, new Vector2(local.x, local.z));
                yield return null;
            }

            Set(participant.Gamepad.leftStick, Vector2.zero);
            Assert.That(arrived(), Is.True, "The participant should reach the target.");
        }

        /// <summary>Walks to within ~1.4 m of a ground point, then turns the view onto an aim point.</summary>
        private IEnumerator SteerAndAim(Participant participant, Vector3 groundTarget, Vector3 aimPoint, float aimRadius)
        {
            yield return WalkTowards(participant, groundTarget, () =>
            {
                Vector3 offset = groundTarget - participant.Avatar.transform.position;
                offset.y = 0f;
                return offset.magnitude <= 1.4f;
            });
            Vector3 remaining = groundTarget - participant.Avatar.transform.position;
            remaining.y = 0f;
            Assert.That(remaining.magnitude, Is.LessThanOrEqualTo(1.5f), "The participant should reach the target.");
            for (int settle = 0; settle < 4 && !IsAimingAt(participant, aimPoint, aimRadius); settle++)
            {
                for (int frame = 0; frame < 300 && !IsAimingAt(participant, aimPoint, aimRadius); frame++)
                {
                    Transform view = participant.Context.ParticipantCamera.transform;
                    Vector3 toTarget = aimPoint - view.position;
                    Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                    float yawError = Vector3.SignedAngle(Vector3.ProjectOnPlane(view.forward, Vector3.up), flatToTarget, Vector3.up);
                    float pitchError = Mathf.Atan2(toTarget.y, flatToTarget.magnitude) * Mathf.Rad2Deg - participant.Avatar.PitchDegrees;
                    Set(participant.Gamepad.rightStick, new Vector2(AimAxis(yawError), AimAxis(pitchError)));
                    yield return null;
                }

                Set(participant.Gamepad.rightStick, Vector2.zero);
                for (int frame = 0; frame < 15; frame++)
                {
                    yield return null;
                }
            }

            Assert.That(IsAimingAt(participant, aimPoint, aimRadius), Is.True, "The participant view should rest on the target.");
        }

        private static bool IsAimingAt(Participant participant, Vector3 aimPoint, float aimRadius)
        {
            Transform view = participant.Context.ParticipantCamera.transform;
            Vector3 toTarget = aimPoint - view.position;
            float along = Vector3.Dot(toTarget, view.forward);
            return along > 0f && (toTarget - view.forward * along).magnitude <= aimRadius * 0.6f;
        }

        private static float AimAxis(float errorDegrees)
        {
            return Mathf.Abs(errorDegrees) < 2f
                ? 0f
                : Mathf.Sign(errorDegrees) * Mathf.Clamp(Mathf.Abs(errorDegrees) / 45f, 0.35f, 0.8f);
        }

        private static ParticipantFlashlight Flashlight(NetworkParticipantFlashlights flashlights, ParticipantId id)
        {
            Assert.That(flashlights.TryGetFlashlight(id, out ParticipantFlashlight flashlight), Is.True);
            return flashlight;
        }

        private static HidingStatus Hiding(NetworkHidingPlaces places, ParticipantId id)
        {
            return places.TryGetHiding(id, out HidingStatus status) ? status : default;
        }

        private IEnumerator PressShoulder(Gamepad gamepad)
        {
            Press(gamepad.rightShoulder);
            yield return null;
            Release(gamepad.rightShoulder);
            yield return null;
            yield return null;
        }

        private static ParticipantItems Items(NetworkParticipantInventory inventory, ParticipantId id)
        {
            Assert.That(inventory.TryGetItems(id, out ParticipantItems items), Is.True);
            return items;
        }

        private IEnumerator PressWest(Gamepad gamepad)
        {
            Press(gamepad.buttonWest);
            yield return null;
            Release(gamepad.buttonWest);
            yield return null;
            yield return null;
        }

        private IEnumerator PressSouth(Gamepad gamepad)
        {
            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;
            yield return null;
        }

        private IEnumerator StartMatch(int participantCount)
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            LocalParticipantJoinManager joinManager = null;
            for (int frame = 0; frame < 120; frame++)
            {
                joinManager = FindComponents<LocalParticipantJoinManager>().FirstOrDefault();
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsHost
                    && joinManager != null
                    && joinManager.IsJoiningOpen)
                {
                    break;
                }

                yield return null;
            }

            NetworkParticipantRegistry registry = FindComponents<NetworkParticipantRegistry>().Single();
            _spawner = FindComponents<NetworkParticipantAvatarSpawner>().Single();
            _monster = FindComponents<NetworkMonster>().Single();
            _vitality = FindComponents<NetworkParticipantVitality>().Single();
            _timeline = FindComponents<NetworkMatchTimeline>().Single();
            Assert.That(_vitality.IsSpawned && _vitality.IsServer, Is.True);

            var gamepads = new Gamepad[participantCount];
            for (int index = 0; index < participantCount; index++)
            {
                gamepads[index] = InputSystem.AddDevice<Gamepad>();
                Press(gamepads[index].startButton);
                Release(gamepads[index].startButton);
                for (int frame = 0; frame < 30 && registry.Count < index + 1; frame++)
                {
                    yield return null;
                }
            }

            Press(gamepads[0].buttonSouth);
            Release(gamepads[0].buttonSouth);
            for (int frame = 0; frame < 120 && _spawner.SpawnedAvatars.Count < participantCount; frame++)
            {
                yield return null;
            }

            Assert.That(_spawner.SpawnedAvatars, Has.Count.EqualTo(participantCount));
            LocalParticipantContext[] contexts = FindComponents<LocalParticipantContext>()
                .Where(context => context.IsReserved)
                .OrderBy(context => context.LocalSlot)
                .ToArray();
            Assert.That(contexts, Has.Length.EqualTo(participantCount));
            for (int index = 0; index < participantCount; index++)
            {
                Assert.That(_spawner.TryGetAvatar(contexts[index].ParticipantId, out NetworkParticipantAvatar avatar), Is.True);
                Assert.That(_vitality.TryGetVitality(contexts[index].ParticipantId, out ParticipantVitality vitality), Is.True);
                Assert.That(vitality.Health, Is.EqualTo(VitalityRules.MaximumHealth));
                _participants.Add(new Participant(gamepads[index], contexts[index], avatar));
            }

            _monster.AttackPerformedOnServer += _attacks.Add;
            yield return null;
        }

        private IEnumerator WaitServerSeconds(double seconds)
        {
            double until = _vitality.SynchronizedTime + seconds;
            float timeout = Time.realtimeSinceStartup + (float)seconds + 3f;
            while (_vitality.SynchronizedTime < until && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static ParticipantVitalityHudView FindHud(ParticipantId id)
        {
            return FindComponents<ParticipantVitalityHudView>().Single(hud => hud.ParticipantId == id);
        }

        private static T[] FindComponents<T>() where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }

        private sealed class Participant
        {
            public Participant(Gamepad gamepad, LocalParticipantContext context, NetworkParticipantAvatar avatar)
            {
                Gamepad = gamepad;
                Context = context;
                Avatar = avatar;
            }

            public Gamepad Gamepad { get; }
            public LocalParticipantContext Context { get; }
            public NetworkParticipantAvatar Avatar { get; }
        }
    }
}
