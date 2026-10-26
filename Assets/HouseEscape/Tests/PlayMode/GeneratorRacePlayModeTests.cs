using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Generators;
using HouseEscape.Infrastructure;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Generators;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Vitality;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Presentation.Generators;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    /// <summary>
    /// Host-side NGO integration of the generator race in the composed Game scene. The real
    /// timeline starts with lights off for 80-90 s, so the race is pointed at a controllable
    /// match/light source on the server. Remote-client paths are not covered here because
    /// remote-console participant registration is still deferred in the composition.
    /// </summary>
    public sealed class GeneratorRacePlayModeTests : InputTestFixture
    {
        private static readonly MatchTimelineSnapshot LightsOn =
            new MatchTimelineSnapshot(true, false, LightPhase.On, 0d, 1_000_000d, 1_000_000d, 100u);
        private static readonly MatchTimelineSnapshot LightsOff =
            new MatchTimelineSnapshot(true, false, LightPhase.Off, 0d, 1_000_000d, 1_000_000d, 101u);
        private static readonly MatchTimelineSnapshot MatchEnded =
            new MatchTimelineSnapshot(true, true, LightPhase.On, 0d, 1_000_000d, 1_000_000d, 102u);

        private readonly List<Participant> _participants = new List<Participant>();
        private NetworkGeneratorRace _race;
        private NetworkGenerator _nearGenerator;
        private FakeMatchTimelineSource _matchState;
        private ulong _hostClientId;

        private Participant First => _participants[0];

        [UnityTearDown]
        public IEnumerator TearDownNetworkScene()
        {
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
                Scene temporaryScene = SceneManager.CreateScene("Generator test cleanup");
                SceneManager.SetActiveScene(temporaryScene);
                yield return SceneManager.UnloadSceneAsync(gameScene);
            }
        }

        [UnityTest]
        public IEnumerator HeldAttemptCompletesOnServerTimeAwardsOnceAndBlocksWalking()
        {
            yield return StartMatch(1);
            yield return SteerOntoNearGenerator(First);

            Press(First.Gamepad.buttonSouth);
            yield return WaitForAttempt(First, true);
            Assert.That(_race.TryGetAttempt(First.Context.ParticipantId, out int generatorNumber, out double startedAt), Is.True);
            Assert.That(generatorNumber, Is.EqualTo(1));
            Assert.That(First.Avatar.IsMovementBlocked, Is.True);
            GeneratorHudView hud = FindHud(First);
            yield return null;
            StringAssert.StartsWith("Generator 1: HOLD A / CROSS", hud.AttemptProgressText);

            Vector3 lockedPosition = First.Avatar.transform.position;
            Set(First.Gamepad.leftStick, Vector2.up);
            for (int frame = 0; frame < 15; frame++)
            {
                yield return null;
            }

            Assert.That(Vector3.Distance(First.Avatar.transform.position, lockedPosition), Is.LessThan(0.001f),
                "Walking must be blocked while the attempt is in progress.");
            Set(First.Gamepad.leftStick, Vector2.zero);

            yield return WaitUntilActive(_nearGenerator);
            Assert.That(_race.SynchronizedTime - startedAt, Is.GreaterThanOrEqualTo(GeneratorRace.RequiredHoldSeconds),
                "Completion is driven by server time after the full hold.");
            Assert.That(_race.ActiveGeneratorCount, Is.EqualTo(1));
            Assert.That(_race.GetScore(First.Context.ParticipantId), Is.EqualTo(200));
            Assert.That(_race.TryGetAttempt(First.Context.ParticipantId, out _, out _), Is.False);
            Assert.That(First.Avatar.IsMovementBlocked, Is.False);
            yield return null;
            Assert.That(hud.GeneratorCountText, Is.EqualTo("Generators: 1/6"));
            Assert.That(hud.AttemptProgressText, Is.Empty);

            Release(First.Gamepad.buttonSouth);
            yield return WaitForBeginInterval();
            Press(First.Gamepad.buttonSouth);
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(_race.TryGetAttempt(First.Context.ParticipantId, out _, out _), Is.False,
                "An active generator cannot be attempted again.");
            Assert.That(_race.GetScore(First.Context.ParticipantId), Is.EqualTo(200));
            Release(First.Gamepad.buttonSouth);
        }

        [UnityTest]
        public IEnumerator TwoLocalParticipantsRacingOneGeneratorProduceASingleWinnerAndScore()
        {
            yield return StartMatch(2);
            Participant second = _participants[1];
            yield return SteerOntoNearGenerator(First);
            yield return SteerOntoNearGenerator(second);

            Press(First.Gamepad.buttonSouth);
            Press(second.Gamepad.buttonSouth);
            yield return WaitForAttempt(First, true);
            yield return WaitForAttempt(second, true);
            Assert.That(_race.InProgressAttemptCount, Is.EqualTo(2));
            Assert.That(First.Avatar.IsMovementBlocked && second.Avatar.IsMovementBlocked, Is.True);

            yield return WaitUntilActive(_nearGenerator);
            yield return null;

            int firstScore = _race.GetScore(First.Context.ParticipantId);
            int secondScore = _race.GetScore(second.Context.ParticipantId);
            Assert.That(new[] { firstScore, secondScore }.OrderBy(score => score), Is.EqualTo(new[] { 0, 200 }),
                "Exactly one participant wins the generator and scores 200 once.");
            Assert.That(_race.ActiveGeneratorCount, Is.EqualTo(1));
            Assert.That(_race.InProgressAttemptCount, Is.Zero, "The loser's attempt ends with the activation.");
            Assert.That(First.Avatar.IsMovementBlocked, Is.False);
            Assert.That(second.Avatar.IsMovementBlocked, Is.False, "The loser can walk again.");
            Release(First.Gamepad.buttonSouth);
            Release(second.Gamepad.buttonSouth);
        }

        [UnityTest]
        public IEnumerator ServerRejectsInvalidIntentionsAndCancelsOnReleaseLightsOffAndDisconnect()
        {
            yield return StartMatch(1);
            Participant participant = First;

            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.NotAiming),
                "Generator 1 is in range of spawn 1, but the spawn view looks over it.");
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.BeginTooSoon));
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId + 77),
                Is.EqualTo(GeneratorAttemptRequestResult.SenderDoesNotOwnParticipant));
            yield return WaitForBeginInterval();
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 2, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.OutOfRange));

            yield return SteerOntoNearGenerator(participant);
            _matchState.SetSnapshot(LightsOff);
            yield return WaitForBeginInterval();
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.LightsOff));
            _matchState.SetSnapshot(LightsOn);

            // Releasing before ten seconds cancels and resets.
            yield return WaitForBeginInterval();
            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            yield return WaitForBeginInterval();
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.AlreadyAttempting));
            Release(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, false);
            Assert.That(_nearGenerator.IsActive, Is.False);
            Assert.That(participant.Avatar.IsMovementBlocked, Is.False);

            // Lights off cancels every in-progress attempt.
            yield return WaitForBeginInterval();
            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            _matchState.SetSnapshot(LightsOff);
            Assert.That(_race.InProgressAttemptCount, Is.Zero);
            Assert.That(participant.Avatar.IsMovementBlocked, Is.False);
            Release(participant.Gamepad.buttonSouth);
            yield return null;
            _matchState.SetSnapshot(LightsOn);

            // A gamepad disconnect counts as releasing; the participant stays reserved.
            yield return WaitForBeginInterval();
            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            InputSystem.RemoveDevice(participant.Gamepad);
            yield return WaitForAttempt(participant, false);
            Assert.That(participant.Context.IsReserved, Is.True);
            Assert.That(participant.Context.IsConnected, Is.False);
            Assert.That(_nearGenerator.IsActive, Is.False);
            Assert.That(_race.GetScore(participant.Context.ParticipantId), Is.Zero);
        }

        [UnityTest]
        public IEnumerator MatchEndAndAvatarDespawnCancelAnInProgressAttempt()
        {
            yield return StartMatch(1);
            Participant participant = First;
            yield return SteerOntoNearGenerator(participant);

            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            _matchState.SetSnapshot(MatchEnded);
            Assert.That(_race.InProgressAttemptCount, Is.Zero, "Match end cancels the attempt immediately.");
            Assert.That(participant.Avatar.IsMovementBlocked, Is.False);
            Release(participant.Gamepad.buttonSouth);
            yield return null;
            _matchState.SetSnapshot(LightsOn);

            yield return WaitForBeginInterval();
            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            participant.Avatar.NetworkObject.Despawn();
            yield return WaitForAttempt(participant, false);
            Assert.That(_nearGenerator.IsActive, Is.False);
            Assert.That(_race.GetScore(participant.Context.ParticipantId), Is.Zero);
            Release(participant.Gamepad.buttonSouth);
        }

        [UnityTest]
        public IEnumerator UnconsciousParticipantAttemptIsCanceledNewOnesRejectedAndOnlyLookIsKept()
        {
            yield return StartMatch(1);
            Participant participant = First;
            NetworkParticipantVitality vitality = FindComponent<NetworkParticipantVitality>();
            yield return SteerOntoNearGenerator(participant);

            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            Assert.That(vitality.ApplyDamageOnServer(participant.Context.ParticipantId, VitalityRules.MaximumHealth),
                Is.EqualTo(VitalityDamageResult.KnockedUnconscious));
            yield return WaitForAttempt(participant, false);
            Assert.That(_nearGenerator.IsActive, Is.False);
            Assert.That(participant.Avatar.IsIncapacitated, Is.True);
            Assert.That(participant.Avatar.IsMovementBlocked, Is.True,
                "Canceling the attempt must not lift the unconscious walk block.");
            Release(participant.Gamepad.buttonSouth);

            yield return WaitForBeginInterval();
            Assert.That(_race.TryBeginAttemptOnServer(participant.Context.ParticipantId, 1, _hostClientId),
                Is.EqualTo(GeneratorAttemptRequestResult.ParticipantIncapacitated));

            // Locally, a press while unconscious sends no begin request at all.
            LocalParticipantGeneratorDriver generatorDriver = FindComponents<LocalParticipantGeneratorDriver>()
                .Single(driver => driver.Context == participant.Context);
            yield return WaitForBeginInterval();
            Press(participant.Gamepad.buttonSouth);
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.That(generatorDriver.RequestedGeneratorNumber, Is.Zero);
            Assert.That(_race.TryGetAttempt(participant.Context.ParticipantId, out _, out _), Is.False);
            Release(participant.Gamepad.buttonSouth);

            Vector3 position = participant.Avatar.transform.position;
            float yaw = participant.Avatar.transform.eulerAngles.y;
            Set(participant.Gamepad.leftStick, Vector2.up);
            Set(participant.Gamepad.rightStick, Vector2.right);
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            Set(participant.Gamepad.leftStick, Vector2.zero);
            Set(participant.Gamepad.rightStick, Vector2.zero);
            Assert.That(Vector3.Distance(participant.Avatar.transform.position, position), Is.LessThan(0.001f),
                "An unconscious participant cannot walk.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, participant.Avatar.transform.eulerAngles.y)), Is.GreaterThan(1f),
                "An unconscious participant may still turn the camera.");
        }

        [UnityTest]
        public IEnumerator HoldAlreadyDueAtKnockoutTimeStillCompletes()
        {
            yield return StartMatch(1);
            Participant participant = First;
            NetworkParticipantVitality vitality = FindComponent<NetworkParticipantVitality>();
            yield return SteerOntoNearGenerator(participant);

            Press(participant.Gamepad.buttonSouth);
            yield return WaitForAttempt(participant, true);
            Assert.That(_race.TryGetAttempt(participant.Context.ParticipantId, out _, out double startedAt), Is.True);

            // Hold the race's resolution so the knockout lands after the hold's due time but
            // before the server processes the completion.
            _race.enabled = false;
            float timeout = Time.realtimeSinceStartup + (float)GeneratorRace.RequiredHoldSeconds + 3f;
            while (_race.SynchronizedTime < startedAt + GeneratorRace.RequiredHoldSeconds + 0.1d
                && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(vitality.ApplyDamageOnServer(participant.Context.ParticipantId, VitalityRules.MaximumHealth),
                Is.EqualTo(VitalityDamageResult.KnockedUnconscious));
            _race.enabled = true;
            yield return null;
            yield return null;

            Assert.That(_nearGenerator.IsActive, Is.True, "A hold due before the knockout wins.");
            Assert.That(_race.GetScore(participant.Context.ParticipantId), Is.EqualTo(200));
            Assert.That(_race.TryGetAttempt(participant.Context.ParticipantId, out _, out _), Is.False);
            Release(participant.Gamepad.buttonSouth);
        }

        private IEnumerator StartMatch(int participantCount)
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

            _hostClientId = NetworkManager.Singleton.LocalClientId;
            NetworkParticipantRegistry registry = FindComponent<NetworkParticipantRegistry>();
            NetworkParticipantAvatarSpawner spawner = FindComponent<NetworkParticipantAvatarSpawner>();
            _race = FindComponent<NetworkGeneratorRace>();
            Assert.That(_race, Is.Not.Null);

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
            for (int frame = 0; frame < 120 && spawner.SpawnedAvatars.Count < participantCount; frame++)
            {
                yield return null;
            }

            Assert.That(spawner.SpawnedAvatars, Has.Count.EqualTo(participantCount));
            Assert.That(_race.IsSpawned, Is.True);
            LocalParticipantContext[] contexts = FindComponents<LocalParticipantContext>()
                .Where(context => context.IsReserved)
                .OrderBy(context => context.LocalSlot)
                .ToArray();
            Assert.That(contexts, Has.Length.EqualTo(participantCount));
            for (int index = 0; index < participantCount; index++)
            {
                Assert.That(contexts[index].ParticipantId.IsValid, Is.True);
                Assert.That(spawner.TryGetAvatar(contexts[index].ParticipantId, out NetworkParticipantAvatar avatar), Is.True);
                _participants.Add(new Participant(gamepads[index], contexts[index], avatar));
            }

            Assert.That(_race.TryGetGenerator(1, out _nearGenerator), Is.True);
            _matchState = new GameObject("Controllable Match State").AddComponent<FakeMatchTimelineSource>();
            _matchState.SetSnapshot(LightsOn);
            _race.UseMatchStateSourceOnServer(_matchState);
        }

        /// <summary>Walks into range of generator 1, then turns and pitches until aiming at it.</summary>
        private IEnumerator SteerOntoNearGenerator(Participant participant)
        {
            const float approachDistance = 1.7f;
            for (int frame = 0; frame < 300; frame++)
            {
                Vector3 offset = _nearGenerator.InteractionPosition - participant.Avatar.transform.position;
                offset.y = 0f;
                if (offset.magnitude <= approachDistance)
                {
                    break;
                }

                Vector3 local = Quaternion.Inverse(participant.Avatar.transform.rotation) * offset.normalized;
                Set(participant.Gamepad.leftStick, new Vector2(local.x, local.z));
                yield return null;
            }

            Set(participant.Gamepad.leftStick, Vector2.zero);
            Assert.That(GeneratorInteractionQuery.IsWithinRange(_nearGenerator, participant.Avatar.transform.position), Is.True);

            for (int settle = 0; settle < 4 && !IsAiming(participant); settle++)
            {
                for (int frame = 0; frame < 300 && !IsAiming(participant); frame++)
                {
                    Set(participant.Gamepad.rightStick, GetAimStick(participant));
                    yield return null;
                }

                Set(participant.Gamepad.rightStick, Vector2.zero);
                for (int frame = 0; frame < 15; frame++)
                {
                    yield return null;
                }
            }

            Assert.That(IsAiming(participant), Is.True, "The participant view should rest on generator 1.");
        }

        private Vector2 GetAimStick(Participant participant)
        {
            Transform view = participant.Context.ParticipantCamera.transform;
            Vector3 toTarget = _nearGenerator.AimPosition - view.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float yawError = Vector3.SignedAngle(flatForward, flatToTarget, Vector3.up);
            float desiredPitch = Mathf.Atan2(toTarget.y, flatToTarget.magnitude) * Mathf.Rad2Deg;
            float pitchError = desiredPitch - participant.Avatar.PitchDegrees;
            return new Vector2(AxisFor(yawError), AxisFor(pitchError));
        }

        private static float AxisFor(float errorDegrees)
        {
            if (Mathf.Abs(errorDegrees) < 2f)
            {
                return 0f;
            }

            return Mathf.Sign(errorDegrees) * Mathf.Clamp(Mathf.Abs(errorDegrees) / 45f, 0.35f, 0.8f);
        }

        private bool IsAiming(Participant participant)
        {
            Transform view = participant.Context.ParticipantCamera.transform;
            return GeneratorInteractionQuery.CanBeginAttempt(
                _nearGenerator,
                participant.Avatar.transform.position,
                view.position,
                view.forward);
        }

        private IEnumerator WaitForAttempt(Participant participant, bool expected)
        {
            for (int frame = 0; frame < 30; frame++)
            {
                if (_race.TryGetAttempt(participant.Context.ParticipantId, out _, out _) == expected)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(_race.TryGetAttempt(participant.Context.ParticipantId, out _, out _), Is.EqualTo(expected));
        }

        private static IEnumerator WaitUntilActive(NetworkGenerator generator)
        {
            float timeout = Time.realtimeSinceStartup + (float)GeneratorRace.RequiredHoldSeconds + 3f;
            while (!generator.IsActive && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(generator.IsActive, Is.True);
        }

        private IEnumerator WaitForBeginInterval()
        {
            double resumeAt = _race.SynchronizedTime + GeneratorBeginRateLimiter.MinimumBeginIntervalSeconds + 0.05d;
            float timeout = Time.realtimeSinceStartup + 2f;
            while (_race.SynchronizedTime < resumeAt && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private GeneratorHudView FindHud(Participant participant)
        {
            return FindComponents<GeneratorHudView>().Single(hud => hud.ParticipantId == participant.Context.ParticipantId);
        }

        private static T FindComponent<T>() where T : Component
        {
            T[] components = FindComponents<T>();
            return components.Length == 0 ? null : components[0];
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
