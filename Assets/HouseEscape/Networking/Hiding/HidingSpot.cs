using System;
using HouseEscape.Gameplay.Hiding;
using UnityEngine;

namespace HouseEscape.Networking.Hiding
{
    /// <summary>A static hiding place in the house (GDD 3.6). Occupancy is server state.</summary>
    [DisallowMultipleComponent]
    public sealed class HidingSpot : MonoBehaviour
    {
        [SerializeField] private HidingSpotType spotType;
        [SerializeField] private Transform aimPoint;

        public HidingSpotType SpotType => spotType;
        public Vector3 InteractionPosition => transform.position;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public void Initialize(HidingSpotType explicitSpotType, Transform explicitAimPoint)
        {
            spotType = explicitSpotType;
            aimPoint = explicitAimPoint != null ? explicitAimPoint : throw new ArgumentNullException(nameof(explicitAimPoint));
        }
    }
}
