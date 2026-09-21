using System;
using System.Collections;
using HouseEscape.Core.Participants;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using HouseEscape.Presentation.Participants;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    [DisallowMultipleComponent]
    public sealed class LocalParticipantComposition : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private LocalParticipantJoinManager joinManager;
        [SerializeField] private LocalParticipantContext[] participantContexts = Array.Empty<LocalParticipantContext>();
        [SerializeField] private LocalParticipantHudView[] participantHuds = Array.Empty<LocalParticipantHudView>();
        [SerializeField] private Camera joiningCamera;
        [SerializeField] private Canvas joiningCanvas;
        [SerializeField] private AudioListener sharedAudioListener;

        private bool _initialized;

        public void Initialize(
            NetworkManager explicitNetworkManager,
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkMatchTimeline explicitMatchTimeline,
            LocalParticipantJoinManager explicitJoinManager,
            LocalParticipantContext[] explicitContexts,
            LocalParticipantHudView[] explicitHuds,
            Camera explicitJoiningCamera,
            Canvas explicitJoiningCanvas,
            AudioListener explicitAudioListener)
        {
            networkManager = explicitNetworkManager;
            participantRegistry = explicitParticipantRegistry;
            matchTimeline = explicitMatchTimeline;
            joinManager = explicitJoinManager;
            participantContexts = explicitContexts;
            participantHuds = explicitHuds;
            joiningCamera = explicitJoiningCamera;
            joiningCanvas = explicitJoiningCanvas;
            sharedAudioListener = explicitAudioListener;
            ValidateDependencies();
        }

        private IEnumerator Start()
        {
            ValidateDependencies();
            Subscribe();

            while (networkManager != null
                && (!networkManager.IsListening
                    || !participantRegistry.IsSpawned
                    || !matchTimeline.IsSpawned))
            {
                yield return null;
            }

            if (networkManager == null || !networkManager.IsClient)
            {
                Debug.LogError("Local participant composition requires an active client connection.", this);
                yield break;
            }

            if (!networkManager.IsServer)
            {
                Debug.LogWarning("Remote-console participant registration is deferred; local joining is disabled on clients.", this);
                yield break;
            }

            joinManager.OpenJoining(networkManager.LocalClientId);
            SynchronizeParticipantBindings();
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            joinManager.ParticipantRegistrationRequested -= HandleRegistrationRequested;
            joinManager.MatchStartRequested -= HandleMatchStartRequested;
            joinManager.ContextReserved -= HandleContextReserved;
            participantRegistry.ParticipantsChanged -= SynchronizeParticipantBindings;
            foreach (LocalParticipantContext context in participantContexts)
            {
                context.ConnectionChanged -= HandleConnectionChanged;
            }
        }

        private void HandleRegistrationRequested(byte localSlot)
        {
            participantRegistry.RequestLocalParticipantRegistration(localSlot);
        }

        private void HandleMatchStartRequested()
        {
            if (!networkManager.IsServer)
            {
                Debug.LogError("The current local-only milestone can only start its authoritative timeline on the host.", this);
                return;
            }

            joinManager.CloseJoining();
            participantRegistry.CloseRegistrationOnServer();
            matchTimeline.BeginMatchOnServer();
            joiningCamera.enabled = false;
            joiningCanvas.enabled = false;
        }

        private void HandleContextReserved(LocalParticipantContext context)
        {
            participantHuds[context.LocalSlot].Prepare(context.LocalSlot);
            if (context.LocalSlot == 0)
            {
                sharedAudioListener.transform.SetParent(context.ParticipantCamera.transform, false);
            }
        }

        private void HandleConnectionChanged(LocalParticipantContext context, bool connected)
        {
            participantHuds[context.LocalSlot].SetReconnectionRequired(!connected);
        }

        private void SynchronizeParticipantBindings()
        {
            if (networkManager == null)
            {
                return;
            }

            for (byte slot = 0; slot < participantContexts.Length; slot++)
            {
                LocalParticipantContext context = participantContexts[slot];
                if (!context.IsReserved
                    || context.ParticipantId.IsValid
                    || !participantRegistry.TryGetParticipant(networkManager.LocalClientId, slot, out ParticipantRecord record))
                {
                    continue;
                }

                joinManager.ConfirmParticipant(slot, record.Id);
                participantHuds[slot].Bind(record.Id, slot);
            }
        }

        private void Subscribe()
        {
            if (_initialized)
            {
                return;
            }

            joinManager.ParticipantRegistrationRequested += HandleRegistrationRequested;
            joinManager.MatchStartRequested += HandleMatchStartRequested;
            joinManager.ContextReserved += HandleContextReserved;
            participantRegistry.ParticipantsChanged += SynchronizeParticipantBindings;
            foreach (LocalParticipantContext context in participantContexts)
            {
                context.ConnectionChanged += HandleConnectionChanged;
            }

            _initialized = true;
        }

        private void ValidateDependencies()
        {
            if (networkManager == null
                || participantRegistry == null
                || matchTimeline == null
                || joinManager == null
                || joiningCamera == null
                || joiningCanvas == null
                || sharedAudioListener == null)
            {
                throw new InvalidOperationException("Local participant composition requires explicit network, timeline, join and audio dependencies.");
            }

            if (participantContexts == null
                || participantHuds == null
                || participantContexts.Length != ParticipantRegistry.MaximumParticipants
                || participantHuds.Length != ParticipantRegistry.MaximumParticipants)
            {
                throw new InvalidOperationException("Local participant composition requires four explicit contexts and four explicit HUDs.");
            }
        }
    }
}
