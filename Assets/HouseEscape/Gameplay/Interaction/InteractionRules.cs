using System;
using System.Numerics;

namespace HouseEscape.Gameplay.Interaction
{
    /// <summary>
    /// Pure range and aim rules shared by everything the primary south face button acts on
    /// (generators, map items, unconscious teammates; GDD 3.4 and 3.5): the participant must be
    /// within range and its view ray must pass close to the target's aim point.
    /// </summary>
    public static class InteractionRules
    {
        /// <summary>GDD 3.4/3.5: interactions require being within two metres.</summary>
        public const float MaximumRangeMetres = 2f;

        private const float MinimumForwardLengthSquared = 1e-8f;

        /// <summary>Ground-position range check (participant root to target root).</summary>
        public static bool IsWithinRange(Vector3 participantPosition, Vector3 targetPosition, float range = MaximumRangeMetres)
        {
            if (!IsFinite(participantPosition) || !IsFinite(targetPosition) || float.IsNaN(range) || range < 0f)
            {
                return false;
            }

            return Vector3.DistanceSquared(participantPosition, targetPosition) <= range * range;
        }

        /// <summary>
        /// Distance between the aim point and the view ray, or zero when the viewer is inside the
        /// aim volume. Positive infinity when the aim point is behind the viewer or inputs are
        /// invalid. Smaller is better aimed.
        /// </summary>
        public static float GetAimError(Vector3 viewOrigin, Vector3 viewForward, Vector3 aimPoint, float aimRadius)
        {
            if (float.IsNaN(aimRadius)
                || aimRadius <= 0f
                || !IsFinite(viewOrigin)
                || !IsFinite(viewForward)
                || !IsFinite(aimPoint))
            {
                return float.PositiveInfinity;
            }

            float forwardLengthSquared = viewForward.LengthSquared();
            if (forwardLengthSquared < MinimumForwardLengthSquared)
            {
                return float.PositiveInfinity;
            }

            Vector3 forward = viewForward / (float)Math.Sqrt(forwardLengthSquared);
            Vector3 toTarget = aimPoint - viewOrigin;
            if (toTarget.LengthSquared() <= aimRadius * aimRadius)
            {
                return 0f;
            }

            float along = Vector3.Dot(toTarget, forward);
            if (along <= 0f)
            {
                return float.PositiveInfinity;
            }

            return (toTarget - forward * along).Length();
        }

        /// <summary>
        /// True when the view ray passes within <paramref name="aimRadius"/> of the aim point in
        /// front of the viewer, or the viewer is inside the aim volume.
        /// </summary>
        public static bool IsAimingAt(Vector3 viewOrigin, Vector3 viewForward, Vector3 aimPoint, float aimRadius)
        {
            return GetAimError(viewOrigin, viewForward, aimPoint, aimRadius) <= aimRadius;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
