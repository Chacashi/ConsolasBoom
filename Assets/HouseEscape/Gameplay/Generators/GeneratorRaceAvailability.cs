using HouseEscape.Core.Timeline;

namespace HouseEscape.Gameplay.Generators
{
    /// <summary>
    /// Evaluates whether generator attempts may run at an authoritative server time, so a hold
    /// can never complete after lights-off. Time checks delegate to
    /// <see cref="MatchTimelineAvailability"/>, which the monster shares.
    /// </summary>
    public static class GeneratorRaceAvailability
    {
        public static bool IsMatchActive(MatchTimelineSnapshot snapshot, double serverTime)
        {
            return MatchTimelineAvailability.IsMatchActive(snapshot, serverTime);
        }

        public static bool AreLightsOn(MatchTimelineSnapshot snapshot, double serverTime)
        {
            return MatchTimelineAvailability.AreLightsOn(snapshot, serverTime);
        }

        /// <summary>
        /// End of the window in which attempts may run for this snapshot: the earlier of the
        /// lights-on phase end and the match deadline, or negative infinity when unavailable.
        /// </summary>
        public static double GetAvailableUntil(MatchTimelineSnapshot snapshot)
        {
            if (!snapshot.IsStarted || snapshot.IsComplete || !snapshot.LightsAreOn)
            {
                return double.NegativeInfinity;
            }

            return snapshot.PhaseEndsAt < snapshot.MatchDeadline ? snapshot.PhaseEndsAt : snapshot.MatchDeadline;
        }
    }
}
