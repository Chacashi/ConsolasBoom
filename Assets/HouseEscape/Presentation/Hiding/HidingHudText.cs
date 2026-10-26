using System;
using HouseEscape.Gameplay.Hiding;

namespace HouseEscape.Presentation.Hiding
{
    public static class HidingHudText
    {
        public static string GetName(HidingSpotType type)
        {
            switch (type)
            {
                case HidingSpotType.Wardrobe:
                    return "wardrobe";
                case HidingSpotType.Bed:
                    return "under the bed";
                case HidingSpotType.Chest:
                    return "chest";
                case HidingSpotType.SmallCloset:
                    return "small closet";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The hiding line for the participant's own HUD at <paramref name="now"/>.</summary>
        public static string Format(HidingStatus status, double now)
        {
            switch (status.Phase)
            {
                case HidingPhase.Entering:
                    return $"Getting into the {GetName(status.SpotType)} — {Percent(status, now)}%";
                case HidingPhase.Hidden:
                    return $"Hidden ({GetName(status.SpotType)}) — {SecondsLeft(status.PhaseEndsAt, now)}s — A / CROSS to leave";
                case HidingPhase.Exiting:
                    return "Leaving the hiding place…";
                default:
                    return now < status.CooldownEndsAt ? $"Hide again in {SecondsLeft(status.CooldownEndsAt, now)}s" : string.Empty;
            }
        }

        private static int SecondsLeft(double endsAt, double now)
        {
            double remaining = endsAt - now;
            return remaining <= 0d ? 0 : (int)Math.Ceiling(remaining);
        }

        private static int Percent(HidingStatus status, double now)
        {
            double duration = status.PhaseEndsAt - status.PhaseStartedAt;
            if (duration <= 0d)
            {
                return 100;
            }

            double progress = (now - status.PhaseStartedAt) / duration;
            return progress <= 0d ? 0 : progress >= 1d ? 100 : (int)Math.Floor(progress * 100d);
        }
    }
}
