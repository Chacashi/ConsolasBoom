namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// GDD 7 "Incapacitation And Recovery" participant condition, plus <see cref="Escaped"/> for a
    /// participant who left the house through the front door (GDD 2) and now spectates.
    /// </summary>
    public enum VitalityState : byte
    {
        Conscious = 0,
        Unconscious = 1,
        Eliminated = 2,
        Escaped = 3
    }
}
