using System;
using System.Collections;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;
using HouseEscape.LocalMultiplayer;
using HouseEscape.Networking.Avatars;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using HouseEscape.Networking.Vitality;
using HouseEscape.Presentation.Flashlight;
using HouseEscape.Presentation.Generators;
using HouseEscape.Presentation.Hiding;
using HouseEscape.Presentation.Items;
using HouseEscape.Presentation.Participants;
using HouseEscape.Presentation.Vitality;
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
        [SerializeField] private NetworkParticipantAvatarSpawner avatarSpawner;
        [SerializeField] private NetworkParticipantAvatarDirectory avatarDirectory;
        [SerializeField] private NetworkParticipantVitality vitality;
        [SerializeField] private LocalParticipantAvatarDriver[] avatarDrivers = Array.Empty<LocalParticipantAvatarDriver>();
        [SerializeField] private LocalParticipantHudView[] participantHuds = Array.Empty<LocalParticipantHudView>();
        [SerializeField] private GeneratorHudView[] generatorHuds = Array.Empty<GeneratorHudView>();
        [SerializeField] private ParticipantVitalityHudView[] vitalityHuds = Array.Empty<ParticipantVitalityHudView>();
        [SerializeField] private ItemHudView[] itemHuds = Array.Empty<ItemHudView>();
        [SerializeField] private RevivalHudView[] revivalHuds = Array.Empty<RevivalHudView>();
        [SerializeField] private FlashlightHudView[] flashlightHuds = Array.Empty<FlashlightHudView>();
        [SerializeField] private HidingHudView[] hidingHuds = Array.Empty<HidingHudView>();
        [SerializeField] private Camera joiningCamera;
        [SerializeField] private Canvas joiningCanvas;
        [SerializeField] private AudioListener sharedAudioListener;

        private readonly bool[] _slotReserved = new bool[ParticipantRegistry.MaximumParticipants];
        private readonly bool[] _slotEliminated = new bool[ParticipantRegistry.MaximumParticipants];

        private bool _initialized;
        private bool _matchStarted;

        /// <summary>The local slot whose camera currently carries the shared AudioListener, or -1.</summary>
        public int AudioFocusSlot { get; private set; } = -1;

        public void Initialize(
            NetworkManager explicitNetworkManager,
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkMatchTimeline explicitMatchTimeline,
            LocalParticipantJoinManager explicitJoinManager,
            LocalParticipantContext[] explicitContexts,
            NetworkParticipantAvatarSpawner explicitAvatarSpawner,
            NetworkParticipantAvatarDirectory explicitAvatarDirectory,
            NetworkParticipantVitality explicitVitality,
            LocalParticipantAvatarDriver[] explicitAvatarDrivers,
            LocalParticipantHudView[] explicitHuds,
            GeneratorHudView[] explicitGeneratorHuds,
            ParticipantVitalityHudView[] explicitVitalityHuds,
            ItemHudView[] explicitItemHuds,
            RevivalHudView[] explicitRevivalHuds,
            FlashlightHudView[] explicitFlashlightHuds,
            HidingHudView[] explicitHidingHuds,
            Camera explicitJoiningCamera,
            Canvas explicitJoiningCanvas,
            AudioListener explicitAudioListener)
        {
            networkManager = explicitNetworkManager;
            participantRegistry = explicitParticipantRegistry;
            matchTimeline = explicitMatchTimeline;
            joinManager = explicitJoinManager;
            participantContexts = explicitContexts;
            avatarSpawner = explicitAvatarSpawner;
            avatarDirectory = explicitAvatarDirectory;
            vitality = explicitVitality;
            avatarDrivers = explicitAvatarDrivers;
            participantHuds = explicitHuds;
            generatorHuds = explicitGeneratorHuds;
            vitalityHuds = explicitVitalityHuds;
            itemHuds = explicitItemHuds;
            revivalHuds = explicitRevivalHuds;
            flashlightHuds = explicitFlashlightHuds;
            hidingHuds = explicitHidingHuds;
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
            avatarDirectory.AvatarRegistered -= HandleAvatarRegistered;
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
            SynchronizeParticipantBindings();
            avatarSpawner.SpawnRegisteredParticipantsOnServer();
            matchTimeline.BeginMatchOnServer();
            _matchStarted = true;
            BindAvailableAvatars();
            joiningCamera.enabled = false;
            joiningCanvas.enabled = false;
        }

        private void HandleAvatarRegistered(NetworkParticipantAvatar avatar)
        {
            if (!_matchStarted)
            {
                return;
            }

            for (int index = 0; index < avatarDrivers.Length; index++)
            {
                if (avatarDrivers[index].TryBind(avatar))
                {
                    return;
                }
            }
        }

        private void BindAvailableAvatars()
        {
            for (int index = 0; index < avatarDrivers.Length; index++)
            {
                LocalParticipantAvatarDriver driver = avatarDrivers[index];
                if (driver.Context.IsReserved
                    && avatarDirectory.TryGetAvatar(driver.Context.ParticipantId, out NetworkParticipantAvatar avatar))
                {
                    driver.TryBind(avatar);
                }
            }
        }

        private void HandleContextReserved(LocalParticipantContext context)
        {
            participantHuds[context.LocalSlot].Prepare(context.LocalSlot);
            UpdateAudioFocus();
        }

        private void Update()
        {
            if (_initialized)
            {
                UpdateAudioFocus();
            }
        }

        /// <summary>
        /// GDD 7: the single shared AudioListener follows the first living local participant's
        /// camera; with none alive it follows Player 1's camera, which shows the view Player 1 is
        /// spectating (or keeps its last view).
        /// </summary>
        private void UpdateAudioFocus()
        {
            bool anyReserved = false;
            for (int slot = 0; slot < participantContexts.Length; slot++)
            {
                LocalParticipantContext context = participantContexts[slot];
                _slotReserved[slot] = context.IsReserved;
                _slotEliminated[slot] = context.IsReserved
                    && context.ParticipantId.IsValid
                    && vitality.IsSpawned
                    && vitality.TryGetVitality(context.ParticipantId, out ParticipantVitality record)
                    && !record.IsInHouse;
                anyReserved |= context.IsReserved;
            }

            if (!anyReserved)
            {
                return;
            }

            int focus = LocalAudioFocusRule.SelectSlot(_slotReserved, _slotEliminated);
            Transform target = participantContexts[focus].ParticipantCamera.transform;
            if (sharedAudioListener.transform.parent != target)
            {
                sharedAudioListener.transform.SetParent(target, false);
                sharedAudioListener.transform.localPosition = Vector3.zero;
                sharedAudioListener.transform.localRotation = Quaternion.identity;
            }

            AudioFocusSlot = focus;
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
                generatorHuds[slot].BindParticipant(record.Id);
                vitalityHuds[slot].BindParticipant(record.Id);
                itemHuds[slot].BindParticipant(record.Id);
                revivalHuds[slot].BindParticipant(record.Id);
                flashlightHuds[slot].BindParticipant(record.Id);
                hidingHuds[slot].BindParticipant(record.Id);
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
            avatarDirectory.AvatarRegistered += HandleAvatarRegistered;
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
                || avatarSpawner == null
                || avatarDirectory == null
                || vitality == null
                || joiningCamera == null
                || joiningCanvas == null
                || sharedAudioListener == null)
            {
                throw new InvalidOperationException("Local participant composition requires explicit network, timeline, join and audio dependencies.");
            }

            if (participantContexts == null
                || participantHuds == null
                || avatarDrivers == null
                || generatorHuds == null
                || vitalityHuds == null
                || itemHuds == null
                || revivalHuds == null
                || flashlightHuds == null
                || hidingHuds == null
                || participantContexts.Length != ParticipantRegistry.MaximumParticipants
                || avatarDrivers.Length != ParticipantRegistry.MaximumParticipants
                || participantHuds.Length != ParticipantRegistry.MaximumParticipants
                || generatorHuds.Length != ParticipantRegistry.MaximumParticipants
                || vitalityHuds.Length != ParticipantRegistry.MaximumParticipants
                || itemHuds.Length != ParticipantRegistry.MaximumParticipants
                || revivalHuds.Length != ParticipantRegistry.MaximumParticipants
                || flashlightHuds.Length != ParticipantRegistry.MaximumParticipants
                || hidingHuds.Length != ParticipantRegistry.MaximumParticipants)
            {
                throw new InvalidOperationException("Local participant composition requires four explicit contexts and four explicit HUDs.");
            }


            for (int index = 0; index < participantContexts.Length; index++)
            {
                if (participantContexts[index] == null
                    || generatorHuds[index] == null
                    || vitalityHuds[index] == null
                    || itemHuds[index] == null
                    || revivalHuds[index] == null
                    || flashlightHuds[index] == null
                    || hidingHuds[index] == null
                    || avatarDrivers[index] == null
                    || avatarDrivers[index].Context != participantContexts[index])
                {
                    throw new InvalidOperationException("Every local participant context requires its matching explicit avatar driver.");
                }
            }
        }
    }
}
