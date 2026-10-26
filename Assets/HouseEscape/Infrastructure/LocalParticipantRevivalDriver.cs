using System;
using HouseEscape.Core.Participants;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Vitality;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Turns one local participant's south-button hold on an unconscious teammate into revival
    /// begin/release intentions (the press is routed by <see cref="LocalParticipantInteractionDriver"/>)
    /// and blocks that participant's walk input while the server reports its revival hold.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantRevivalDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkParticipantRevival revival;

        private bool _requested;

        public LocalParticipantContext Context => context;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkParticipantRevival explicitRevival)
        {
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            revival = explicitRevival != null
                ? explicitRevival
                : throw new ArgumentNullException(nameof(explicitRevival));
            if (avatarDriver.Context != context)
            {
                throw new ArgumentException("The avatar driver must belong to the same participant context.", nameof(explicitAvatarDriver));
            }
        }

        private void Update()
        {
            if (avatarDriver == null || revival == null || context == null)
            {
                return;
            }

            bool reviving = revival.IsSpawned
                && context.ParticipantId.IsValid
                && revival.TryGetRevivalByReviver(context.ParticipantId, out _, out _);
            avatarDriver.SetMovementBlocked(LocalMovementBlock.Revival, reviving);
        }

        /// <summary>
        /// The unconscious teammate a press would target now, with its aim error. False while
        /// incapacitated, already reviving or without a first-aid kit.
        /// </summary>
        public bool TryFindTarget(out ParticipantId targetId, out float aimError)
        {
            targetId = ParticipantId.None;
            aimError = float.PositiveInfinity;
            NetworkParticipantAvatar avatar = avatarDriver.BoundAvatar;
            if (!revival.IsSpawned
                || !context.ParticipantId.IsValid
                || avatar == null
                || avatarDriver.IsIncapacitated
                || revival.TryGetRevivalByReviver(context.ParticipantId, out _, out _))
            {
                return false;
            }

            Transform view = context.ParticipantCamera.transform;
            return revival.TryFindTarget(
                context.ParticipantId,
                avatar.transform.position,
                view.position,
                view.forward,
                out targetId,
                out aimError);
        }

        /// <summary>Sends the begin intention; released by <see cref="ReleaseRequested"/>.</summary>
        public void BeginRevival(ParticipantId targetId)
        {
            _requested = true;
            revival.RequestBeginRevival(context.ParticipantId, targetId);
        }

        public void ReleaseRequested()
        {
            if (!_requested)
            {
                return;
            }

            _requested = false;
            if (revival != null && revival.IsSpawned && context.ParticipantId.IsValid)
            {
                revival.RequestReleaseRevival(context.ParticipantId);
            }
        }

        private void OnDestroy()
        {
            ReleaseRequested();
        }
    }
}
