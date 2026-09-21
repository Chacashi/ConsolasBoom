using System;
using UnityEngine;

namespace HouseEscape.LocalMultiplayer
{
    public static class LocalParticipantViewportLayout
    {
        public static Rect GetViewport(int participantCount, int localSlot)
        {
            if (participantCount < 1 || participantCount > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            }

            if (localSlot < 0 || localSlot >= participantCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localSlot));
            }

            if (participantCount == 1)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            if (participantCount == 2)
            {
                return new Rect(localSlot * 0.5f, 0f, 0.5f, 1f);
            }

            switch (localSlot)
            {
                case 0: return new Rect(0f, 0.5f, 0.5f, 0.5f);
                case 1: return new Rect(0.5f, 0.5f, 0.5f, 0.5f);
                case 2: return new Rect(0f, 0f, 0.5f, 0.5f);
                default: return new Rect(0.5f, 0f, 0.5f, 0.5f);
            }
        }
    }
}
