namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>Approved health and incapacitation values (GDD 3.3 and 7).</summary>
    public static class VitalityRules
    {
        public const int MaximumHealth = 100;

        /// <summary>Seconds an unconscious participant may be revived before permanent elimination.</summary>
        public const double RecoveryWindowSeconds = 30d;

        /// <summary>Seconds a reviver must hold the south face button to revive a teammate.</summary>
        public const double RevivalHoldSeconds = 10d;

        /// <summary>Health a revived participant returns to consciousness with.</summary>
        public const int RevivedHealth = 40;

        /// <summary>Points for the reviver when a revival completes.</summary>
        public const int RevivalScoreAward = 150;
    }
}
