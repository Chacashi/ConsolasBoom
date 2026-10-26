using System;
using HouseEscape.Core.Participants;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Items;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Routes one local participant's primary south face button to whatever it is aiming at
    /// (GDD 3.4): an inactive generator (hold to activate), a map item (instant pickup), an
    /// unconscious teammate (hold to revive with a first-aid kit, GDD 7), a flashlight charging
    /// station (hold to recharge, GDD 3.2) or a hiding place (GDD 3.6). While hidden, the button
    /// leaves the hiding place instead. When several targets
    /// qualify, the one closest to the centre of the view wins. Releasing the button releases any
    /// hold. The server validates every intention.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantInteractionDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private LocalParticipantGeneratorDriver generatorDriver;
        [SerializeField] private LocalParticipantRevivalDriver revivalDriver;
        [SerializeField] private LocalParticipantFlashlightDriver flashlightDriver;
        [SerializeField] private LocalParticipantHidingDriver hidingDriver;
        [SerializeField] private NetworkMapItemField mapItemField;

        private bool _subscribed;

        public LocalParticipantContext Context => context;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            LocalParticipantGeneratorDriver explicitGeneratorDriver,
            LocalParticipantRevivalDriver explicitRevivalDriver,
            LocalParticipantFlashlightDriver explicitFlashlightDriver,
            LocalParticipantHidingDriver explicitHidingDriver,
            NetworkMapItemField explicitMapItemField)
        {
            Unsubscribe();
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            generatorDriver = explicitGeneratorDriver != null
                ? explicitGeneratorDriver
                : throw new ArgumentNullException(nameof(explicitGeneratorDriver));
            revivalDriver = explicitRevivalDriver != null
                ? explicitRevivalDriver
                : throw new ArgumentNullException(nameof(explicitRevivalDriver));
            flashlightDriver = explicitFlashlightDriver != null
                ? explicitFlashlightDriver
                : throw new ArgumentNullException(nameof(explicitFlashlightDriver));
            hidingDriver = explicitHidingDriver != null
                ? explicitHidingDriver
                : throw new ArgumentNullException(nameof(explicitHidingDriver));
            mapItemField = explicitMapItemField != null
                ? explicitMapItemField
                : throw new ArgumentNullException(nameof(explicitMapItemField));
            if (avatarDriver.Context != context || generatorDriver.Context != context || revivalDriver.Context != context
                || flashlightDriver.Context != context
                || hidingDriver.Context != context)
            {
                throw new ArgumentException("Every driver must belong to the same participant context.", nameof(explicitGeneratorDriver));
            }

            if (Application.isPlaying)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            if (context != null && avatarDriver != null && generatorDriver != null && revivalDriver != null && flashlightDriver != null && hidingDriver != null && mapItemField != null)
            {
                Subscribe();
            }
        }

        private void HandleInteractHeldChanged(LocalParticipantContext changedContext, bool held)
        {
            if (changedContext != context)
            {
                return;
            }

            if (held)
            {
                Press();
            }
            else
            {
                generatorDriver.ReleaseRequested();
                revivalDriver.ReleaseRequested();
                flashlightDriver.ReleaseChargingRequested();
                hidingDriver.ReleaseRequested();
            }
        }

        private void Press()
        {
            NetworkParticipantAvatar avatar = avatarDriver.BoundAvatar;
            if (avatar == null || avatarDriver.IsIncapacitated || !context.ParticipantId.IsValid)
            {
                return;
            }

            if (hidingDriver.IsHidden)
            {
                hidingDriver.Leave();
                return;
            }

            bool hasGenerator = generatorDriver.TryFindTarget(out int generatorNumber, out float generatorAimError);
            Transform view = context.ParticipantCamera.transform;
            NetworkMapItem item = null;
            float itemAimError = float.PositiveInfinity;
            bool hasItem = mapItemField.IsSpawned
                && mapItemField.TryFindTarget(avatar.transform.position, view.position, view.forward, out item, out itemAimError)
                && (!hasGenerator || itemAimError < generatorAimError);
            float bestAimError = hasItem ? itemAimError : hasGenerator ? generatorAimError : float.PositiveInfinity;
            bool hasTeammate = revivalDriver.TryFindTarget(out ParticipantId teammateId, out float teammateAimError)
                && teammateAimError < bestAimError;
            bestAimError = hasTeammate ? teammateAimError : bestAimError;
            bool hasStation = flashlightDriver.TryFindStation(out int stationIndex, out float stationAimError)
                && stationAimError < bestAimError;
            bestAimError = hasStation ? stationAimError : bestAimError;
            bool hasSpot = hidingDriver.TryFindSpot(out int spotIndex, out float spotAimError)
                && spotAimError < bestAimError;

            if (hasSpot)
            {
                hidingDriver.BeginHiding(spotIndex);
            }
            else if (hasStation)
            {
                flashlightDriver.BeginCharging(stationIndex);
            }
            else if (hasTeammate)
            {
                revivalDriver.BeginRevival(teammateId);
            }
            else if (hasItem)
            {
                mapItemField.RequestPickup(context.ParticipantId, item);
            }
            else if (hasGenerator)
            {
                generatorDriver.BeginAttempt(generatorNumber);
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            context.InteractHeldChanged += HandleInteractHeldChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && context != null)
            {
                context.InteractHeldChanged -= HandleInteractHeldChanged;
            }

            _subscribed = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
