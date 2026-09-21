using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Infrastructure
{
    [DisallowMultipleComponent]
    public sealed class NetworkSessionBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private bool autoStartHost = true;

        public void Initialize(NetworkManager explicitNetworkManager)
        {
            networkManager = explicitNetworkManager;
        }

        private void Start()
        {
            if (autoStartHost)
            {
                StartHost();
            }
        }

        public bool StartHost()
        {
            return CanStart() && networkManager.StartHost();
        }

        public bool StartServer()
        {
            return CanStart() && networkManager.StartServer();
        }

        public bool StartClient()
        {
            return CanStart() && networkManager.StartClient();
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private bool CanStart()
        {
            if (networkManager == null)
            {
                Debug.LogError($"{nameof(NetworkSessionBootstrap)} requires an assigned {nameof(NetworkManager)}.", this);
                return false;
            }

            return !networkManager.IsListening;
        }
    }
}
