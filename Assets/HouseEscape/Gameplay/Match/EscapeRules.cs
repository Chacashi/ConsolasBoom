namespace HouseEscape.Gameplay.Match
{
    /// <summary>Approved escape values (GDD 2 and 6 "Score").</summary>
    public static class EscapeRules
    {
        /// <summary>Points for each participant who escapes through the front door.</summary>
        public const int EscapeScoreAward = 500;

        /// <summary>Placeholder tuning: a conscious participant this close to the open front door escapes.</summary>
        public const float ExitRadiusMetres = 1.5f;

        /// <summary>The front door opens once every generator is active.</summary>
        public static bool IsDoorOpen(int activeGenerators, int totalGenerators)
        {
            return totalGenerators > 0 && activeGenerators >= totalGenerators;
        }
    }
}
