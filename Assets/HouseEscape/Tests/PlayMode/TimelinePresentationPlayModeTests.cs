using System.Collections;
using System.Linq;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using HouseEscape.Presentation.Timeline;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    public sealed class TimelinePresentationPlayModeTests : InputTestFixture
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
                Scene temporaryScene = SceneManager.CreateScene("Timeline test cleanup");
                SceneManager.SetActiveScene(temporaryScene);
                yield return SceneManager.UnloadSceneAsync(gameScene);
            }
        }

        [UnityTest]
        public IEnumerator ConfiguredGameSceneStartsAuthoritativeHostTimeline()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            NetworkMatchTimeline timeline = null;
            NetworkParticipantRegistry registry = null;
            LocalParticipantJoinManager joinManager = null;
            const int maximumFrames = 120;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                timeline = FindComponentInActiveScene<NetworkMatchTimeline>();
                registry = FindComponentInActiveScene<NetworkParticipantRegistry>();
                joinManager = FindComponentInActiveScene<LocalParticipantJoinManager>();
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsListening
                    && timeline != null
                    && timeline.IsSpawned
                    && registry != null
                    && registry.IsSpawned
                    && joinManager != null
                    && joinManager.IsJoiningOpen)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(NetworkManager.Singleton, Is.Not.Null);
            Assert.That(NetworkManager.Singleton.IsHost, Is.True);
            Assert.That(timeline, Is.Not.Null);
            Assert.That(timeline.IsSpawned, Is.True);
            Assert.That(timeline.Snapshot.IsStarted, Is.False);
            Assert.That(FindComponentsInActiveScene<Camera>().Count(camera => camera.enabled), Is.EqualTo(1));
            Assert.That(
                FindComponentsInActiveScene<TMP_Text>(),
                Has.Some.Matches<TMP_Text>(label => label.text == "PRESS MENU TO JOIN"));

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.startButton);
            Release(gamepad.startButton);
            for (int frame = 0; frame < maximumFrames && registry.Count == 0; frame++)
            {
                yield return null;
            }

            Assert.That(joinManager.ReservedCount, Is.EqualTo(1));
            Assert.That(registry.Count, Is.EqualTo(1));
            LocalParticipantContext joinedContext = FindComponentInActiveScene<LocalParticipantContext>();
            Assert.That(joinedContext, Is.Not.Null);
            Assert.That(joinedContext.gameObject.activeInHierarchy, Is.True);
            Assert.That(joinedContext.PlayerInput.enabled, Is.True);
            Assert.That(joinedContext.PlayerInput.currentActionMap.name, Is.EqualTo(LocalParticipantContext.ActionMapName));
            Assert.That(joinedContext.ParticipantCamera.enabled, Is.True);
            Assert.That(joinedContext.ParticipantCanvas.enabled, Is.True);
            Press(gamepad.buttonSouth);
            Release(gamepad.buttonSouth);
            for (int frame = 0; frame < maximumFrames && !timeline.Snapshot.IsStarted; frame++)
            {
                yield return null;
            }

            Assert.That(joinManager.IsJoiningOpen, Is.False);
            Assert.That(timeline.Snapshot.IsStarted, Is.True);
            Assert.That(FindComponentsInActiveScene<Camera>().Count(camera => camera.enabled), Is.EqualTo(1));
            Assert.That(
                FindComponentsInActiveScene<Canvas>().Any(canvas => canvas.name == "Joining HUD" && canvas.enabled),
                Is.False);
            Assert.That(
                registry.TryRegisterParticipantOnServer(NetworkManager.Singleton.LocalClientId, 1, out _),
                Is.EqualTo(ParticipantRegistrationResult.RegistrationClosed));
            Assert.That(timeline.Snapshot.LightPhase, Is.EqualTo(LightPhase.Off));
            Assert.That(
                timeline.Snapshot.MatchDeadline - timeline.Snapshot.MatchStartedAt,
                Is.EqualTo(MatchClock.MatchDurationSeconds).Within(0.001d));

            MatchTimelinePresentationSource presentation =
                FindComponentInActiveScene<MatchTimelinePresentationSource>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Current.IsAvailable, Is.True);
            Assert.That(presentation.Current.MatchRemainingSeconds, Is.GreaterThan(1190d));

            Light sceneLight = FindComponentInActiveScene<Light>();
            Assert.That(sceneLight, Is.Not.Null);
            Assert.That(sceneLight.enabled, Is.False);

            TMP_Text[] labels = FindComponentsInActiveScene<TMP_Text>();
            Assert.That(labels, Has.Some.Matches<TMP_Text>(label => label.text == "LIGHTS OFF"));
            Assert.That(labels, Has.Some.Matches<TMP_Text>(label => label.text == "Match: 20:00"));
            Assert.That(FindComponentsInActiveScene<AudioListener>().Count(listener => listener.enabled), Is.EqualTo(1));
            Assert.That(FindComponentsInActiveScene<AudioSource>(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplicitSourceDrivesRigAndRaisesWarningOnce()
        {
            var root = new GameObject("Timeline presentation test");
            root.SetActive(false);
            FakeMatchTimelineSource fake = root.AddComponent<FakeMatchTimelineSource>();
            MatchTimelinePresentationSource presentation = root.AddComponent<MatchTimelinePresentationSource>();
            HouseLightRigView rig = root.AddComponent<HouseLightRigView>();
            CanvasGroup warningVisual = root.AddComponent<CanvasGroup>();
            AudioSource warningAudio = root.AddComponent<AudioSource>();
            LightTransitionWarningView warningView = root.AddComponent<LightTransitionWarningView>();
            Light houseLight = new GameObject("House light").AddComponent<Light>();
            houseLight.transform.SetParent(root.transform);

            presentation.Initialize(fake);
            rig.Initialize(presentation, new[] { houseLight });
            warningView.Initialize(presentation, warningVisual, warningAudio);
            Assert.That(houseLight.enabled, Is.False);
            fake.SynchronizedTime = 0d;
            fake.SetSnapshot(new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.Off,
                0d,
                1200d,
                80d,
                1u));

            int warningCount = 0;
            presentation.LightTransitionWarningStarted += () => warningCount++;
            root.SetActive(true);
            yield return null;

            Assert.That(houseLight.enabled, Is.False);

            fake.SynchronizedTime = 77d;
            yield return null;
            yield return null;

            Assert.That(presentation.Current.IsLightTransitionWarning, Is.True);
            Assert.That(warningCount, Is.EqualTo(1));
            Assert.That(warningVisual.alpha, Is.EqualTo(1f));
            Assert.That(warningAudio.isPlaying, Is.True);

            presentation.enabled = false;
            fake.SynchronizedTime = 157d;
            fake.SetSnapshot(new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.On,
                0d,
                1200d,
                160d,
                2u));
            presentation.enabled = true;
            yield return null;

            Assert.That(warningCount, Is.EqualTo(2));

            fake.SynchronizedTime = 277d;
            fake.SetSnapshot(new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.On,
                200d,
                1400d,
                280d,
                1u));
            yield return null;

            Assert.That(warningCount, Is.EqualTo(3));

            fake.SetSnapshot(default);
            yield return null;

            Assert.That(houseLight.enabled, Is.False);
            Assert.That(warningVisual.alpha, Is.Zero);

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator NetworkTimelineDoesNotStartWithoutAnNgoSpawn()
        {
            var gameObject = new GameObject("Unspawned network timeline");
            NetworkMatchTimeline timeline = gameObject.AddComponent<NetworkMatchTimeline>();

            yield return null;

            Assert.That(timeline.IsSpawned, Is.False);
            Assert.That(timeline.Snapshot.IsStarted, Is.False);
            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator ActiveComponentsRecoverWhenInitializedExplicitly()
        {
            var root = new GameObject("Active timeline composition test");
            FakeMatchTimelineSource fake = root.AddComponent<FakeMatchTimelineSource>();

            LogAssert.Expect(
                LogType.Error,
                "MatchTimelinePresentationSource requires an explicitly assigned IMatchTimelineSource.");
            MatchTimelinePresentationSource presentation = root.AddComponent<MatchTimelinePresentationSource>();
            presentation.Initialize(fake);

            LogAssert.Expect(
                LogType.Error,
                "HouseLightRigView requires an explicitly assigned timeline source.");
            HouseLightRigView rig = root.AddComponent<HouseLightRigView>();
            Light houseLight = new GameObject("Runtime wired light").AddComponent<Light>();
            houseLight.transform.SetParent(root.transform);
            rig.Initialize(presentation, new[] { houseLight });

            fake.SynchronizedTime = 10d;
            fake.SetSnapshot(new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.On,
                0d,
                1200d,
                60d,
                1u));
            yield return null;

            Assert.That(presentation.enabled, Is.True);
            Assert.That(rig.enabled, Is.True);
            Assert.That(houseLight.enabled, Is.True);
            Object.Destroy(root);
        }

        [Test]
        public void NetworkSnapshotRoundTripsAsOneValue()
        {
            var expected = new MatchTimelineSnapshot(
                true,
                false,
                LightPhase.On,
                10d,
                1210d,
                65d,
                4u);

            var networkSnapshot = new NetworkMatchTimelineSnapshot(expected);

            Assert.That(networkSnapshot.ToCoreSnapshot(), Is.EqualTo(expected));
        }

        private static T FindComponentInActiveScene<T>() where T : Component
        {
            T[] components = FindComponentsInActiveScene<T>();
            return components.Length > 0 ? components[0] : null;
        }

        private static T[] FindComponentsInActiveScene<T>() where T : Component
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
