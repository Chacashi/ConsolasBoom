using System.Collections;
using System.Linq;
using HouseEscape.Infrastructure;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    public sealed class ParticipantAvatarPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator TearDownNetworkScene()
        {
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
                Scene temporaryScene = SceneManager.CreateScene("Avatar test cleanup");
                SceneManager.SetActiveScene(temporaryScene);
                yield return SceneManager.UnloadSceneAsync(gameScene);
            }
        }

        [UnityTest]
        public IEnumerator TwoHostParticipantsSpawnBindAndMoveInIsolationOnlyAfterMatchStart()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            LocalParticipantJoinManager joinManager = null;
            NetworkParticipantRegistry registry = null;
            NetworkMatchTimeline timeline = null;
            NetworkParticipantAvatarSpawner spawner = null;
            for (int frame = 0; frame < 120; frame++)
            {
                joinManager = FindComponent<LocalParticipantJoinManager>();
                registry = FindComponent<NetworkParticipantRegistry>();
                timeline = FindComponent<NetworkMatchTimeline>();
                spawner = FindComponent<NetworkParticipantAvatarSpawner>();
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsHost
                    && joinManager != null
                    && joinManager.IsJoiningOpen)
                {
                    break;
                }

                yield return null;
            }

            Gamepad first = InputSystem.AddDevice<Gamepad>();
            Gamepad second = InputSystem.AddDevice<Gamepad>();
            Press(first.startButton);
            Release(first.startButton);
            yield return null;
            Press(second.startButton);
            Release(second.startButton);
            for (int frame = 0; frame < 30 && registry.Count < 2; frame++)
            {
                yield return null;
            }

            LocalParticipantContext[] contexts = FindComponents<LocalParticipantContext>()
                .Where(context => context.IsReserved)
                .OrderBy(context => context.LocalSlot)
                .ToArray();
            Assert.That(contexts, Has.Length.EqualTo(2));
            Assert.That(spawner.SpawnedAvatars, Is.Empty, "Avatars must not exist or move before match start.");

            Set(first.leftStick, Vector2.up);
            Set(second.rightStick, Vector2.right);
            yield return null;
            Assert.That(contexts[0].Move, Is.EqualTo(Vector2.up));
            Assert.That(contexts[0].Look, Is.EqualTo(Vector2.zero));
            Assert.That(contexts[1].Move, Is.EqualTo(Vector2.zero));
            Assert.That(contexts[1].Look, Is.EqualTo(Vector2.right));

            Press(first.buttonSouth);
            Release(first.buttonSouth);
            for (int frame = 0; frame < 120 && spawner.SpawnedAvatars.Count < 2; frame++)
            {
                yield return null;
            }

            Assert.That(timeline.Snapshot.IsStarted, Is.True);
            Assert.That(spawner.SpawnedAvatars, Has.Count.EqualTo(2));
            NetworkParticipantAvatar[] avatars = spawner.SpawnedAvatars
                .OrderBy(avatar => avatar.ParticipantId.Value)
                .ToArray();
            Assert.That(avatars[0].ParticipantId, Is.Not.EqualTo(avatars[1].ParticipantId));
            Assert.That(avatars[0].NetworkObject.OwnerClientId, Is.EqualTo(NetworkManager.Singleton.LocalClientId));
            Assert.That(avatars[1].NetworkObject.OwnerClientId, Is.EqualTo(NetworkManager.Singleton.LocalClientId));

            LocalParticipantAvatarDriver[] drivers = FindComponents<LocalParticipantAvatarDriver>()
                .Where(driver => driver.Context.IsReserved)
                .OrderBy(driver => driver.Context.LocalSlot)
                .ToArray();
            Assert.That(drivers[0].BoundAvatar.ParticipantId, Is.EqualTo(contexts[0].ParticipantId));
            Assert.That(drivers[1].BoundAvatar.ParticipantId, Is.EqualTo(contexts[1].ParticipantId));
            yield return null;
            Assert.That(contexts[0].ParticipantCamera.transform.position,
                Is.EqualTo(drivers[0].BoundAvatar.ViewAnchor.position));
            Assert.That(contexts[1].ParticipantCamera.transform.position,
                Is.EqualTo(drivers[1].BoundAvatar.ViewAnchor.position));

            Vector3 firstStart = drivers[0].BoundAvatar.transform.position;
            Vector3 secondStart = drivers[1].BoundAvatar.transform.position;
            Set(second.rightStick, Vector2.zero);
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            Assert.That(Vector3.Distance(drivers[0].BoundAvatar.transform.position, firstStart), Is.GreaterThan(0.01f));
            Assert.That(Vector3.Distance(drivers[1].BoundAvatar.transform.position, secondStart), Is.LessThan(0.02f));
            Assert.That(drivers[0].BoundAvatar.transform.position.y, Is.EqualTo(firstStart.y).Within(0.0001f));
        }

        private static T FindComponent<T>() where T : Component
        {
            T[] components = FindComponents<T>();
            return components.Length == 0 ? null : components[0];
        }

        private static T[] FindComponents<T>() where T : Component
        {
            var results = new System.Collections.Generic.List<T>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }
    }
}
