using UnityEngine;
using Unity.Netcode;
using HouseEscape.Player;
using System;

namespace HouseEscape.Network
{
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player Network")]
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Flashlight flashlight;
        [SerializeField] private PlayerEnergy energy;

        [Header("Network Variables")]
        [SerializeField] private NetworkVariable<float> _health = new NetworkVariable<float>(100f);
        [SerializeField] private NetworkVariable<float> _energy = new NetworkVariable<float>(100f);
        [SerializeField] private NetworkVariable<float> _battery = new NetworkVariable<float>(100f);
        [SerializeField] private NetworkVariable<bool> _isDead = new NetworkVariable<bool>(false);

        private Transform _playerTransform;

        public NetworkVariable<float> Health => _health;
        public NetworkVariable<float> Energy => _energy;
        public NetworkVariable<float> Battery => _battery;
        public NetworkVariable<bool> IsDead => _isDead;

        public event Action<float> OnRemoteHealthChanged;
        public event Action OnRemoteDeath;

        private void Awake()
        {
            _playerTransform = transform;

            if (health != null)
            {
                health.OnHealthChanged += HandleLocalHealthChanged;
                health.OnDied += HandleLocalDeath;
            }

            _health.OnValueChanged += (oldValue, newValue) =>
            {
                OnRemoteHealthChanged?.Invoke(newValue);
            };

            _isDead.OnValueChanged += (oldValue, newValue) =>
            {
                if (newValue && !IsOwner)
                    OnRemoteDeath?.Invoke();
            };
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleLocalHealthChanged;
                health.OnDied -= HandleLocalDeath;
            }
        }

        private void HandleLocalHealthChanged(float percentage)
        {
            if (IsOwner)
            {
                _health.Value = percentage * 100f;
            }
        }

        private void HandleLocalDeath()
        {
            if (IsOwner)
            {
                _isDead.Value = true;
            }
        }

        public void ServerRpcSetHealth(float healthValue)
        {
            if (!IsServer) return;

            _health.Value = healthValue;
            if (health != null)
            {
                float maxHealth = 100f;
                health.TakeDamage(maxHealth - healthValue);
            }
        }

        public void ClientRpcUpdatePosition(Vector3 position, Quaternion rotation)
        {
            _playerTransform.position = position;
            _playerTransform.rotation = rotation;
        }

        public void ServerRpcRespawn()
        {
            if (!IsServer) return;

            _isDead.Value = false;
            _health.Value = 100f;

            if (health != null)
            {
                var spawnManager = FindAnyObjectByType<HouseEscape.Systems.GameManager>();
                if (spawnManager != null)
                {
                    var spawnPoint = spawnManager.GetPlayerSpawnPoint();
                    if (spawnPoint != null)
                        _playerTransform.position = spawnPoint.position;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                Debug.Log("[NetworkPlayer] Local player spawned: " + NetworkObject.NetworkObjectId);
            }
            else
            {
                Debug.Log("[NetworkPlayer] Remote player spawned: " + NetworkObject.NetworkObjectId);
            }
        }

        private void Update()
        {
            if (IsOwner && IsServer)
            {
                if (health != null)
                    _health.Value = health.CurrentHealth;
                if (energy != null)
                    _energy.Value = energy.CurrentEnergy;
                if (flashlight != null)
                    _battery.Value = flashlight.CurrentBattery;
            }
        }
    }
}
