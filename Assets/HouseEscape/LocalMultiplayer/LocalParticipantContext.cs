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
        public const string MoveActionName = "Move";
        public const string LookActionName = "Look";
        public const string InteractActionName = "Interact";
        public const string SpectateNextActionName = "SpectateNext";
        public const string RunActionName = "Run";
        public const string SelectPreviousItemActionName = "SelectPreviousItem";
        public const string SelectNextItemActionName = "SelectNextItem";
        public const string UseItemActionName = "UseItem";
        public const string ToggleFlashlightActionName = "ToggleFlashlight";

        [SerializeField] private byte localSlot;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Camera participantCamera;
        [SerializeField] private Canvas participantCanvas;

        private InputActionAsset _actionsInstance;
        private InputAction _startMatchAction;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _interactAction;
        private InputAction _spectateNextAction;
        private InputAction _runAction;
        private InputAction _selectPreviousItemAction;
        private InputAction _selectNextItemAction;
        private InputAction _useItemAction;
        private InputAction _toggleFlashlightAction;

        public byte LocalSlot => localSlot;
        public ParticipantId ParticipantId { get; private set; }
        public bool IsReserved { get; private set; }
        public bool IsConnected { get; private set; }
        public PlayerInput PlayerInput => playerInput;
        public Camera ParticipantCamera => participantCamera;
        public Canvas ParticipantCanvas => participantCanvas;
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool IsInteractHeld { get; private set; }

        /// <summary>Left stick button (L3) held: a run intention sent with movement commands.</summary>
        public bool IsRunHeld { get; private set; }

        public event Action<LocalParticipantContext> StartMatchPerformed;
        public event Action<LocalParticipantContext, bool> ConnectionChanged;
        /// <summary>Raised when move, look or the run button changes.</summary>
        public event Action<LocalParticipantContext> MovementInputChanged;
        public event Action<LocalParticipantContext, bool> InteractHeldChanged;

        /// <summary>Primary south face button press, used to cycle the spectated teammate.</summary>
        public event Action<LocalParticipantContext> SpectateNextPerformed;

        /// <summary>D-pad left (-1) or right (+1): change the selected item type.</summary>
        public event Action<LocalParticipantContext, int> ItemSelectionPerformed;

        /// <summary>West face button: consume one of the selected item.</summary>
        public event Action<LocalParticipantContext> UseItemPerformed;

        /// <summary>Right shoulder button: toggle the flashlight.</summary>
        public event Action<LocalParticipantContext> FlashlightTogglePerformed;

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
            _moveAction = playerInput.actions.FindAction($"{ActionMapName}/{MoveActionName}", true);
            _lookAction = playerInput.actions.FindAction($"{ActionMapName}/{LookActionName}", true);
            _moveAction.performed += HandleMoveChanged;
            _moveAction.canceled += HandleMoveChanged;
            _lookAction.performed += HandleLookChanged;
            _lookAction.canceled += HandleLookChanged;
            _interactAction = playerInput.actions.FindAction($"{ActionMapName}/{InteractActionName}", true);
            _interactAction.performed += HandleInteractPerformed;
            _interactAction.canceled += HandleInteractCanceled;
            _spectateNextAction = playerInput.actions.FindAction($"{ActionMapName}/{SpectateNextActionName}", true);
            _spectateNextAction.performed += HandleSpectateNextPerformed;
            _runAction = playerInput.actions.FindAction($"{ActionMapName}/{RunActionName}", true);
            _runAction.performed += HandleRunPerformed;
            _runAction.canceled += HandleRunCanceled;
            _selectPreviousItemAction = playerInput.actions.FindAction($"{ActionMapName}/{SelectPreviousItemActionName}", true);
            _selectPreviousItemAction.performed += HandleSelectPreviousItemPerformed;
            _selectNextItemAction = playerInput.actions.FindAction($"{ActionMapName}/{SelectNextItemActionName}", true);
            _selectNextItemAction.performed += HandleSelectNextItemPerformed;
            _useItemAction = playerInput.actions.FindAction($"{ActionMapName}/{UseItemActionName}", true);
            _useItemAction.performed += HandleUseItemPerformed;
            _toggleFlashlightAction = playerInput.actions.FindAction($"{ActionMapName}/{ToggleFlashlightActionName}", true);
            _toggleFlashlightAction.performed += HandleToggleFlashlightPerformed;
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
            ClearMovementInput();
            // GDD 3.5 rule 6: a gamepad disconnect counts as releasing the button.
            SetInteractHeld(false);
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

            if (_moveAction != null)
            {
                _moveAction.performed -= HandleMoveChanged;
                _moveAction.canceled -= HandleMoveChanged;
            }

            if (_lookAction != null)
            {
                _lookAction.performed -= HandleLookChanged;
                _lookAction.canceled -= HandleLookChanged;
            }

            if (_interactAction != null)
            {
                _interactAction.performed -= HandleInteractPerformed;
                _interactAction.canceled -= HandleInteractCanceled;
            }

            if (_spectateNextAction != null)
            {
                _spectateNextAction.performed -= HandleSpectateNextPerformed;
            }

            if (_runAction != null)
            {
                _runAction.performed -= HandleRunPerformed;
                _runAction.canceled -= HandleRunCanceled;
            }

            if (_selectPreviousItemAction != null)
            {
                _selectPreviousItemAction.performed -= HandleSelectPreviousItemPerformed;
            }

            if (_selectNextItemAction != null)
            {
                _selectNextItemAction.performed -= HandleSelectNextItemPerformed;
            }

            if (_useItemAction != null)
            {
                _useItemAction.performed -= HandleUseItemPerformed;
            }

            if (_toggleFlashlightAction != null)
            {
                _toggleFlashlightAction.performed -= HandleToggleFlashlightPerformed;
            }

            ClearMovementInput();
            SetInteractHeld(false);

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

        private void HandleMoveChanged(InputAction.CallbackContext callbackContext)
        {
            Vector2 next = IsConnected ? callbackContext.ReadValue<Vector2>() : Vector2.zero;
            if (Move != next)
            {
                Move = next;
                MovementInputChanged?.Invoke(this);
            }
        }

        private void HandleLookChanged(InputAction.CallbackContext callbackContext)
        {
            Vector2 next = IsConnected ? callbackContext.ReadValue<Vector2>() : Vector2.zero;
            if (Look != next)
            {
                Look = next;
                MovementInputChanged?.Invoke(this);
            }
        }

        private void HandleInteractPerformed(InputAction.CallbackContext callbackContext)
        {
            SetInteractHeld(IsConnected);
        }

        private void HandleInteractCanceled(InputAction.CallbackContext callbackContext)
        {
            SetInteractHeld(false);
        }

        private void HandleSpectateNextPerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsConnected)
            {
                SpectateNextPerformed?.Invoke(this);
            }
        }

        private void HandleSelectPreviousItemPerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsConnected)
            {
                ItemSelectionPerformed?.Invoke(this, -1);
            }
        }

        private void HandleSelectNextItemPerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsConnected)
            {
                ItemSelectionPerformed?.Invoke(this, 1);
            }
        }

        private void HandleUseItemPerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsConnected)
            {
                UseItemPerformed?.Invoke(this);
            }
        }

        private void HandleToggleFlashlightPerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsConnected)
            {
                FlashlightTogglePerformed?.Invoke(this);
            }
        }

        private void HandleRunPerformed(InputAction.CallbackContext callbackContext)
        {
            SetRunHeld(IsConnected);
        }

        private void HandleRunCanceled(InputAction.CallbackContext callbackContext)
        {
            SetRunHeld(false);
        }

        private void SetRunHeld(bool held)
        {
            if (IsRunHeld == held)
            {
                return;
            }

            IsRunHeld = held;
            MovementInputChanged?.Invoke(this);
        }

        private void SetInteractHeld(bool held)
        {
            if (IsInteractHeld == held)
            {
                return;
            }

            IsInteractHeld = held;
            InteractHeldChanged?.Invoke(this, held);
        }

        private void ClearMovementInput()
        {
            bool changed = Move != Vector2.zero || Look != Vector2.zero || IsRunHeld;
            Move = Vector2.zero;
            Look = Vector2.zero;
            IsRunHeld = false;
            if (changed)
            {
                MovementInputChanged?.Invoke(this);
            }
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
