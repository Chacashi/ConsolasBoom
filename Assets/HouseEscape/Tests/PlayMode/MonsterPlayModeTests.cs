using System.Collections;
using System.Collections.Generic;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Monster;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Monster;
using HouseEscape.Networking.Participants;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    /// <summary>
    /// Host-side NGO integration of the server-only monster in the composed Game scene. The
    /// monster is pointed at a controllable match/light source, like the generator race tests.
    /// </summary>
    public sealed class MonsterPlayModeTests : InputTestFixture
    {
        private static readonly MatchTimelineSnapshot LightsOn =
            new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1_000_000d, 1_000_000d, 200u);
        private static readonly MatchTimelineSnapshot LightsOff =
            new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1_000_000d, 1_000_000d, 201u);

        private readonly List<MonsterAttack> _attacks = new List<MonsterAttack>();
        private readonly List<NetworkParticipantAvatar> _avatars = new List<NetworkParticipantAvatar>();
        private NetworkMonster _monster;
        private NetworkParticipantAvatar _avatar;
        private NetworkParticipantAvatarSpawner _spawner;
        private FakeMatchTimelineSource _matchState;

        [UnityTearDown]
        public IEnumerator TearDownNetworkScene()
        {
            if (_monster != null)
            {
                _monster.AttackPerformedOnServer -= _attacks.Add;
            }

            _attacks.Clear();
            _avatars.Clear();
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
                Scene temporaryScene = SceneManager.CreateScene("Monster test cleanup");
                SceneManager.SetActiveScene(temporaryScene);
                yield return SceneManager.UnloadSceneAsync(gameScene);
            }
        }

        [UnityTest]
        public IEnumerator SleepsInPlaceWithLightsOffEvenWithAParticipantInAttackRange()
        {
            yield return StartMatch(LightsOff);
            _monster.WarpOnServer(_avatar.transform.position + new Vector3(1f, 0f, 0f));
            yield return null;
            Vector3 sleepingPosition = _monster.transform.position;

            yield return WaitServerSeconds(2.5d);

            Assert.That(_monster.State, Is.EqualTo(MonsterState.Sleeping));
            Assert.That(Vector3.Distance(_monster.transform.position, sleepingPosition), Is.LessThan(0.001f));
            Assert.That(_attacks, Is.Empty, "A sleeping monster never attacks.");
        }

        [UnityTest]
        public IEnumerator WakesChasesAndAttacksEveryTwoSecondsThenSleepsImmediatelyOnDarkness()
        {
            yield return StartMatch(LightsOff);
            Vector3 start = _avatar.transform.position + new Vector3(6f, 0f, 0f);
            _monster.WarpOnServer(start);
            yield return null;

            _matchState.SetSnapshot(LightsOn);
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Chasing), "Waking with a detected player starts chasing.");
            Assert.That(_monster.ServerTargetId, Is.EqualTo(_avatar.ParticipantId));

            double timeout = _monster.SynchronizedTime + 8d;
            while (_attacks.Count < 2 && _monster.SynchronizedTime < timeout)
            {
                yield return null;
            }

            Assert.That(_attacks, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Vector3.Distance(_monster.transform.position, start), Is.GreaterThan(4f), "The monster moved to the player.");
            Assert.That(_attacks[0].TargetId, Is.EqualTo(_avatar.ParticipantId));
            Assert.That(_attacks[0].Damage, Is.EqualTo(MonsterRules.AttackDamage));
            Assert.That(_attacks[1].PerformedAt - _attacks[0].PerformedAt,
                Is.GreaterThanOrEqualTo(MonsterRules.AttackIntervalSeconds));
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Attacking));

            _matchState.SetSnapshot(LightsOff);
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Sleeping), "Darkness ends hunting immediately.");
            Assert.That(_monster.ServerTargetId.IsValid, Is.False);
            yield return null;
            Vector3 sleepingPosition = _monster.transform.position;
            int attackCount = _attacks.Count;
            yield return WaitServerSeconds(2.5d);
            Assert.That(Vector3.Distance(_monster.transform.position, sleepingPosition), Is.LessThan(0.001f));
            Assert.That(_attacks, Has.Count.EqualTo(attackCount));
        }

        [UnityTest]
        public IEnumerator SearchesByWanderingWhenNoPlayerIsDetectable()
        {
            yield return StartMatch(LightsOff);
            Vector3 start = _avatar.transform.position + new Vector3(4f, 0f, 0f);
            _monster.WarpOnServer(start);
            _avatar.SetMonsterTargetableOnServer(false);

            _matchState.SetSnapshot(LightsOn);
            yield return WaitServerSeconds(1.5d);

            Assert.That(_monster.State, Is.EqualTo(MonsterState.Searching), "Untargetable players are never detected.");
            Assert.That(Vector3.Distance(_monster.transform.position, start), Is.GreaterThan(0.5f), "Searching wanders.");

            // A 10 m wander leg at walk speed takes under ~4 s, so arrival must pick the next point
            // well before the 6 s forced repath.
            double searchingSince = _monster.SynchronizedTime - 1.5d;
            float timeout = Time.realtimeSinceStartup + 6f;
            while (_monster.ServerWanderDestinationCount < 2 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(_monster.ServerWanderDestinationCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(_monster.SynchronizedTime - searchingSince,
                Is.LessThan(NetworkMonster.WanderRepathIntervalSeconds - 0.5d),
                "Reaching a wander point selects the next one before the forced repath.");
            Assert.That(_attacks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnvironmentBlocksLineOfSight()
        {
            yield return StartMatch(LightsOff);
            Vector3 avatarPosition = _avatar.transform.position;
            _monster.WarpOnServer(avatarPosition + new Vector3(5f, 0f, 0f));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = avatarPosition + new Vector3(2.5f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(0.3f, 3f, 30f);

            // Bodies of the monster and of participants on the sight line never block it, and
            // must not hide the wall behind them.
            AddBodyCollider(_monster.transform, avatarPosition + new Vector3(4.4f, 1.3f, 0f));
            AddBodyCollider(_avatar.transform, avatarPosition + new Vector3(0.6f, 1.2f, 0f));
            yield return new WaitForFixedUpdate();

            _matchState.SetSnapshot(LightsOn);
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Searching));

            Object.Destroy(wall);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(_monster.State, Is.EqualTo(MonsterState.Chasing));
        }

        [UnityTest]
        public IEnumerator TwoLocalParticipantsSwitchToNearestAttackTheRightOneAndDropDespawnedTarget()
        {
            yield return StartMatch(LightsOff, 2);
            NetworkParticipantAvatar first = _avatars[0];
            NetworkParticipantAvatar second = _avatars[1];
            Vector3 firstPosition = first.transform.position;
            Vector3 secondPosition = second.transform.position;
            Vector3 outward = (secondPosition - firstPosition).normalized;

            _monster.WarpOnServer(firstPosition - outward * 3f);
            _matchState.SetSnapshot(LightsOn);
            Assert.That(_monster.ServerTargetId, Is.EqualTo(first.ParticipantId));

            _monster.WarpOnServer(secondPosition + outward * 3f);
            yield return null;
            yield return null;
            Assert.That(_monster.ServerTargetId, Is.EqualTo(second.ParticipantId), "Switches to the nearer player.");

            double timeout = _monster.SynchronizedTime + 5d;
            while (_attacks.Count < 1 && _monster.SynchronizedTime < timeout)
            {
                yield return null;
            }

            Assert.That(_attacks, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(_attacks[0].TargetId, Is.EqualTo(second.ParticipantId));

            second.NetworkObject.Despawn();
            yield return null;
            yield return null;
            Assert.That(_spawner.SpawnedAvatars, Has.Count.EqualTo(1), "Despawned avatars leave the spawner list.");
            Assert.That(_monster.ServerTargetId, Is.EqualTo(first.ParticipantId),
                "The despawned target is dropped and the remaining detected player is selected.");
        }

        private static void AddBodyCollider(Transform owner, Vector3 worldPosition)
        {
            var body = new GameObject("Test Body Collider");
            body.transform.SetParent(owner, false);
            body.transform.position = worldPosition;
            body.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.6f, 0.6f);
        }

        private IEnumerator StartMatch(MatchTimelineSnapshot initialSnapshot, int participantCount = 1)
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            LocalParticipantJoinManager joinManager = null;
            for (int frame = 0; frame < 120; frame++)
            {
                joinManager = FindComponent<LocalParticipantJoinManager>();
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsHost
                    && joinManager != null
                    && joinManager.IsJoiningOpen)
                {
                    break;
                }

                yield return null;
            }

            NetworkParticipantRegistry registry = FindComponent<NetworkParticipantRegistry>();
            NetworkParticipantAvatarSpawner spawner = FindComponent<NetworkParticipantAvatarSpawner>();
            _spawner = spawner;
            _monster = FindComponent<NetworkMonster>();
            Assert.That(_monster, Is.Not.Null);
            Assert.That(_monster.IsSpawned && _monster.IsServer, Is.True);

            Gamepad firstGamepad = null;
            for (int index = 0; index < participantCount; index++)
            {
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
                firstGamepad = firstGamepad ?? gamepad;
                Press(gamepad.startButton);
                Release(gamepad.startButton);
                for (int frame = 0; frame < 30 && registry.Count < index + 1; frame++)
                {
                    yield return null;
                }
            }

            Press(firstGamepad.buttonSouth);
            Release(firstGamepad.buttonSouth);
            for (int frame = 0; frame < 120 && spawner.SpawnedAvatars.Count < participantCount; frame++)
            {
                yield return null;
            }

            Assert.That(spawner.SpawnedAvatars, Has.Count.EqualTo(participantCount));
            _avatars.AddRange(spawner.SpawnedAvatars);
            _avatars.Sort((left, right) => left.ParticipantId.Value.CompareTo(right.ParticipantId.Value));
            _avatar = _avatars[0];

            _matchState = new GameObject("Controllable Match State").AddComponent<FakeMatchTimelineSource>();
            _matchState.SetSnapshot(initialSnapshot);
            _monster.UseMatchStateSourceOnServer(_matchState);
            _monster.AttackPerformedOnServer += _attacks.Add;
        }

        private IEnumerator WaitServerSeconds(double seconds)
        {
            double until = _monster.SynchronizedTime + seconds;
            float timeout = Time.realtimeSinceStartup + (float)seconds + 3f;
            while (_monster.SynchronizedTime < until && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static T FindComponent<T>() where T : Component
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
