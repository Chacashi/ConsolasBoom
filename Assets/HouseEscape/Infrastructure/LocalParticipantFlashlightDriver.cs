using System;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Flashlight;
using HouseEscape.Presentation.Flashlight;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Routes one local participant's right shoulder button into flashlight toggle intentions and
    /// its south-button hold on a charging station into charging begin/release intentions (the
    /// press is routed by <see cref="LocalParticipantInteractionDriver"/>). Blocks walk input while
    /// the server reports the participant charging.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantFlashlightDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkParticipantFlashlights flashlights;
        [SerializeField] private FlashlightHudView flashlightHud;

        private bool _chargingRequested;
        private bool _subscribed;

        public LocalParticipantContext Context => context;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkParticipantFlashlights explicitFlashlights,
            FlashlightHudView explicitFlashlightHud)
        {
            Unsubscribe();
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            flashlights = explicitFlashlights != null
                ? explicitFlashlights
                : throw new ArgumentNullException(nameof(explicitFlashlights));
            flashlightHud = explicitFlashlightHud != null
                ? explicitFlashlightHud
                : throw new ArgumentNullException(nameof(explicitFlashlightHud));
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
            if (context != null && avatarDriver != null && flashlights != null && flashlightHud != null)
            {
                Subscribe();
            }
        }

        private void Update()
        {
            if (avatarDriver == null || flashlights == null || context == null)
            {
                return;
            }

            bool charging = flashlights.IsSpawned
                && context.ParticipantId.IsValid
                && flashlights.IsCharging(context.ParticipantId);
            avatarDriver.SetMovementBlocked(LocalMovementBlock.Charging, charging);

            // Without an avatar (before the match, or once eliminated) there is no flashlight.
            flashlightHud.SetHidden(avatarDriver.BoundAvatar == null);
        }

        /// <summary>The charging station a press would target now, with its aim error.</summary>
        public bool TryFindStation(out int stationIndex, out float aimError)
        {
            stationIndex = -1;
            aimError = float.PositiveInfinity;
            NetworkParticipantAvatar avatar = avatarDriver.BoundAvatar;
            if (!flashlights.IsSpawned
                || !context.ParticipantId.IsValid
                || avatar == null
                || avatarDriver.IsIncapacitated
                || flashlights.IsCharging(context.ParticipantId))
            {
                return false;
            }

            Transform view = context.ParticipantCamera.transform;
            return flashlights.TryFindStation(avatar.transform.position, view.position, view.forward, out stationIndex, out aimError);
        }

        /// <summary>Sends the begin intention; released by <see cref="ReleaseChargingRequested"/>.</summary>
        public void BeginCharging(int stationIndex)
        {
            _chargingRequested = true;
            flashlights.RequestBeginCharging(context.ParticipantId, stationIndex);
        }

        public void ReleaseChargingRequested()
        {
            if (!_chargingRequested)
            {
                return;
            }

            _chargingRequested = false;
            if (flashlights != null && flashlights.IsSpawned && context.ParticipantId.IsValid)
            {
                flashlights.RequestReleaseCharging(context.ParticipantId);
            }
        }

        private void HandleFlashlightTogglePerformed(LocalParticipantContext changedContext)
        {
            if (changedContext != context
                || !flashlights.IsSpawned
                || !context.ParticipantId.IsValid
                || avatarDriver.BoundAvatar == null
                || avatarDriver.IsIncapacitated)
            {
                return;
            }

            flashlights.RequestToggle(context.ParticipantId);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            context.FlashlightTogglePerformed += HandleFlashlightTogglePerformed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && context != null)
            {
                context.FlashlightTogglePerformed -= HandleFlashlightTogglePerformed;
            }

            _subscribed = false;
        }

        private void OnDestroy()
        {
            ReleaseChargingRequested();
            Unsubscribe();
        }
    }
}
