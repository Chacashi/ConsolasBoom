namespace HouseEscape.Gameplay.Vitality
{
    public enum VitalityDamageResult
    {
        /// <summary>Unknown participant or non-positive damage.</summary>
        Ignored,

        /// <summary>Unconscious and eliminated participants take no damage.</summary>
        NotConscious,

        Damaged,
        KnockedUnconscious
    }
}
