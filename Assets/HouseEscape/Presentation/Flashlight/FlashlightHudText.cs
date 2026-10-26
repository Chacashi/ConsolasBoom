using HouseEscape.Gameplay.Flashlight;

namespace HouseEscape.Presentation.Flashlight
{
    public static class FlashlightHudText
    {
        public static string Format(int wholeBattery, bool isOn, bool isCharging)
        {
            int maximum = (int)FlashlightRules.MaximumBattery;
            int clamped = wholeBattery < 0 ? 0 : wholeBattery > maximum ? maximum : wholeBattery;
            string state = isCharging ? " — charging" : isOn ? " (on)" : string.Empty;
            return $"Flashlight: {clamped}%{state}";
        }
    }
}
