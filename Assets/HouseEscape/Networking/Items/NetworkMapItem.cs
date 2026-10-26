using System;
using HouseEscape.Gameplay.Items;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Items
{
    /// <summary>
    /// One pickable map item. The server sets its type before spawning; the type is replicated
    /// and drives a placeholder colour. Instances register with their
    /// <see cref="NetworkMapItemField"/> on every peer while spawned.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMapItem : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Transform aimPoint;
        [SerializeField] private Renderer visual;

        private readonly NetworkVariable<byte> _type = new NetworkVariable<byte>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkMapItemField _field;
        private ItemType _configuredType;
        private MaterialPropertyBlock _propertyBlock;

        public ItemType ItemType => (ItemType)_type.Value;
        public Vector3 InteractionPosition => transform.position;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public void Initialize(Transform explicitAimPoint, Renderer explicitVisual)
        {
            aimPoint = explicitAimPoint != null ? explicitAimPoint : throw new ArgumentNullException(nameof(explicitAimPoint));
            visual = explicitVisual != null ? explicitVisual : throw new ArgumentNullException(nameof(explicitVisual));
        }

        /// <summary>Server-only, before spawning: the item's type, written to the replicated value on spawn.</summary>
        internal void ConfigureServer(ItemType type)
        {
            if (IsSpawned)
            {
                throw new InvalidOperationException("A map item's type must be configured before it is spawned.");
            }

            _configuredType = type;
        }

        internal void AttachField(NetworkMapItemField field)
        {
            _field = field;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _type.Value = (byte)_configuredType;
            }

            _type.OnValueChanged += HandleTypeChanged;
            ApplyColour();
            if (_field != null)
            {
                _field.Register(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _type.OnValueChanged -= HandleTypeChanged;
            if (_field != null)
            {
                _field.Unregister(this);
            }
        }

        private void HandleTypeChanged(byte previous, byte current)
        {
            ApplyColour();
        }

        private void ApplyColour()
        {
            if (visual == null)
            {
                return;
            }

            Color colour = GetPlaceholderColour(ItemType);
            _propertyBlock ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, colour);
            _propertyBlock.SetColor(ColorId, colour);
            visual.SetPropertyBlock(_propertyBlock);
        }

        private static Color GetPlaceholderColour(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return new Color(0.85f, 0.1f, 0.1f);
                case ItemType.Sandwich:
                    return new Color(0.9f, 0.7f, 0.3f);
                case ItemType.EnergyDrink:
                    return new Color(0.2f, 0.5f, 1f);
                case ItemType.FirstAidKit:
                    return Color.white;
                case ItemType.FlashlightBattery:
                    return new Color(0.95f, 0.9f, 0.1f);
                default:
                    return Color.gray;
            }
        }
    }
}
