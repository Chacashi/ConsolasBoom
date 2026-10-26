using System;
using UnityEngine;

namespace HouseEscape.Networking.Flashlight
{
    /// <summary>
    /// A static flashlight charging station placed in the house (GDD 3.2). It has no replicated
    /// state: several players may charge at once and progress belongs to each player's battery.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChargingStation : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;

        public Vector3 InteractionPosition => transform.position;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        public void Initialize(Transform explicitAimPoint)
        {
            aimPoint = explicitAimPoint != null ? explicitAimPoint : throw new ArgumentNullException(nameof(explicitAimPoint));
        }
    }
}
