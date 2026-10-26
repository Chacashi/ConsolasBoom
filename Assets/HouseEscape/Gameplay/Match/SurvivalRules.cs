using HouseEscape.Core.Timeline;

namespace HouseEscape.Gameplay.Match
{
    /// <summary>"Survive one lights-on phase" scoring (GDD 6 "Score").</summary>
    public static class SurvivalRules
    {
        public const int LightsOnPhaseSurvivedAward = 50;

        /// <summary>
        /// A lights-on phase was survived when the running match moves from a lights-on snapshot
        /// to a lights-off one; the match ending during lights-on does not count.
        /// </summary>
        public static bool IsLightsOnPhaseSurvived(MatchTimelineSnapshot previous, MatchTimelineSnapshot current)
        {
            return previous.IsStarted
                && previous.LightPhase == LightPhase.On
                && current.IsStarted
                && !current.IsComplete
                && current.LightPhase == LightPhase.Off
                && current.Revision > previous.Revision;
        }
    }
}
