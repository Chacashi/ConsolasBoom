using System;
using HouseEscape.Gameplay.Generators;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Generators
{
    /// <summary>
    /// One in-scene generator. Its active state is written only by the server through
    /// <see cref="NetworkGeneratorRace"/> and replicated to every peer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGenerator : NetworkBehaviour
    {
        [SerializeField, Range(1, GeneratorRace.TotalGeneratorCount)] private int generatorNumber = 1;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private GameObject inactiveIndicator;
        [SerializeField] private GameObject activeIndicator;

        private readonly NetworkVariable<bool> _isActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int GeneratorNumber => generatorNumber;
        public bool IsActive => _isActive.Value;
        public Transform AimPoint => aimPoint;
        public Vector3 InteractionPosition => transform.position;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public event Action<NetworkGenerator> ActiveChanged;

        public void Initialize(
            Transform explicitAimPoint,
            GameObject explicitInactiveIndicator,
            GameObject explicitActiveIndicator)
        {
            aimPoint = explicitAimPoint != null
                ? explicitAimPoint
                : throw new ArgumentNullException(nameof(explicitAimPoint));
            inactiveIndicator = explicitInactiveIndicator;
            activeIndicator = explicitActiveIndicator;
            ApplyIndicator(false);
        }

        public void SetGeneratorNumber(int explicitGeneratorNumber)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("A generator number cannot change while spawned.");
            }

            if (explicitGeneratorNumber < 1 || explicitGeneratorNumber > GeneratorRace.TotalGeneratorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(explicitGeneratorNumber));
            }

            generatorNumber = explicitGeneratorNumber;
        }

        public override void OnNetworkSpawn()
        {
            _isActive.OnValueChanged += HandleActiveChanged;
            if (IsServer)
            {
                // A NetworkVariable keeps its local value across despawn; reset per session.
                _isActive.Value = false;
            }

            ApplyIndicator(_isActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isActive.OnValueChanged -= HandleActiveChanged;
        }

        internal void ActivateOnServer()
        {
            if (!IsSpawned || !IsServer)
            {
                throw new InvalidOperationException("Only the spawned server generator can be activated.");
            }

            _isActive.Value = true;
        }

        private void HandleActiveChanged(bool previous, bool current)
        {
            ApplyIndicator(current);
            ActiveChanged?.Invoke(this);
        }

        private void ApplyIndicator(bool active)
        {
            if (inactiveIndicator != null)
            {
                inactiveIndicator.SetActive(!active);
            }

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(active);
            }
        }
    }
}
