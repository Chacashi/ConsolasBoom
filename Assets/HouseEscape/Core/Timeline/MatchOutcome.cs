namespace HouseEscape.Core.Timeline
{
    /// <summary>
    /// Server-authoritative match result (GDD 2). <see cref="None"/> while the match is running.
    /// Defeat is recorded when every participant is eliminated or 6 AM is reached; Victory when
    /// every conscious participant escaped after all generators were activated.
    /// </summary>
    public enum MatchOutcome : byte
    {
        None = 0,
        Defeat = 1,
        Victory = 2
    }
}
