using System;
using HouseEscape.Core.Participants;
using HouseEscape.Gameplay.Vitality;

namespace HouseEscape.Presentation.Vitality
{
    public static class RevivalHudText
    {
        /// <summary>Whole hold percentage derived from the server start time and synchronized time.</summary>
        public static int GetPercent(double startedAt, double now)
        {
            if (double.IsNaN(startedAt) || double.IsNaN(now) || double.IsInfinity(startedAt) || double.IsInfinity(now))
            {
                return 0;
            }

            double progress = (now - startedAt) / VitalityRules.RevivalHoldSeconds;
            return progress <= 0d ? 0 : progress >= 1d ? 100 : (int)Math.Floor(progress * 100d);
        }

        public static string FormatReviving(ParticipantId target, int percent)
        {
            return $"Reviving P{target.Value} — {percent}%";
        }

        public static string FormatBeingRevived(ParticipantId reviver, int percent)
        {
            return $"Being revived by P{reviver.Value} — {percent}%";
        }
    }
}
