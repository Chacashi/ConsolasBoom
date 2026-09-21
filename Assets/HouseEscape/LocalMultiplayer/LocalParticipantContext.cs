using System;
using HouseEscape.Core.Participants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HouseEscape.LocalMultiplayer
{
    [DisallowMultipleComponent]
    public sealed class LocalParticipantContext : MonoBehaviour
    {
        public const string ActionMapName = "LocalPlayer";
        public const string GamepadControlScheme = "Gamepad";
        public const string StartMatchActionName = "StartMatch";

        [SerializeField] private byte localSlot;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Camera participantCamera;
        [SerializeField] private Canvas participantCanvas;

        private InputActionAsset _actionsInstance;
        private InputAction _startMatchAction;

        public byte LocalSlot => localSlot;
        public ParticipantId ParticipantId { get; private set; }
        public bool IsReserved { get; private set; }
        public bool IsConnected { get; private set; }
        public PlayerInput PlayerInput => playerInput;
        public Camera ParticipantCamera => participantCamera;
        public Canvas ParticipantCanvas => participantCanvas;

        public event Action<LocalParticipantContext> StartMatchPerformed;
        public event Action<LocalParticipantContext, bool> ConnectionChanged;

        public void Initialize(byte slot, PlayerInput input, Camera camera, Canvas canvas)
        {
            if (slot >= ParticipantRegistry.MaximumParticipants)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            localSlot = slot;
            playerInput = input != null ? input : throw new ArgumentNullException(nameof(input));
            participantCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            participantCanvas = canvas != null ? canvas : throw new ArgumentNullException(nameof(canvas));
            SetPresentationActive(false);
            playerInput.enabled = false;
        }

        public void Reserve(Gamepad gamepad, InputActionAsset actions)
        {
            if (IsReserved)
            {
                throw new InvalidOperationException($"Local slot {localSlot} is already reserved.");
            }

            if (gamepad == null)
            {
                throw new ArgumentNullException(nameof(gamepad));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            IsReserved = true;
            IsConnected = true;
            _actionsInstance = Instantiate(actions);
            playerInput.actions = _actionsInstance;
            playerInput.defaultActionMap = ActionMapName;
            playerInput.defaultControlScheme = GamepadControlScheme;
            playerInput.neverAutoSwitchControlSchemes = true;
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            playerInput.enabled = true;
            playerInput.SwitchCurrentControlScheme(GamepadControlScheme, gamepad);
            playerInput.ActivateInput();

            _startMatchAction = playerInput.actions.FindAction(
                $"{ActionMapName}/{StartMatchActionName}",
                true);
            _startMatchAction.performed += HandleStartMatchPerformed;
            playerInput.onDeviceLost += HandleDeviceLost;
            playerInput.onDeviceRegained += HandleDeviceRegained;
            SetPresentationActive(true);
        }

        public void BindParticipant(ParticipantId participantId)
        {
            if (!participantId.IsValid)
            {
                throw new ArgumentException("A local context requires a valid participant ID.", nameof(participantId));
            }

            ParticipantId = participantId;
        }

        public bool IsPairedTo(InputDevice device)
        {
            if (!IsReserved || device == null)
            {
                return false;
            }

            foreach (InputDevice pairedDevice in playerInput.devices)
            {
                if (pairedDevice == device)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetViewport(Rect viewport)
        {
            participantCamera.rect = viewport;
        }

        public void MarkDeviceDisconnected()
        {
            if (!IsReserved || !IsConnected)
            {
                return;
            }

            IsConnected = false;
            ConnectionChanged?.Invoke(this, false);
        }

        public void MarkDeviceReconnected()
        {
            if (!IsReserved || IsConnected)
            {
                return;
            }

            IsConnected = true;
            ConnectionChanged?.Invoke(this, true);
        }

        private void OnDestroy()
        {
            if (_startMatchAction != null)
            {
                _startMatchAction.performed -= HandleStartMatchPerformed;
            }

            if (playerInput != null)
            {
                playerInput.onDeviceLost -= HandleDeviceLost;
                playerInput.onDeviceRegained -= HandleDeviceRegained;
                playerInput.DeactivateInput();
                playerInput.actions = null;
            }

            if (_actionsInstance != null)
            {
                _actionsInstance.Disable();
                Destroy(_actionsInstance);
                _actionsInstance = null;
            }
        }

        private void SetPresentationActive(bool active)
        {
            if (participantCamera != null)
            {
                participantCamera.enabled = active;
            }

            if (participantCanvas != null)
            {
                participantCanvas.enabled = active;
            }
        }

        private void HandleStartMatchPerformed(InputAction.CallbackContext callbackContext)
        {
            StartMatchPerformed?.Invoke(this);
        }

        private void HandleDeviceLost(PlayerInput input)
        {
            MarkDeviceDisconnected();
        }

        private void HandleDeviceRegained(PlayerInput input)
        {
            MarkDeviceReconnected();
        }
    }
}
