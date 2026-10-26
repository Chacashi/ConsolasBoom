using System;
using HouseEscape.Gameplay.Vitality;
using UnityEngine;

namespace HouseEscape.Presentation.Vitality
{
    /// <summary>
    /// Local-only camera feedback for low energy (GDD 3.3): the field of view drops from 70 to
    /// 60 degrees at 10-29% energy, and below 10% the camera sways. Effects apply only while the
    /// participant is conscious; nothing here is replicated.
    /// </summary>
    public static class EnergyCameraEffects
    {
        public const float NormalFieldOfView = 70f;
        public const float LowEnergyFieldOfView = 60f;

        public const int LowEnergyMinimum = 10;
        public const int LowEnergyMaximum = 29;

        // Sway tuning (presentation only, not specified by the GDD).
        public const float SwayRollDegrees = 2.5f;
        public const float SwayPitchDegrees = 1.2f;
        public const float SwayRollFrequency = 0.6f;
        public const float SwayPitchFrequency = 0.9f;

        public static float GetFieldOfView(VitalityState state, int wholeEnergy)
        {
            return state == VitalityState.Conscious
                && wholeEnergy >= LowEnergyMinimum
                && wholeEnergy <= LowEnergyMaximum
                ? LowEnergyFieldOfView
                : NormalFieldOfView;
        }

        public static bool IsSwaying(VitalityState state, int wholeEnergy)
        {
            return state == VitalityState.Conscious && wholeEnergy < LowEnergyMinimum;
        }

        /// <summary>Local camera rotation offset; identity unless swaying.</summary>
        public static Quaternion GetSway(VitalityState state, int wholeEnergy, double time)
        {
            if (!IsSwaying(state, wholeEnergy))
            {
                return Quaternion.identity;
            }

            float roll = SwayRollDegrees * (float)Math.Sin(2d * Math.PI * SwayRollFrequency * time);
            float pitch = SwayPitchDegrees * (float)Math.Sin(2d * Math.PI * SwayPitchFrequency * time);
            return Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
