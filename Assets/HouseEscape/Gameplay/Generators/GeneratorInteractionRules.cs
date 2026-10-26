using System;
using System.Numerics;
using HouseEscape.Gameplay.Interaction;

namespace HouseEscape.Gameplay.Generators
{
    /// <summary>
    /// Pure spatial and progress rules for starting and presenting a generator attempt.
    /// The server uses these to validate begin intentions; clients use the same rules only
    /// to choose which generator to request.
    /// </summary>
    public static class GeneratorInteractionRules
    {
        /// <summary>GDD 3.5 rule 4: the participant must be within two metres.</summary>
        public const float MaximumRangeMetres = InteractionRules.MaximumRangeMetres;

        /// <summary>
        /// Placeholder tuning: radius of the aim volume around a generator's aim point that
        /// the participant's view ray must pass through.
        /// </summary>
        public const float AimRadiusMetres = 0.6f;

        /// <summary>
        /// Technical server-side tolerance added to <see cref="AimRadiusMetres"/> to absorb
        /// yaw/pitch replication skew between a client's view and the server avatar. Range
        /// remains strict.
        /// </summary>
        public const float ServerAimToleranceMetres = 0.25f;

        public const float ServerAimRadiusMetres = AimRadiusMetres + ServerAimToleranceMetres;

        /// <summary>
        /// Range is measured between the participant's ground position (avatar root) and the
        /// generator's ground position (generator root).
        /// </summary>
        public static bool IsWithinRange(Vector3 participantPosition, Vector3 generatorPosition)
        {
            return InteractionRules.IsWithinRange(participantPosition, generatorPosition, MaximumRangeMetres);
        }

        /// <summary>
        /// Returns true when the view ray from <paramref name="viewOrigin"/> along
        /// <paramref name="viewForward"/> passes within <see cref="AimRadiusMetres"/> of the aim
        /// point in front of the viewer, or when the viewer is inside the aim volume.
        /// </summary>
        public static bool IsAimingAt(Vector3 viewOrigin, Vector3 viewForward, Vector3 aimPoint)
        {
            return IsAimingAt(viewOrigin, viewForward, aimPoint, AimRadiusMetres);
        }

        public static bool IsAimingAt(Vector3 viewOrigin, Vector3 viewForward, Vector3 aimPoint, float aimRadius)
        {
            return InteractionRules.IsAimingAt(viewOrigin, viewForward, aimPoint, aimRadius);
        }

        public static bool CanBeginAttempt(
            Vector3 participantPosition,
            Vector3 generatorPosition,
            Vector3 viewOrigin,
            Vector3 viewForward,
            Vector3 aimPoint)
        {
            return IsWithinRange(participantPosition, generatorPosition)
                && IsAimingAt(viewOrigin, viewForward, aimPoint);
        }

        /// <summary>Normalized hold progress in [0, 1] derived from synchronized server time.</summary>
        public static float GetHoldProgress(double attemptStartedAt, double synchronizedTime)
        {
            if (double.IsNaN(attemptStartedAt)
                || double.IsInfinity(attemptStartedAt)
                || double.IsNaN(synchronizedTime)
                || double.IsInfinity(synchronizedTime))
            {
                return 0f;
            }

            double progress = (synchronizedTime - attemptStartedAt) / GeneratorRace.RequiredHoldSeconds;
            return progress <= 0d ? 0f : progress >= 1d ? 1f : (float)progress;
        }
    }
}
