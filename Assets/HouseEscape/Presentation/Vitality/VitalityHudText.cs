using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;
using HouseEscape.Gameplay.Vitality;

namespace HouseEscape.Presentation.Vitality
{
    public static class VitalityHudText
    {
        public static string FormatHealth(int health)
        {
            int clamped = health < 0 ? 0 : health > VitalityRules.MaximumHealth ? VitalityRules.MaximumHealth : health;
            return $"Health: {clamped}";
        }

        public static string FormatEnergy(int wholeEnergy)
        {
            int maximum = (int)EnergyRules.MaximumEnergy;
            int clamped = wholeEnergy < 0 ? 0 : wholeEnergy > maximum ? maximum : wholeEnergy;
            return $"Energy: {clamped}";
        }

        /// <summary>
        /// Condition line: empty while conscious, a countdown while unconscious, and the spectated
        /// teammate (labelled by participant number) once eliminated.
        /// </summary>
        public static string FormatStatus(ParticipantVitality vitality, double now, ParticipantId spectated)
        {
            switch (vitality.State)
            {
                case VitalityState.Unconscious:
                    return $"Unconscious — {vitality.GetRecoverySecondsRemaining(now)}s";
                case VitalityState.Eliminated:
                    return spectated.IsValid ? $"Eliminated — spectating P{spectated.Value}" : "Eliminated";
                case VitalityState.Escaped:
                    return spectated.IsValid ? $"Escaped — spectating P{spectated.Value}" : "Escaped";
                default:
                    return string.Empty;
            }
        }

        public static string FormatOutcome(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.Defeat:
                    return "DEFEAT";
                case MatchOutcome.Victory:
                    return "VICTORY — you escaped the house";
                default:
                    return string.Empty;
            }
        }
    }
}
