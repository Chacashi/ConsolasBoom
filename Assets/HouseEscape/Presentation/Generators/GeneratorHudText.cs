using HouseEscape.Gameplay.Generators;

namespace HouseEscape.Presentation.Generators
{
    public static class GeneratorHudText
    {
        public static string FormatProgress(int activeGeneratorCount)
        {
            int clamped = activeGeneratorCount < 0
                ? 0
                : activeGeneratorCount > GeneratorRace.TotalGeneratorCount
                    ? GeneratorRace.TotalGeneratorCount
                    : activeGeneratorCount;
            string progress = $"Generators: {clamped}/{GeneratorRace.TotalGeneratorCount}";
            return clamped == GeneratorRace.TotalGeneratorCount ? progress + " — front door open" : progress;
        }

        public static string FormatAttempt(int generatorNumber, float holdProgress)
        {
            float clamped = holdProgress < 0f ? 0f : holdProgress > 1f ? 1f : holdProgress;
            int percent = (int)(clamped * 100f);
            return $"Generator {generatorNumber}: HOLD A / CROSS {percent}%";
        }
    }
}
