using UnityEngine;
using Unity.Netcode;
using System;

namespace HouseEscape.Network
{
    public class NetworkGameManager : NetworkBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private int port = 7777;

        private NetworkManager _networkManager;
        private int _lastConnectedCount;

        public static NetworkGameManager Instance { get; private set; }

        public event Action<int> OnPlayerJoined;
        public event Action<int> OnPlayerLeft;

        public int ConnectedPlayers => _networkManager != null ? _networkManager.ConnectedClients.Count : 0;
        public bool IsHost => _networkManager != null && _networkManager.IsServer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = gameObject.AddComponent<NetworkManager>();

            _lastConnectedCount = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_networkManager == null || !_networkManager.IsListening)
                return;

            int connected = _networkManager.ConnectedClients.Count;
            if (connected != _lastConnectedCount)
            {
                if (connected > _lastConnectedCount)
                {
                    Debug.Log("[NetworkGameManager] Player connected. Total: " + connected);
                    OnPlayerJoined?.Invoke(connected);
                }
                else
                {
                    Debug.Log("[NetworkGameManager] Player disconnected. Total: " + connected);
                    OnPlayerLeft?.Invoke(connected);
                }
                _lastConnectedCount = connected;
            }
        }

        public void StartHost()
        {
            if (_networkManager != null)
            {
                Debug.Log("[NetworkGameManager] Starting host");
                _networkManager.StartHost();
            }
        }

        public void StartServer()
        {
            if (_networkManager != null)
            {
                Debug.Log("[NetworkGameManager] Starting server");
                _networkManager.StartServer();
            }
        }

        public void ConnectClient()
        {
            if (_networkManager != null)
            {
                Debug.Log("[NetworkGameManager] Connecting client to localhost:" + port);
                _networkManager.StartClient();
            }
        }

        public void StopNetwork()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
                Debug.Log("[NetworkGameManager] Network stopped");
            }
        }
    }
}
