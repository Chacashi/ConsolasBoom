using System;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    [DisallowMultipleComponent]
    public sealed class LocalParticipantAvatarDriver : MonoBehaviour
    {
        public const float CommandsPerSecond = 20f;

        [SerializeField] private LocalParticipantContext context;

        private NetworkParticipantAvatar _avatar;
        private Transform _cameraOriginalParent;
        private double _nextSendTime;
        private uint _sequence;
        private bool _subscribed;
        private bool _sendPending;
        private LocalMovementBlock _movementBlocks;
        private bool _incapacitated;

        public LocalParticipantContext Context => context;
        public NetworkParticipantAvatar BoundAvatar => _avatar;
        public bool IsMovementBlocked => _movementBlocks != LocalMovementBlock.None || _incapacitated;

        /// <summary>Local mirror of the replicated condition: unconscious or eliminated participants cannot act.</summary>
        public bool IsIncapacitated => _incapacitated;

        public void Initialize(LocalParticipantContext explicitContext)
        {
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            Subscribe();
        }

        /// <summary>
        /// Suppresses walk input locally (look is kept) while a hold interaction of the given
        /// source (generator attempt, revival) is in progress. The server enforces the same block
        /// authoritatively; this only avoids sending input that would be discarded.
        /// </summary>
        public void SetMovementBlocked(LocalMovementBlock source, bool blocked)
        {
            LocalMovementBlock next = blocked ? _movementBlocks | source : _movementBlocks & ~source;
            if (_movementBlocks == next)
            {
                return;
            }

            _movementBlocks = next;
            _sendPending = true;
        }

        /// <summary>
        /// Suppresses walk input locally while the participant is not conscious (look is kept).
        /// The server enforces the block authoritatively.
        /// </summary>
        public void SetIncapacitated(bool incapacitated)
        {
            if (_incapacitated == incapacitated)
            {
                return;
            }

            _incapacitated = incapacitated;
            _sendPending = true;
        }

        public bool TryBind(NetworkParticipantAvatar avatar)
        {
            if (context == null
                || avatar == null
                || !context.ParticipantId.IsValid
                || avatar.ParticipantId != context.ParticipantId)
            {
                return false;
            }

            if (_avatar == avatar)
            {
                return true;
            }

            Unbind();
            _avatar = avatar;
            _avatar.Despawning += HandleAvatarDespawning;
            Transform cameraTransform = context.ParticipantCamera.transform;
            cameraTransform.SetParent(avatar.ViewAnchor, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            _nextSendTime = Time.unscaledTimeAsDouble;
            SendCurrentInput(_nextSendTime);
            return true;
        }

        public void Unbind()
        {
            Release(true);
        }

        private void Release(bool restoreCamera)
        {
            if (_avatar == null)
            {
                return;
            }

            _avatar.Despawning -= HandleAvatarDespawning;
            if (restoreCamera && context != null && context.ParticipantCamera != null)
            {
                // Keep the world pose so an eliminated participant keeps its last view until a
                // spectator target is attached (or permanently, when no teammate is alive).
                context.ParticipantCamera.transform.SetParent(_cameraOriginalParent, true);
            }

            _avatar = null;
        }

        private void Awake()
        {
            if (context != null)
            {
                Subscribe();
            }
        }

        private void Update()
        {
            if (_avatar == null)
            {
                return;
            }

            if (_avatar.ParticipantId != context.ParticipantId)
            {
                Unbind();
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            if (now < _nextSendTime
                || (!_sendPending && EffectiveMove == Vector2.zero && context.Look == Vector2.zero))
            {
                return;
            }

            SendCurrentInput(now);
        }

        private void HandleMovementInputChanged(LocalParticipantContext changedContext)
        {
            if (_avatar != null && changedContext == context)
            {
                double now = Time.unscaledTimeAsDouble;
                if (now >= _nextSendTime)
                {
                    SendCurrentInput(now);
                }
                else
                {
                    _sendPending = true;
                }
            }
        }

        private void SendCurrentInput(double now)
        {
            _nextSendTime = now + 1d / CommandsPerSecond;
            _sendPending = false;
            _avatar.SubmitInput(++_sequence, EffectiveMove, context.Look, EffectiveRun);
        }

        private Vector2 EffectiveMove => IsMovementBlocked ? Vector2.zero : context.Move;

        /// <summary>Run intention only; the server decides from energy and movement whether it applies.</summary>
        private bool EffectiveRun => !IsMovementBlocked && context.IsRunHeld;

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _cameraOriginalParent = context.ParticipantCamera.transform.parent;
            context.MovementInputChanged += HandleMovementInputChanged;
            _subscribed = true;
        }

        private void HandleAvatarDespawning(NetworkParticipantAvatar avatar)
        {
            if (_avatar == avatar)
            {
                Unbind();
            }
        }

        private void OnDestroy()
        {
            // During teardown the original camera parent may itself be destroyed; do not reparent.
            Release(false);
            if (_subscribed && context != null)
            {
                context.MovementInputChanged -= HandleMovementInputChanged;
                _subscribed = false;
            }
        }
    }
}
