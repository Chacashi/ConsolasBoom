using System;

namespace HouseEscape.Gameplay.Flashlight
{
    /// <summary>Approved flashlight values (GDD 3.2 and 3.4).</summary>
    public static class FlashlightRules
    {
        public const float MaximumBattery = 100f;
        public const float StartingBattery = 100f;

        /// <summary>5% per second while on: 20 seconds of continuous use from full.</summary>
        public const double DrainPerSecond = 5d;

        /// <summary>A full recharge takes 45 seconds at a charging station (about 2.2% per second).</summary>
        public const double RechargePerSecond = MaximumBattery / 45d;

        /// <summary>A flashlight battery item adds 50%, capped at 100%.</summary>
        public const float BatteryItemGain = 50f;

        /// <summary>The beam illuminates a frontal cone of approximately three metres.</summary>
        public const float BeamRangeMetres = 3f;

        /// <summary>Placeholder tuning: aim volume radius around a charging station's aim point.</summary>
        public const float StationAimRadiusMetres = 0.6f;

        public const float StationServerAimRadiusMetres = StationAimRadiusMetres + 0.25f;

        public static float Clamp(float battery)
        {
            if (float.IsNaN(battery))
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(MaximumBattery, battery));
        }

        /// <summary>The whole percentage shown on the HUD and replicated to clients.</summary>
        public static int ToWholeBattery(float battery)
        {
            return (int)Math.Floor(Clamp(battery));
        }
    }
}
