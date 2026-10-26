namespace HouseEscape.Core.Timeline
{
    /// <summary>
    /// Evaluates a timeline snapshot at an authoritative server time. Comparing against the
    /// snapshot deadlines closes the window between a phase/match deadline passing and the
    /// timeline publishing its next snapshot.
    /// </summary>
    public static class MatchTimelineAvailability
    {
        public static bool IsMatchActive(MatchTimelineSnapshot snapshot, double serverTime)
        {
            return snapshot.IsStarted
                && !snapshot.IsComplete
                && serverTime < snapshot.MatchDeadline;
        }

        public static bool AreLightsOn(MatchTimelineSnapshot snapshot, double serverTime)
        {
            return snapshot.LightsAreOn && serverTime < snapshot.PhaseEndsAt;
        }

        /// <summary>True while the match is active and the lights are on.</summary>
        public static bool IsLitMatchActive(MatchTimelineSnapshot snapshot, double serverTime)
        {
            return IsMatchActive(snapshot, serverTime) && AreLightsOn(snapshot, serverTime);
        }
    }
}
