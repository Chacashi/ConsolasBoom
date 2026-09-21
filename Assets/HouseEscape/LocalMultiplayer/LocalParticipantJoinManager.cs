using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HouseEscape.LocalMultiplayer
{
    [DisallowMultipleComponent]
    public sealed class LocalParticipantJoinManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset localPlayerActions;
        [SerializeField] private LocalParticipantContext[] participantContexts = Array.Empty<LocalParticipantContext>();

        private readonly HashSet<int> _pressedJoinDevices = new HashSet<int>();
        private readonly Queue<Gamepad> _pendingJoinDevices = new Queue<Gamepad>();
        private bool _subscribed;

        public bool IsJoiningOpen { get; private set; }
        public int ReservedCount { get; private set; }
        public ulong OwnerClientId { get; private set; }
        public IReadOnlyList<LocalParticipantContext> Contexts => participantContexts;

        public event Action<byte> ParticipantRegistrationRequested;
        public event Action MatchStartRequested;
        public event Action<LocalParticipantContext> ContextReserved;

        public void Initialize(InputActionAsset actions, LocalParticipantContext[] contexts)
        {
            localPlayerActions = actions != null ? actions : throw new ArgumentNullException(nameof(actions));
            participantContexts = contexts != null ? contexts : throw new ArgumentNullException(nameof(contexts));
            ValidateContexts();
            if (Application.isPlaying)
            {
                Subscribe();
            }
        }

        public void OpenJoining(ulong ownerClientId)
        {
            if (localPlayerActions == null || participantContexts.Length != ParticipantRegistry.MaximumParticipants)
            {
                throw new InvalidOperationException("The join manager must be initialized with four participant contexts.");
            }

            OwnerClientId = ownerClientId;
            IsJoiningOpen = true;
        }

        public void CloseJoining()
        {
            IsJoiningOpen = false;
            _pressedJoinDevices.Clear();
            _pendingJoinDevices.Clear();
        }

        public void ConfirmParticipant(byte localSlot, ParticipantId participantId)
        {
            GetContext(localSlot).BindParticipant(participantId);
        }

        private void Awake()
        {
            if (localPlayerActions != null && participantContexts.Length > 0)
            {
                ValidateContexts();
                Subscribe();
            }
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                InputSystem.onEvent -= HandleInputEvent;
                InputSystem.onDeviceChange -= HandleDeviceChange;
                _subscribed = false;
            }

            foreach (LocalParticipantContext context in participantContexts)
            {
                if (context != null)
                {
                    context.StartMatchPerformed -= HandleStartMatchPerformed;
                }
            }
        }

        private void Update()
        {
            while (IsJoiningOpen
                && ReservedCount < ParticipantRegistry.MaximumParticipants
                && _pendingJoinDevices.Count > 0)
            {
                Gamepad gamepad = _pendingJoinDevices.Dequeue();
                if (gamepad == null || !gamepad.added || IsDeviceReserved(gamepad))
                {
                    continue;
                }

                LocalParticipantContext context = participantContexts[ReservedCount];
                context.Reserve(gamepad, localPlayerActions);
                ReservedCount++;
                ApplyViewports();
                ContextReserved?.Invoke(context);
                ParticipantRegistrationRequested?.Invoke(context.LocalSlot);
            }
        }

        private void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!IsJoiningOpen || !(device is Gamepad gamepad) || IsDeviceReserved(device))
            {
                return;
            }

            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            bool pressed = gamepad.startButton.ReadValueFromEvent(eventPtr) >= InputSystem.settings.defaultButtonPressPoint;
            if (!pressed)
            {
                _pressedJoinDevices.Remove(device.deviceId);
                return;
            }

            if (!_pressedJoinDevices.Add(device.deviceId) || ReservedCount >= ParticipantRegistry.MaximumParticipants)
            {
                return;
            }

            _pendingJoinDevices.Enqueue(gamepad);
        }

        private void HandleStartMatchPerformed(LocalParticipantContext context)
        {
            if (IsJoiningOpen && ReservedCount > 0 && context.LocalSlot == 0)
            {
                MatchStartRequested?.Invoke();
            }
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            for (int index = 0; index < ReservedCount; index++)
            {
                LocalParticipantContext context = participantContexts[index];
                if (!context.IsPairedTo(device))
                {
                    continue;
                }

                if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
                {
                    context.MarkDeviceDisconnected();
                }
                else if (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added)
                {
                    context.MarkDeviceReconnected();
                }
            }
        }

        private bool IsDeviceReserved(InputDevice device)
        {
            for (int index = 0; index < ReservedCount; index++)
            {
                if (participantContexts[index].IsPairedTo(device))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyViewports()
        {
            for (int index = 0; index < ReservedCount; index++)
            {
                participantContexts[index].SetViewport(
                    LocalParticipantViewportLayout.GetViewport(ReservedCount, index));
            }
        }

        private LocalParticipantContext GetContext(byte localSlot)
        {
            for (int index = 0; index < participantContexts.Length; index++)
            {
                if (participantContexts[index].LocalSlot == localSlot)
                {
                    return participantContexts[index];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(localSlot));
        }

        private void ValidateContexts()
        {
            if (participantContexts.Length != ParticipantRegistry.MaximumParticipants)
            {
                throw new InvalidOperationException("Exactly four prepared local participant contexts are required.");
            }

            for (int index = 0; index < participantContexts.Length; index++)
            {
                if (participantContexts[index] == null || participantContexts[index].LocalSlot != index)
                {
                    throw new InvalidOperationException("Participant contexts must explicitly cover local slots 0 through 3 in order.");
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            InputSystem.onEvent += HandleInputEvent;
            InputSystem.onDeviceChange += HandleDeviceChange;
            foreach (LocalParticipantContext context in participantContexts)
            {
                context.StartMatchPerformed += HandleStartMatchPerformed;
            }

            _subscribed = true;
        }
    }
}
