using System.Collections;
using HouseEscape.LocalMultiplayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HouseEscape.Tests.PlayMode
{
    public sealed class LocalParticipantJoinPlayModeTests : InputTestFixture
    {
        private GameObject _root;
        private InputActionAsset _actions;
        private LocalParticipantContext[] _contexts;
        private LocalParticipantJoinManager _joinManager;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _actions = CreateActions();
            _root = new GameObject("Local join test root");
            _contexts = new LocalParticipantContext[4];
            for (byte slot = 0; slot < _contexts.Length; slot++)
            {
                GameObject contextObject = new GameObject($"Context {slot + 1}");
                contextObject.transform.SetParent(_root.transform);
                PlayerInput playerInput = contextObject.AddComponent<PlayerInput>();
                Camera camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(contextObject.transform);
                Canvas canvas = new GameObject("HUD").AddComponent<Canvas>();
                canvas.transform.SetParent(contextObject.transform);
                LocalParticipantContext context = contextObject.AddComponent<LocalParticipantContext>();
                context.Initialize(slot, playerInput, camera, canvas);
                _contexts[slot] = context;
            }

            _joinManager = _root.AddComponent<LocalParticipantJoinManager>();
            _joinManager.Initialize(_actions, _contexts);
            _joinManager.OpenJoining(99);
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_actions);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FourGamepadsJoinExclusivelyFifthAndKeyboardAreRejectedAndLayoutsAreCorrect()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.enterKey);
            Release(keyboard.enterKey);
            yield return null;
            Assert.That(_joinManager.ReservedCount, Is.Zero);

            var gamepads = new Gamepad[5];
            for (int index = 0; index < gamepads.Length; index++)
            {
                gamepads[index] = InputSystem.AddDevice<Gamepad>();
                Press(gamepads[index].startButton);
                Release(gamepads[index].startButton);
                yield return null;
            }

            Assert.That(_joinManager.ReservedCount, Is.EqualTo(4));
            for (int index = 0; index < _contexts.Length; index++)
            {
                Assert.That(_contexts[index].IsPairedTo(gamepads[index]), Is.True);
                Assert.That(_contexts[index].PlayerInput.devices, Has.Count.EqualTo(1));
                Assert.That(_contexts[index].ParticipantCamera.rect,
                    Is.EqualTo(LocalParticipantViewportLayout.GetViewport(4, index)));
            }

            Assert.That(_contexts[0].IsPairedTo(gamepads[1]), Is.False);
            Assert.That(_contexts[3].IsPairedTo(gamepads[4]), Is.False);
        }

        [UnityTest]
        public IEnumerator OnlyPlayerOneStartsJoiningClosesAndDisconnectedSlotRemainsReserved()
        {
            Gamepad[] gamepads = new Gamepad[4];
            for (int index = 0; index < gamepads.Length; index++)
            {
                gamepads[index] = InputSystem.AddDevice<Gamepad>();
                Press(gamepads[index].startButton);
                Release(gamepads[index].startButton);
                yield return null;
            }

            int startRequests = 0;
            _joinManager.MatchStartRequested += () =>
            {
                startRequests++;
                _joinManager.CloseJoining();
            };

            Press(gamepads[1].buttonSouth);
            Release(gamepads[1].buttonSouth);
            yield return null;
            Assert.That(startRequests, Is.Zero);

            Press(gamepads[0].buttonSouth);
            Release(gamepads[0].buttonSouth);
            yield return null;
            Assert.That(startRequests, Is.EqualTo(1));
            Assert.That(_joinManager.IsJoiningOpen, Is.False);

            InputSystem.RemoveDevice(gamepads[2]);
            yield return null;
            Assert.That(_contexts[2].IsReserved, Is.True);
            Assert.That(_contexts[2].IsConnected, Is.False);
            Assert.That(_joinManager.ReservedCount, Is.EqualTo(4));

            Gamepad replacement = InputSystem.AddDevice<Gamepad>();
            Press(replacement.startButton);
            Release(replacement.startButton);
            yield return null;
            Assert.That(_joinManager.ReservedCount, Is.EqualTo(4));
            Assert.That(_contexts[2].IsPairedTo(replacement), Is.False);
        }

        [UnityTest]
        public IEnumerator MoveAndLookAreRoutedIndependentlyAndDeviceLossClearsBoth()
        {
            Gamepad first = InputSystem.AddDevice<Gamepad>();
            Gamepad second = InputSystem.AddDevice<Gamepad>();
            Press(first.startButton);
            Release(first.startButton);
            yield return null;
            Press(second.startButton);
            Release(second.startButton);
            yield return null;

            Set(first.leftStick, new Vector2(0.75f, -0.25f));
            Set(first.rightStick, new Vector2(-0.5f, 0.9f));
            Set(second.leftStick, new Vector2(-0.2f, 0.4f));
            Set(second.rightStick, new Vector2(0.3f, -0.7f));
            yield return null;

            Assert.That(_contexts[0].Move, Is.EqualTo(first.leftStick.ReadValue()));
            Assert.That(_contexts[0].Look, Is.EqualTo(first.rightStick.ReadValue()));
            Assert.That(_contexts[1].Move, Is.EqualTo(second.leftStick.ReadValue()));
            Assert.That(_contexts[1].Look, Is.EqualTo(second.rightStick.ReadValue()));

            Press(first.buttonSouth);
            yield return null;
            Assert.That(_contexts[0].IsInteractHeld, Is.True);
            Assert.That(_contexts[1].IsInteractHeld, Is.False);

            InputSystem.RemoveDevice(first);
            yield return null;
            Assert.That(_contexts[0].Move, Is.EqualTo(Vector2.zero));
            Assert.That(_contexts[0].Look, Is.EqualTo(Vector2.zero));
            Assert.That(_contexts[0].IsInteractHeld, Is.False, "Device loss counts as releasing the hold.");
            Assert.That(_contexts[1].Move, Is.Not.EqualTo(Vector2.zero));
            Assert.That(_contexts[1].Look, Is.Not.EqualTo(Vector2.zero));
        }

        [Test]
        public void ViewportLayoutUsesFullVerticalHalvesAndThreePlayerEmptyFourthQuadrant()
        {
            Assert.That(LocalParticipantViewportLayout.GetViewport(1, 0), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(LocalParticipantViewportLayout.GetViewport(2, 0), Is.EqualTo(new Rect(0f, 0f, 0.5f, 1f)));
            Assert.That(LocalParticipantViewportLayout.GetViewport(2, 1), Is.EqualTo(new Rect(0.5f, 0f, 0.5f, 1f)));
            Assert.That(LocalParticipantViewportLayout.GetViewport(3, 2), Is.EqualTo(new Rect(0f, 0f, 0.5f, 0.5f)));
        }

        private static InputActionAsset CreateActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap(LocalParticipantContext.ActionMapName);
            map.AddAction("Join", InputActionType.Button).AddBinding("<Gamepad>/start");
            map.AddAction(LocalParticipantContext.StartMatchActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/buttonSouth");
            InputAction navigate = map.AddAction("Navigate", InputActionType.Value);
            navigate.expectedControlType = "Vector2";
            navigate.AddBinding("<Gamepad>/leftStick");
            InputAction move = map.AddAction(LocalParticipantContext.MoveActionName, InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddBinding("<Gamepad>/leftStick");
            InputAction look = map.AddAction(LocalParticipantContext.LookActionName, InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Gamepad>/rightStick");
            map.AddAction(LocalParticipantContext.InteractActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/buttonSouth");
            map.AddAction(LocalParticipantContext.SpectateNextActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/buttonSouth");
            map.AddAction(LocalParticipantContext.RunActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/leftStickPress");
            map.AddAction(LocalParticipantContext.SelectPreviousItemActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/dpad/left");
            map.AddAction(LocalParticipantContext.SelectNextItemActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/dpad/right");
            map.AddAction(LocalParticipantContext.UseItemActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/buttonWest");
            map.AddAction(LocalParticipantContext.ToggleFlashlightActionName, InputActionType.Button)
                .AddBinding("<Gamepad>/rightShoulder");
            asset.AddControlScheme(LocalParticipantContext.GamepadControlScheme)
                .WithRequiredDevice("<Gamepad>");
            return asset;
        }
    }
}
