using HouseEscape.Gameplay.Generators;
using HouseEscape.Gameplay.Interaction;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace HouseEscape.Networking.Generators
{
    /// <summary>Unity-vector adapter over the pure <see cref="GeneratorInteractionRules"/>.</summary>
    public static class GeneratorInteractionQuery
    {
        public static bool CanBeginAttempt(
            NetworkGenerator generator,
            Vector3 participantPosition,
            Vector3 viewOrigin,
            Vector3 viewForward)
        {
            return IsWithinRange(generator, participantPosition)
                && IsAimingAt(generator, viewOrigin, viewForward);
        }

        public static bool IsWithinRange(NetworkGenerator generator, Vector3 participantPosition)
        {
            return generator != null
                && GeneratorInteractionRules.IsWithinRange(
                    ToNumerics(participantPosition),
                    ToNumerics(generator.InteractionPosition));
        }

        public static bool IsAimingAt(NetworkGenerator generator, Vector3 viewOrigin, Vector3 viewForward)
        {
            return IsAimingAt(generator, viewOrigin, viewForward, GeneratorInteractionRules.AimRadiusMetres);
        }

        /// <summary>Server validation uses the wider <see cref="GeneratorInteractionRules.ServerAimRadiusMetres"/>.</summary>
        public static bool IsAimingAtWithServerTolerance(NetworkGenerator generator, Vector3 viewOrigin, Vector3 viewForward)
        {
            return IsAimingAt(generator, viewOrigin, viewForward, GeneratorInteractionRules.ServerAimRadiusMetres);
        }

        /// <summary>Distance between the view ray and the generator's aim point; smaller is better aimed.</summary>
        public static float GetAimError(NetworkGenerator generator, Vector3 viewOrigin, Vector3 viewForward)
        {
            return generator != null
                ? InteractionRules.GetAimError(
                    ToNumerics(viewOrigin),
                    ToNumerics(viewForward),
                    ToNumerics(generator.AimPosition),
                    GeneratorInteractionRules.AimRadiusMetres)
                : float.PositiveInfinity;
        }

        private static bool IsAimingAt(NetworkGenerator generator, Vector3 viewOrigin, Vector3 viewForward, float aimRadius)
        {
            return generator != null
                && GeneratorInteractionRules.IsAimingAt(
                    ToNumerics(viewOrigin),
                    ToNumerics(viewForward),
                    ToNumerics(generator.AimPosition),
                    aimRadius);
        }

        private static NumericsVector3 ToNumerics(Vector3 value)
        {
            return new NumericsVector3(value.x, value.y, value.z);
        }
    }
}
