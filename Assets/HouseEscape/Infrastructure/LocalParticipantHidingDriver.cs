using System;
using HouseEscape.Gameplay.Hiding;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Hiding;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Turns one local participant's south-button presses on a hiding place into enter, release
    /// (cancels a held chest entry) and leave intentions; the press itself is routed by
    /// <see cref="LocalParticipantInteractionDriver"/>. Blocks walk input while the server reports
    /// the participant entering, hidden or vulnerable after leaving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantHidingDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkHidingPlaces hidingPlaces;

        private bool _enterRequested;

        public LocalParticipantContext Context => context;

        /// <summary>Replicated: the participant is inside a hiding place.</summary>
        public bool IsHidden => TryGetStatus(out HidingStatus status) && status.Phase == HidingPhase.Hidden;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkHidingPlaces explicitHidingPlaces)
        {
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            hidingPlaces = explicitHidingPlaces != null
                ? explicitHidingPlaces
                : throw new ArgumentNullException(nameof(explicitHidingPlaces));
            if (avatarDriver.Context != context)
            {
                throw new ArgumentException("The avatar driver must belong to the same participant context.", nameof(explicitAvatarDriver));
            }
        }

        private void Update()
        {
            if (avatarDriver == null || hidingPlaces == null || context == null)
            {
                return;
            }

            avatarDriver.SetMovementBlocked(LocalMovementBlock.Hiding, TryGetStatus(out HidingStatus status) && status.BlocksMovement);
        }

        /// <summary>The free hiding place a press would target now, with its aim error.</summary>
        public bool TryFindSpot(out int spotIndex, out float aimError)
        {
            spotIndex = -1;
            aimError = float.PositiveInfinity;
            NetworkParticipantAvatar avatar = avatarDriver.BoundAvatar;
            if (!hidingPlaces.IsSpawned
                || !context.ParticipantId.IsValid
                || avatar == null
                || avatarDriver.IsIncapacitated
                || (TryGetStatus(out HidingStatus status)
                    && (status.Phase != HidingPhase.None || hidingPlaces.SynchronizedTime < status.CooldownEndsAt)))
            {
                return false;
            }

            Transform view = context.ParticipantCamera.transform;
            return hidingPlaces.TryFindSpot(avatar.transform.position, view.position, view.forward, out spotIndex, out aimError);
        }

        public void BeginHiding(int spotIndex)
        {
            _enterRequested = true;
            hidingPlaces.RequestBegin(context.ParticipantId, spotIndex);
        }

        public void Leave()
        {
            if (hidingPlaces.IsSpawned && context.ParticipantId.IsValid)
            {
                hidingPlaces.RequestLeave(context.ParticipantId);
            }
        }

        /// <summary>Releasing the button cancels a held chest entry that has not completed.</summary>
        public void ReleaseRequested()
        {
            if (!_enterRequested)
            {
                return;
            }

            _enterRequested = false;
            if (hidingPlaces != null && hidingPlaces.IsSpawned && context.ParticipantId.IsValid)
            {
                hidingPlaces.RequestRelease(context.ParticipantId);
            }
        }

        private bool TryGetStatus(out HidingStatus status)
        {
            status = default;
            return hidingPlaces.IsSpawned
                && context.ParticipantId.IsValid
                && hidingPlaces.TryGetHiding(context.ParticipantId, out status);
        }

        private void OnDestroy()
        {
            ReleaseRequested();
        }
    }
}
