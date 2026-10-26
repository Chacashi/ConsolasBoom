using System;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Movement;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Avatars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkParticipantAvatar : NetworkBehaviour
    {
        public const double InputTimeoutSeconds = 0.25d;
        public const double MinimumCommandIntervalSeconds = 1d / 30d;

        /// <summary>Downward acceleration applied by the server while the avatar is airborne.</summary>
        public const float GravityMetresPerSecondSquared = -9.81f;

        [SerializeField] private Transform viewAnchor;
        [SerializeField] private Light flashlightBeam;
        [SerializeField] private CharacterController characterController;

        private readonly NetworkVariable<ulong> _participantId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _pitchDegrees = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _flashlightOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _hidden = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly MovementCommandBuffer _commandBuffer = new MovementCommandBuffer();

        private NetworkParticipantRegistry _participantRegistry;
        private NetworkMatchTimeline _matchTimeline;
        private ParticipantRecord _serverParticipant;
        private MovementMotorState _motorState;
        private double _lastSimulationTime;
        private double _lastAcceptedCommandTime = double.NegativeInfinity;
        private bool _movementBlocked;
        private bool _monsterTargetable = true;
        private bool _incapacitated;
        private double _incapacitatedAt = double.PositiveInfinity;
        private float _energy = EnergyRules.StartingEnergy;
        private float _verticalVelocity;
        private bool _running;
        private NetworkParticipantAvatarDirectory _directory;

        public ParticipantId ParticipantId => _participantId.Value == 0
            ? ParticipantId.None
            : new ParticipantId(_participantId.Value);
        public Transform ViewAnchor => viewAnchor;
        public float PitchDegrees => _pitchDegrees.Value;

        /// <summary>Replicated: the beam is visible to every peer while on.</summary>
        public bool IsFlashlightOn => _flashlightOn.Value;

        /// <summary>Replicated: inside a hiding place; the avatar is not rendered on any peer.</summary>
        public bool IsHidden => _hidden.Value;
        public bool IsMovementBlocked => _movementBlocked || _incapacitated;

        /// <summary>
        /// Server-only: whether the monster may target this participant. Defaults to true; the
        /// hiding slice clears it while the participant is hidden. Unconscious participants are
        /// never targetable.
        /// </summary>
        public bool IsMonsterTargetable => _monsterTargetable && !_incapacitated;

        /// <summary>
        /// Server-only: the participant is unconscious and can neither walk nor act (look is kept).
        /// Set by the participant vitality adapter; independent of the generator walk block.
        /// </summary>
        public bool IsIncapacitated => _incapacitated;

        /// <summary>Server-only: server time of the knockout, or positive infinity while able to act.</summary>
        public double IncapacitatedAt => _incapacitatedAt;

        /// <summary>
        /// Server-only: the last simulated step was an accepted run (run requested, enough energy,
        /// actually moving and not blocked). Drives the running energy drain.
        /// </summary>
        public bool IsRunningOnServer => _running;

        /// <summary>Server-only: the authoritative energy used for movement speed.</summary>
        public float EnergyOnServer => _energy;

        public event Action<NetworkParticipantAvatar> Despawning;

        public void ConfigureServer(
            ParticipantRecord participant,
            NetworkParticipantRegistry participantRegistry,
            NetworkMatchTimeline matchTimeline)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("An avatar must be configured before it is spawned.");
            }

            _serverParticipant = participant;
            _participantRegistry = participantRegistry != null
                ? participantRegistry
                : throw new ArgumentNullException(nameof(participantRegistry));
            _matchTimeline = matchTimeline != null
                ? matchTimeline
                : throw new ArgumentNullException(nameof(matchTimeline));
        }

        /// <summary>
        /// Injects the per-scene directory this avatar registers with while spawned. Called by the
        /// server spawner and, on other peers, by the directory's prefab instance handler.
        /// </summary>
        public void AttachDirectory(NetworkParticipantAvatarDirectory directory)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("The avatar directory must be attached before spawning.");
            }

            _directory = directory != null ? directory : throw new ArgumentNullException(nameof(directory));
        }

        public void InitializeViewAnchor(Transform explicitViewAnchor)
        {
            viewAnchor = explicitViewAnchor != null
                ? explicitViewAnchor
                : throw new ArgumentNullException(nameof(explicitViewAnchor));
        }

        /// <summary>
        /// The server moves the avatar through this controller so walls, furniture and floors
        /// collide; on other peers it only acts as the avatar's collider.
        /// </summary>
        public void InitializeCharacterController(CharacterController explicitCharacterController)
        {
            characterController = explicitCharacterController != null
                ? explicitCharacterController
                : throw new ArgumentNullException(nameof(explicitCharacterController));
        }

        public void InitializeFlashlight(Light explicitFlashlightBeam)
        {
            flashlightBeam = explicitFlashlightBeam != null
                ? explicitFlashlightBeam
                : throw new ArgumentNullException(nameof(explicitFlashlightBeam));
        }

        /// <summary>Server-only: set by the flashlight adapter from the authoritative state.</summary>
        public void SetFlashlightOnServer(bool on)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change its flashlight.");
            }

            _flashlightOn.Value = on;
        }

        /// <summary>Server-only: set by the hiding adapter while the participant is hidden.</summary>
        public void SetHiddenOnServer(bool hidden)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change its hidden state.");
            }

            _hidden.Value = hidden;
        }

        public override void OnNetworkSpawn()
        {
            _hidden.OnValueChanged += HandleHiddenChanged;
            ApplyHidden(_hidden.Value);
            _pitchDegrees.OnValueChanged += HandlePitchChanged;
            ApplyPitch(_pitchDegrees.Value);
            _flashlightOn.OnValueChanged += HandleFlashlightChanged;
            ApplyFlashlight(_flashlightOn.Value);

            if (!IsServer)
            {
                RegisterWithDirectory();
                return;
            }

            if (!_serverParticipant.Id.IsValid || _participantRegistry == null || _matchTimeline == null)
            {
                throw new InvalidOperationException("The server avatar was spawned without participant and match dependencies.");
            }

            _participantId.Value = _serverParticipant.Id.Value;

            Vector3 position = transform.position;
            _motorState = new MovementMotorState(
                position.x,
                position.y,
                position.z,
                transform.eulerAngles.y,
                0f);
            _lastSimulationTime = NetworkManager.ServerTime.Time;
            RegisterWithDirectory();
        }

        private void RegisterWithDirectory()
        {
            if (_directory == null)
            {
                Debug.LogError($"{nameof(NetworkParticipantAvatar)} spawned without an injected avatar directory.", this);
                return;
            }

            _directory.Register(this);
        }

        /// <summary>
        /// Server-only: blocks walking while keeping look input, e.g. during a generator attempt.
        /// </summary>
        public void SetMovementBlockedOnServer(bool blocked)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change its movement block.");
            }

            _movementBlocked = blocked;
        }

        /// <summary>Server-only: hidden participants are never valid monster targets.</summary>
        public void SetMonsterTargetableOnServer(bool targetable)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change monster targetability.");
            }

            _monsterTargetable = targetable;
        }

        /// <summary>Server-only: blocks walking and acting while the participant is unconscious.</summary>
        public void SetIncapacitatedOnServer(bool incapacitated)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change its incapacitation.");
            }

            if (_incapacitated == incapacitated)
            {
                return;
            }

            _incapacitated = incapacitated;
            _incapacitatedAt = incapacitated ? NetworkManager.ServerTime.Time : double.PositiveInfinity;
        }

        /// <summary>
        /// Server-only: the participant's authoritative energy, pushed by the vitality adapter.
        /// Speed brackets and the ability to run are derived from it.
        /// </summary>
        public void SetEnergyOnServer(float energy)
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server avatar can change its energy.");
            }

            _energy = EnergyRules.Clamp(energy);
        }

        public override void OnNetworkDespawn()
        {
            _movementBlocked = false;
            _monsterTargetable = true;
            _incapacitated = false;
            _incapacitatedAt = double.PositiveInfinity;
            _energy = EnergyRules.StartingEnergy;
            _running = false;
            if (_directory != null)
            {
                _directory.Unregister(this);
            }

            Despawning?.Invoke(this);
            _pitchDegrees.OnValueChanged -= HandlePitchChanged;
            _flashlightOn.OnValueChanged -= HandleFlashlightChanged;
            ApplyFlashlight(false);
            _hidden.OnValueChanged -= HandleHiddenChanged;
            ApplyHidden(false);
        }

        private void HandleHiddenChanged(bool previous, bool current)
        {
            ApplyHidden(current);
        }

        private void ApplyHidden(bool hidden)
        {
            foreach (Renderer avatarRenderer in GetComponentsInChildren<Renderer>(true))
            {
                avatarRenderer.enabled = !hidden;
            }
        }

        private void HandleFlashlightChanged(bool previous, bool current)
        {
            ApplyFlashlight(current);
        }

        private void ApplyFlashlight(bool on)
        {
            if (flashlightBeam != null && flashlightBeam.enabled != on)
            {
                flashlightBeam.enabled = on;
            }
        }

        /// <param name="runRequested">Run intention (held L3). The server validates energy and movement.</param>
        public void SubmitInput(uint sequence, Vector2 move, Vector2 look, bool runRequested)
        {
            if (!IsSpawned || !IsClient)
            {
                return;
            }

            SubmitInputRpc(_participantId.Value, sequence, move.x, move.y, look.x, look.y, runRequested);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputRpc(
            ulong requestedParticipantId,
            uint sequence,
            float moveX,
            float moveY,
            float lookX,
            float lookY,
            bool runRequested,
            RpcParams rpcParams = default)
        {
            if (!IsServer
                || !_matchTimeline.Snapshot.IsStarted
                || _matchTimeline.Snapshot.IsComplete
                || requestedParticipantId == 0
                || requestedParticipantId != _participantId.Value)
            {
                return;
            }

            var participantId = new ParticipantId(requestedParticipantId);
            if (!_participantRegistry.IsParticipantOwnedBy(participantId, rpcParams.Receive.SenderClientId))
            {
                return;
            }

            if (NetworkObject.OwnerClientId != rpcParams.Receive.SenderClientId)
            {
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            if (now - _lastAcceptedCommandTime < MinimumCommandIntervalSeconds)
            {
                return;
            }

            var command = new MovementCommand(
                sequence,
                new MovementInput(moveX, moveY, lookX, lookY, runRequested));
            if (!_commandBuffer.TryAccept(command, now))
            {
                return;
            }

            _lastAcceptedCommandTime = now;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _matchTimeline == null)
            {
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            double elapsed = now - _lastSimulationTime;
            _lastSimulationTime = now;
            if (!_matchTimeline.Snapshot.IsStarted || _matchTimeline.Snapshot.IsComplete || elapsed <= 0d)
            {
                _running = false;
                return;
            }

            float deltaTime = (float)Math.Min(elapsed, 0.1d);
            MovementInput input = _commandBuffer.GetInput(now, InputTimeoutSeconds);
            if (IsMovementBlocked)
            {
                input = new MovementInput(0f, 0f, input.LookX, input.LookY, false);
            }

            // The client only states an intention; running needs energy and actual movement.
            _running = input.IsRunRequested && input.HasMovement && EnergyRules.CanRun(_energy);
            float speed = EnergyRules.GetMovementSpeed(_energy, _running);
            MovementMotorState desired = MovementMotor.Step(_motorState, input, deltaTime, speed);
            transform.rotation = Quaternion.Euler(0f, desired.YawDegrees, 0f);
            Vector3 position = MoveWithCollisions(
                new Vector3(desired.X - _motorState.X, 0f, desired.Z - _motorState.Z),
                deltaTime);
            _motorState = new MovementMotorState(position.x, position.y, position.z, desired.YawDegrees, desired.PitchDegrees);

            if (!Mathf.Approximately(_pitchDegrees.Value, _motorState.PitchDegrees))
            {
                _pitchDegrees.Value = _motorState.PitchDegrees;
                ApplyPitch(_pitchDegrees.Value);
            }
        }

        /// <summary>
        /// Server-only: applies the motor's horizontal displacement plus gravity through the
        /// character controller, so the authoritative position never passes through colliders.
        /// </summary>
        private Vector3 MoveWithCollisions(Vector3 horizontal, float deltaTime)
        {
            if (characterController == null || !characterController.enabled)
            {
                transform.position += horizontal;
                return transform.position;
            }

            _verticalVelocity = characterController.isGrounded
                ? -1f
                : _verticalVelocity + GravityMetresPerSecondSquared * deltaTime;
            characterController.Move(horizontal + Vector3.up * (_verticalVelocity * deltaTime));
            return transform.position;
        }

        private void HandlePitchChanged(float previous, float current)
        {
            ApplyPitch(current);
        }

        private void ApplyPitch(float pitch)
        {
            if (viewAnchor != null)
            {
                viewAnchor.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            }
        }
    }
}
