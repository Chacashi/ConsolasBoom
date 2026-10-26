namespace HouseEscape.Gameplay.Hiding
{
    /// <summary>Approved hiding values (GDD 3.6 and 6 "Score").</summary>
    public static class HidingRules
    {
        public const double ExitVulnerabilitySeconds = 1d;
        public const double CooldownSeconds = 10d;
        public const int SuccessScoreAward = 75;

        /// <summary>Placeholder tuning: aim volume radius around a hiding place's aim point.</summary>
        public const float AimRadiusMetres = 0.7f;

        public const float ServerAimRadiusMetres = AimRadiusMetres + 0.25f;

        public static double GetMaximumSeconds(HidingSpotType type)
        {
            switch (type)
            {
                case HidingSpotType.Wardrobe:
                    return 30d;
                case HidingSpotType.Bed:
                    return 20d;
                case HidingSpotType.Chest:
                    return 45d;
                case HidingSpotType.SmallCloset:
                    return 15d;
                default:
                    return 0d;
            }
        }

        /// <summary>Seconds the south face button must be held to get in (a chest takes two).</summary>
        public static double GetEnterHoldSeconds(HidingSpotType type)
        {
            return type == HidingSpotType.Chest ? 2d : 0d;
        }

        /// <summary>
        /// Whether the hiding place protects a player who enters while the monster is detecting
        /// them: every type except a bed ("works only if the monster did not see the player enter").
        /// </summary>
        public static bool Protects(HidingSpotType type, bool seenEntering)
        {
            return type != HidingSpotType.Bed || !seenEntering;
        }
    }
}
