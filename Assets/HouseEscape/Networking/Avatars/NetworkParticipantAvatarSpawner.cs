using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Networking.Participants;
using HouseEscape.Networking.Timeline;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Avatars
{
    [DisallowMultipleComponent]
    public sealed class NetworkParticipantAvatarSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkParticipantRegistry participantRegistry;
        [SerializeField] private NetworkMatchTimeline matchTimeline;
        [SerializeField] private NetworkParticipantAvatar avatarPrefab;
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private NetworkParticipantAvatarDirectory avatarDirectory;

        private readonly List<NetworkParticipantAvatar> _spawnedAvatars = new List<NetworkParticipantAvatar>(4);

        public IReadOnlyList<NetworkParticipantAvatar> SpawnedAvatars => _spawnedAvatars;
        public event Action<NetworkParticipantAvatar> AvatarSpawned;

        public void Initialize(
            NetworkManager explicitNetworkManager,
            NetworkParticipantRegistry explicitParticipantRegistry,
            NetworkMatchTimeline explicitMatchTimeline,
            NetworkParticipantAvatar explicitAvatarPrefab,
            Transform[] explicitSpawnPoints,
            NetworkParticipantAvatarDirectory explicitAvatarDirectory)
        {
            networkManager = explicitNetworkManager;
            participantRegistry = explicitParticipantRegistry;
            matchTimeline = explicitMatchTimeline;
            avatarPrefab = explicitAvatarPrefab;
            spawnPoints = explicitSpawnPoints;
            avatarDirectory = explicitAvatarDirectory;
            ValidateDependencies();
        }

        public void SpawnRegisteredParticipantsOnServer()
        {
            ValidateDependencies();
            if (!networkManager.IsServer || !participantRegistry.IsSpawned)
            {
                throw new InvalidOperationException("Only the active server can spawn participant avatars.");
            }

            IReadOnlyList<ParticipantRecord> participants = participantRegistry.GetSnapshot();
            for (int index = 0; index < participants.Count; index++)
            {
                ParticipantRecord participant = participants[index];
                if (TryGetAvatar(participant.Id, out _))
                {
                    continue;
                }

                Transform spawnPoint = spawnPoints[index];
                NetworkParticipantAvatar avatar = Instantiate(
                    avatarPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation);
                avatar.ConfigureServer(participant, participantRegistry, matchTimeline);
                avatar.AttachDirectory(avatarDirectory);
                avatar.Despawning += HandleAvatarDespawning;
                avatar.NetworkObject.SpawnWithOwnership(participant.OwnerClientId, destroyWithScene: true);
                _spawnedAvatars.Add(avatar);
                AvatarSpawned?.Invoke(avatar);
            }
        }

        /// <summary>
        /// The zero-based spawn slot (spawn point index, "Player N" is slot N-1) a participant
        /// is spawned at: its position in the registry snapshot. -1 when not registered.
        /// </summary>
        public int GetSpawnSlot(ParticipantId participantId)
        {
            IReadOnlyList<ParticipantRecord> participants = participantRegistry.GetSnapshot();
            for (int index = 0; index < participants.Count; index++)
            {
                if (participants[index].Id == participantId)
                {
                    return index;
                }
            }

            return -1;
        }

        public bool TryGetAvatar(ParticipantId participantId, out NetworkParticipantAvatar avatar)
        {
            for (int index = 0; index < _spawnedAvatars.Count; index++)
            {
                NetworkParticipantAvatar existing = _spawnedAvatars[index];
                if (existing != null && existing.ParticipantId == participantId)
                {
                    avatar = existing;
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        private void HandleAvatarDespawning(NetworkParticipantAvatar avatar)
        {
            avatar.Despawning -= HandleAvatarDespawning;
            _spawnedAvatars.Remove(avatar);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _spawnedAvatars.Count; index++)
            {
                if (_spawnedAvatars[index] != null)
                {
                    _spawnedAvatars[index].Despawning -= HandleAvatarDespawning;
                }
            }

            _spawnedAvatars.Clear();
        }

        private void ValidateDependencies()
        {
            if (networkManager == null
                || participantRegistry == null
                || matchTimeline == null
                || avatarPrefab == null
                || avatarDirectory == null
                || spawnPoints == null
                || spawnPoints.Length != ParticipantRegistry.MaximumParticipants)
            {
                throw new InvalidOperationException("Avatar spawning requires explicit network dependencies, a prefab, an avatar directory and four spawn points.");
            }

            for (int index = 0; index < spawnPoints.Length; index++)
            {
                if (spawnPoints[index] == null)
                {
                    throw new InvalidOperationException("Every participant spawn point must be assigned.");
                }
            }
        }
    }
}
