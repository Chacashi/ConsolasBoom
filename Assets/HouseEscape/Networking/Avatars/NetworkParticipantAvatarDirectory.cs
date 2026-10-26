using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Avatars
{
    /// <summary>
    /// Per-scene lookup of spawned participant avatars by <see cref="ParticipantId"/>, filled on
    /// every peer. The server spawner attaches the directory to the avatars it creates; on other
    /// peers an NGO prefab instance handler attaches it to replicated instances. Avatars register
    /// themselves on network spawn and unregister on network despawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkParticipantAvatarDirectory : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkParticipantAvatar avatarPrefab;

        private readonly List<NetworkParticipantAvatar> _avatars = new List<NetworkParticipantAvatar>(ParticipantRegistry.MaximumParticipants);

        private InstanceHandler _instanceHandler;
        private NetworkManager _handlerOwner;

        public IReadOnlyList<NetworkParticipantAvatar> Avatars => _avatars;

        public event Action<NetworkParticipantAvatar> AvatarRegistered;
        public event Action<NetworkParticipantAvatar> AvatarUnregistered;

        public void Initialize(NetworkManager explicitNetworkManager, NetworkParticipantAvatar explicitAvatarPrefab)
        {
            networkManager = explicitNetworkManager != null
                ? explicitNetworkManager
                : throw new ArgumentNullException(nameof(explicitNetworkManager));
            avatarPrefab = explicitAvatarPrefab != null
                ? explicitAvatarPrefab
                : throw new ArgumentNullException(nameof(explicitAvatarPrefab));
        }

        public bool TryGetAvatar(ParticipantId participantId, out NetworkParticipantAvatar avatar)
        {
            if (participantId.IsValid)
            {
                for (int index = 0; index < _avatars.Count; index++)
                {
                    NetworkParticipantAvatar existing = _avatars[index];
                    if (existing != null && existing.IsSpawned && existing.ParticipantId == participantId)
                    {
                        avatar = existing;
                        return true;
                    }
                }
            }

            avatar = null;
            return false;
        }

        internal void Register(NetworkParticipantAvatar avatar)
        {
            if (avatar == null || _avatars.Contains(avatar))
            {
                return;
            }

            _avatars.Add(avatar);
            AvatarRegistered?.Invoke(avatar);
        }

        internal void Unregister(NetworkParticipantAvatar avatar)
        {
            if (avatar != null && _avatars.Remove(avatar))
            {
                AvatarUnregistered?.Invoke(avatar);
            }
        }

        private void Awake()
        {
            if (networkManager == null || avatarPrefab == null)
            {
                Debug.LogError($"{nameof(NetworkParticipantAvatarDirectory)} requires an explicit NetworkManager and avatar prefab.", this);
                return;
            }

            _instanceHandler = new InstanceHandler(this);
            _handlerOwner = networkManager;
            _handlerOwner.PrefabHandler.AddHandler(avatarPrefab.gameObject, _instanceHandler);
        }

        private void OnDestroy()
        {
            if (_handlerOwner != null && avatarPrefab != null)
            {
                _handlerOwner.PrefabHandler.RemoveHandler(avatarPrefab.gameObject);
            }

            _handlerOwner = null;
            _instanceHandler = null;
            _avatars.Clear();
        }

        private NetworkObject InstantiateReplica(Vector3 position, Quaternion rotation)
        {
            NetworkParticipantAvatar avatar = Instantiate(avatarPrefab, position, rotation);
            avatar.AttachDirectory(this);
            return avatar.NetworkObject;
        }

        /// <summary>Creates replicated avatar instances with the directory injected.</summary>
        private sealed class InstanceHandler : INetworkPrefabInstanceHandler
        {
            private readonly NetworkParticipantAvatarDirectory _directory;

            public InstanceHandler(NetworkParticipantAvatarDirectory directory)
            {
                _directory = directory;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                return _directory.InstantiateReplica(position, rotation);
            }

            public void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null)
                {
                    UnityEngine.Object.Destroy(networkObject.gameObject);
                }
            }
        }
    }
}
