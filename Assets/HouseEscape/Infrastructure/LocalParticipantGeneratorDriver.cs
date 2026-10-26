using System;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Generators;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    /// <summary>
    /// Turns one local participant's south-button hold into generator begin/release intentions
    /// (the press itself is routed by <see cref="LocalParticipantInteractionDriver"/>, which picks
    /// the aimed target) and blocks that participant's walk input while the server reports an
    /// attempt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalParticipantGeneratorDriver : MonoBehaviour
    {
        [SerializeField] private LocalParticipantContext context;
        [SerializeField] private LocalParticipantAvatarDriver avatarDriver;
        [SerializeField] private NetworkGeneratorRace generatorRace;

        private int _requestedGeneratorNumber;

        public LocalParticipantContext Context => context;
        public int RequestedGeneratorNumber => _requestedGeneratorNumber;

        public void Initialize(
            LocalParticipantContext explicitContext,
            LocalParticipantAvatarDriver explicitAvatarDriver,
            NetworkGeneratorRace explicitGeneratorRace)
        {
            context = explicitContext != null
                ? explicitContext
                : throw new ArgumentNullException(nameof(explicitContext));
            avatarDriver = explicitAvatarDriver != null
                ? explicitAvatarDriver
                : throw new ArgumentNullException(nameof(explicitAvatarDriver));
            generatorRace = explicitGeneratorRace != null
                ? explicitGeneratorRace
                : throw new ArgumentNullException(nameof(explicitGeneratorRace));
            if (avatarDriver.Context != context)
            {
                throw new ArgumentException("The avatar driver must belong to the same participant context.", nameof(explicitAvatarDriver));
            }
        }

        private void Update()
        {
            if (avatarDriver == null || generatorRace == null || context == null)
            {
                return;
            }

            bool attempting = generatorRace.IsSpawned
                && context.ParticipantId.IsValid
                && generatorRace.TryGetAttempt(context.ParticipantId, out _, out _);
            avatarDriver.SetMovementBlocked(LocalMovementBlock.Generator, attempting);
        }

        /// <summary>
        /// The generator a press would target now (nearest inactive one in range and aimed at),
        /// with its aim error for comparison against other targets. False while incapacitated or
        /// already attempting.
        /// </summary>
        public bool TryFindTarget(out int generatorNumber, out float aimError)
        {
            generatorNumber = 0;
            aimError = float.PositiveInfinity;

            // Unconscious or eliminated participants cannot act; the server rejects it anyway.
            if (!generatorRace.IsSpawned || !context.ParticipantId.IsValid || avatarDriver.IsIncapacitated)
            {
                return false;
            }

            // A previous request the server rejected (or already ended) must not block a new
            // press; only a live replicated attempt does.
            if (generatorRace.TryGetAttempt(context.ParticipantId, out _, out _))
            {
                return false;
            }

            NetworkParticipantAvatar avatar = avatarDriver.BoundAvatar;
            if (avatar == null)
            {
                return false;
            }

            // The participant camera is attached to the avatar's view anchor, so this is the
            // participant's own view. The server repeats range and aim checks authoritatively.
            Transform view = context.ParticipantCamera.transform;
            if (!generatorRace.TryFindTarget(avatar.transform.position, view.position, view.forward, out generatorNumber)
                || !generatorRace.TryGetGenerator(generatorNumber, out NetworkGenerator generator))
            {
                generatorNumber = 0;
                return false;
            }

            aimError = GeneratorInteractionQuery.GetAimError(generator, view.position, view.forward);
            return true;
        }

        /// <summary>Sends the begin intention; released by <see cref="ReleaseRequested"/>.</summary>
        public void BeginAttempt(int generatorNumber)
        {
            _requestedGeneratorNumber = generatorNumber;
            generatorRace.RequestBeginAttempt(context.ParticipantId, generatorNumber);
        }

        public void ReleaseRequested()
        {
            if (_requestedGeneratorNumber == 0)
            {
                return;
            }

            int generatorNumber = _requestedGeneratorNumber;
            _requestedGeneratorNumber = 0;
            if (generatorRace != null && generatorRace.IsSpawned && context.ParticipantId.IsValid)
            {
                generatorRace.RequestReleaseAttempt(context.ParticipantId, generatorNumber);
            }
        }

        private void OnDestroy()
        {
            ReleaseRequested();
        }
    }
}
