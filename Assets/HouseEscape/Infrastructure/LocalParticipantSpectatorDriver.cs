using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Vitality;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Local presentation for one participant's replicated condition. While the participant is
    /// not conscious it suppresses local walk/act input. Once permanently eliminated (and its own
    /// avatar has released the camera) it attaches the camera to a living teammate's view
    /// anchor; the paired gamepad's south face button cycles to the next living teammate. With no
    /// living teammate the camera keeps its last view. Avatars are resolved through the per-peer
    /// <see cref="NetworkParticipantAvatarDirectory"/>. While conscious, low energy narrows the
    /// camera field of view and makes it sway (<see cref="EnergyCameraEffects"/>, local only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantSpectatorDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private NetworkParticipantAvatarDirectory avatarDirectory;
        [SerializeField] private ParticipantVitalityHudView vitalityHud;

        private readonly List<ParticipantVitality> _participants = new List<ParticipantVitality>(ParticipantRegistry.MaximumParticipants);

        private NetworkParticipantAvatar _spectatedAvatar;
        private ParticipantId _spectatedId;
        private Transform _cameraOriginalParent;
        private bool _subscribed;
        private bool _swayApplied;

        public LocalParticipantContext Context => context;
        public ParticipantId SpectatedParticipantId => _spectatedAvatar != null ? _spectatedId : ParticipantId.None;

        /// <summary>True once the replicated record says this local participant is permanently eliminated.</summary>
        public bool IsEliminated => TryGetOwnVitality(out ParticipantVitality own) && own.State == VitalityState.Eliminated;
        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkParticipantVitality explicitVitality,
            NetworkParticipantAvatarDirectory explicitAvatarDirectory,
            ParticipantVitalityHudView explicitVitalityHud)
        {
            Unsubscribe();
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            vitality = explicitVitality != null
                ? explicitVitality
                : throw new ArgumentNullException(nameof(explicitVitality));
            avatarDirectory = explicitAvatarDirectory != null
                ? explicitAvatarDirectory
                : throw new ArgumentNullException(nameof(explicitAvatarDirectory));
            vitalityHud = explicitVitalityHud != null
                ? explicitVitalityHud
                : throw new ArgumentNullException(nameof(explicitVitalityHud));
            if (avatarDriver.Context != context)
            {
                throw new ArgumentException("The avatar driver must belong to the same participant context.", nameof(explicitAvatarDriver));
            }

            if (Application.isPlaying)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            if (context != null && avatarDriver != null && vitality != null && avatarDirectory != null && vitalityHud != null)
            {
                Subscribe();
            }
        }

        private void Update()
        {
            if (context == null || avatarDriver == null)
            {
                return;
            }

            bool known = TryGetOwnVitality(out ParticipantVitality own);
            VitalityState state = own.State;
            avatarDriver.SetIncapacitated(known && state != VitalityState.Conscious);
            ApplyEnergyEffects(known, own);

            if (!CanSpectate(known, own))
            {
                if (_spectatedAvatar != null)
                {
                    StopSpectating(false, true);
                }

                return;
            }

            Refresh();
            if (_spectatedAvatar != null
                && SpectatorTargetSelector.IsValidTarget(_participants, context.ParticipantId, _spectatedId))
            {
                return;
            }

            SpectateFirstAvailable(SpectatorTargetSelector.Resolve(_participants, context.ParticipantId, _spectatedId));
        }

        private void HandleSpectateNextPerformed(LocalParticipantContext changedContext)
        {
            if (changedContext != context)
            {
                return;
            }

            bool known = TryGetOwnVitality(out ParticipantVitality own);
            if (!CanSpectate(known, own))
            {
                return;
            }

            SpectateFirstAvailable(SpectatorTargetSelector.Next(Refresh(), context.ParticipantId, _spectatedId));
        }

        private bool TryGetOwnVitality(out ParticipantVitality own)
        {
            own = default;
            return vitality != null
                && vitality.IsSpawned
                && context != null
                && context.ParticipantId.IsValid
                && vitality.TryGetVitality(context.ParticipantId, out own);
        }

        /// <summary>
        /// Field of view and sway only while viewing through the own avatar; the sway is reset to
        /// the view anchor's identity pose as soon as it stops.
        /// </summary>
        private void ApplyEnergyEffects(bool known, ParticipantVitality own)
        {
            Camera participantCamera = context.ParticipantCamera;
            NetworkParticipantAvatar ownAvatar = avatarDriver.BoundAvatar;
            bool viewingOwnAvatar = known
                && ownAvatar != null
                && participantCamera.transform.parent == ownAvatar.ViewAnchor;
            VitalityState state = viewingOwnAvatar ? own.State : VitalityState.Eliminated;

            float fieldOfView = EnergyCameraEffects.GetFieldOfView(state, own.WholeEnergy);
            if (!Mathf.Approximately(participantCamera.fieldOfView, fieldOfView))
            {
                participantCamera.fieldOfView = fieldOfView;
            }

            bool swaying = EnergyCameraEffects.IsSwaying(state, own.WholeEnergy);
            if (swaying)
            {
                participantCamera.transform.localRotation =
                    EnergyCameraEffects.GetSway(state, own.WholeEnergy, Time.unscaledTimeAsDouble);
            }
            else if (_swayApplied && viewingOwnAvatar)
            {
                participantCamera.transform.localRotation = Quaternion.identity;
            }

            _swayApplied = swaying;
        }

        /// <summary>Only once eliminated or escaped and after the own avatar released the camera.</summary>
        private bool CanSpectate(bool known, ParticipantVitality own)
        {
            return known && !own.IsInHouse && avatarDriver.BoundAvatar == null;
        }

        private List<ParticipantVitality> Refresh()
        {
            vitality.GetVitalities(_participants);
            return _participants;
        }

        /// <summary>Tries <paramref name="candidate"/>, then each following living teammate with a spawned avatar.</summary>
        private void SpectateFirstAvailable(ParticipantId candidate)
        {
            for (int attempt = 0; attempt < _participants.Count && candidate.IsValid; attempt++)
            {
                if (TrySpectate(candidate))
                {
                    return;
                }

                candidate = SpectatorTargetSelector.Next(_participants, context.ParticipantId, candidate);
            }

            // No living teammate with an avatar: keep the last view.
        }

        private bool TrySpectate(ParticipantId target)
        {
            if (target == _spectatedId && _spectatedAvatar != null)
            {
                return true;
            }

            if (!avatarDirectory.TryGetAvatar(target, out NetworkParticipantAvatar avatar)
                || avatar.ViewAnchor == null)
            {
                return false;
            }

            StopSpectating(true, true);
            _spectatedId = target;
            _spectatedAvatar = avatar;
            _spectatedAvatar.Despawning += HandleSpectatedAvatarDespawning;
            Transform cameraTransform = context.ParticipantCamera.transform;
            cameraTransform.SetParent(avatar.ViewAnchor, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            vitalityHud.SetSpectatedParticipant(target);
            return true;
        }

        private void HandleSpectatedAvatarDespawning(NetworkParticipantAvatar avatar)
        {
            if (avatar == _spectatedAvatar)
            {
                // Detach (keeping the last view) before the avatar is destroyed; the next Update
                // picks another living teammate.
                StopSpectating(true, true);
            }
        }

        private void StopSpectating(bool keepTargetId, bool restoreCamera)
        {
            if (_spectatedAvatar != null)
            {
                _spectatedAvatar.Despawning -= HandleSpectatedAvatarDespawning;
                if (restoreCamera && context != null && context.ParticipantCamera != null)
                {
                    context.ParticipantCamera.transform.SetParent(_cameraOriginalParent, true);
                }
            }

            _spectatedAvatar = null;
            if (!keepTargetId)
            {
                _spectatedId = ParticipantId.None;
            }

            if (vitalityHud != null)
            {
                vitalityHud.SetSpectatedParticipant(ParticipantId.None);
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _cameraOriginalParent = context.ParticipantCamera.transform.parent;
            context.SpectateNextPerformed += HandleSpectateNextPerformed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && context != null)
            {
                context.SpectateNextPerformed -= HandleSpectateNextPerformed;
            }

            _subscribed = false;
        }

        private void OnDestroy()
        {
            // During teardown the original camera parent may itself be destroyed; do not reparent.
            StopSpectating(false, false);
            Unsubscribe();
        }
    }
}
