using System;
using HouseEscape.Gameplay.Movement;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// Approved energy values (GDD 3.3 "Health And Energy" and "Energy rules"). Energy is a
    /// continuous 0-100 value on the server; brackets use whole percentages, so a value is in a
    /// bracket when its floor is (for example 59.9 counts as 30-59).
    /// </summary>
    public static class EnergyRules
    {
        public const float MaximumEnergy = 100f;
        public const float StartingEnergy = 100f;

        public const double RunningDrainPerSecond = 2d;
        public const double InjuredDrainPerSecond = 1d;
        public const double PassiveDrainPerSecond = 0.5d / 60d;

        /// <summary>A participant is injured while health is below this value.</summary>
        public const int InjuredHealthThreshold = 50;

        public const float RunSpeedMultiplier = 1.6f;

        /// <summary>Below this energy the participant cannot run.</summary>
        public const float MinimumRunEnergy = 10f;

        public static bool IsInjured(int health) => health < InjuredHealthThreshold;

        public static bool CanRun(float energy) => energy >= MinimumRunEnergy;

        /// <summary>Additive drain in energy per second; zero unless conscious and the match is active.</summary>
        public static double GetDrainPerSecond(VitalityState state, int health, bool isRunning, bool isMatchActive)
        {
            if (state != VitalityState.Conscious || !isMatchActive)
            {
                return 0d;
            }

            double drain = PassiveDrainPerSecond;
            if (isRunning)
            {
                drain += RunningDrainPerSecond;
            }

            if (IsInjured(health))
            {
                drain += InjuredDrainPerSecond;
            }

            return drain;
        }

        /// <summary>Energy speed penalty: 60-100 normal, 30-59 -20%, 10-29 -40%, 0-9 -60%.</summary>
        public static float GetSpeedMultiplier(float energy)
        {
            if (energy >= 60f)
            {
                return 1f;
            }

            if (energy >= 30f)
            {
                return 0.8f;
            }

            return energy >= 10f ? 0.6f : 0.4f;
        }

        /// <summary>
        /// Server movement speed in metres per second: walk speed, times the run multiplier when a
        /// run is requested and allowed, times the energy penalty.
        /// </summary>
        public static float GetMovementSpeed(float energy, bool runRequested)
        {
            float run = runRequested && CanRun(energy) ? RunSpeedMultiplier : 1f;
            return MovementMotor.WalkSpeed * run * GetSpeedMultiplier(energy);
        }

        public static float Clamp(float energy)
        {
            if (float.IsNaN(energy))
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(MaximumEnergy, energy));
        }

        /// <summary>The whole percentage shown on the HUD and replicated to clients.</summary>
        public static int ToWholeEnergy(float energy)
        {
            return (int)Math.Floor(Clamp(energy));
        }
    }
}
