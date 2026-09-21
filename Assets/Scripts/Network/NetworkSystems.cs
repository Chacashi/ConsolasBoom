using UnityEngine;
using Unity.Netcode;
using HouseEscape.Systems;
using System;

namespace HouseEscape.Network
{
    public class NetworkLightCycle : NetworkBehaviour
    {
        [Header("Light Cycle Network")]
        [SerializeField] private LightCycleManager lightCycleManager;

        [Header("Network Variables")]
        [SerializeField] private NetworkVariable<bool> _lightsAreOn = new NetworkVariable<bool>(false);
        [SerializeField] private NetworkVariable<float> _timerRemaining = new NetworkVariable<float>(0f);

        public NetworkVariable<bool> LightsAreOn => _lightsAreOn;
        public NetworkVariable<float> TimerRemaining => _timerRemaining;

        public event Action<bool> OnRemoteLightsChanged;

        private void Awake()
        {
            if (lightCycleManager == null)
                lightCycleManager = GetComponent<LightCycleManager>();

            if (lightCycleManager != null)
            {
                lightCycleManager.OnLightsChanged += HandleLightsChanged;
                lightCycleManager.OnTimerUpdated += HandleTimerUpdated;
            }

            _lightsAreOn.OnValueChanged += (oldValue, newValue) =>
            {
                OnRemoteLightsChanged?.Invoke(newValue);
            };
        }

        private void OnDestroy()
        {
            if (lightCycleManager != null)
            {
                lightCycleManager.OnLightsChanged -= HandleLightsChanged;
                lightCycleManager.OnTimerUpdated -= HandleTimerUpdated;
            }
        }

        private void HandleLightsChanged(bool isOn)
        {
            if (IsServer)
            {
                _lightsAreOn.Value = isOn;
            }
        }

        private void HandleTimerUpdated(float timer)
        {
            if (IsServer)
            {
                _timerRemaining.Value = timer;
            }
        }

        public void ServerRpcSetLights(bool isOn)
        {
            if (!IsServer) return;

            _lightsAreOn.Value = isOn;
            if (lightCycleManager != null)
                lightCycleManager.SetLightsOn(isOn);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log("[NetworkLightCycle] Light cycle synced on network");
        }

        private void Update()
        {
            if (IsServer && lightCycleManager != null)
            {
                _lightsAreOn.Value = lightCycleManager.LightsAreOn;
                _timerRemaining.Value = lightCycleManager.TimerRemaining;
            }
        }
    }

    public class NetworkGenerator : NetworkBehaviour
    {
        [Header("Generator Network")]
        [SerializeField] private Generator generator;

        [Header("Network Variables")]
        [SerializeField] private NetworkVariable<bool> _isActivated = new NetworkVariable<bool>(false);
        [SerializeField] private NetworkVariable<float> _progress = new NetworkVariable<float>(0f);

        public NetworkVariable<bool> IsActivated => _isActivated;
        public NetworkVariable<float> Progress => _progress;

        public event Action<bool> OnRemoteActivationChanged;

        private void Awake()
        {
            if (generator == null)
                generator = GetComponent<Generator>();

            if (generator != null)
            {
                generator.OnActivated += HandleGeneratorActivated;
                generator.OnProgress += HandleGeneratorProgress;
            }

            _isActivated.OnValueChanged += (oldValue, newValue) =>
            {
                OnRemoteActivationChanged?.Invoke(newValue);
            };
        }

        private void OnDestroy()
        {
            if (generator != null)
            {
                generator.OnActivated -= HandleGeneratorActivated;
                generator.OnProgress -= HandleGeneratorProgress;
            }
        }

        private void HandleGeneratorActivated(Generator gen)
        {
            if (IsServer)
            {
                _isActivated.Value = true;
            }
        }

        private void HandleGeneratorProgress(Generator gen, float progress)
        {
            if (IsServer)
            {
                _progress.Value = progress;
            }
        }

        public void ServerRpcActivate()
        {
            if (!IsServer) return;

            if (generator != null)
                generator.StartActivation();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log("[NetworkGenerator] Generator synced on network: " + (generator != null ? generator.Name : "Unknown"));
        }

        private void Update()
        {
            if (IsServer && generator != null)
            {
                _isActivated.Value = generator.IsActivated;
                _progress.Value = generator.Progress;
            }
        }
    }
}
