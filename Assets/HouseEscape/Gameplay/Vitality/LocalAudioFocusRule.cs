using System;
using System.Collections.Generic;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// GDD 7: a console hears its audio from the first living local participant (lowest local
    /// slot that is reserved and not permanently eliminated). If no local participant is alive,
    /// audio follows the view Player 1 (slot 0) is spectating.
    /// </summary>
    public static class LocalAudioFocusRule
    {
        /// <param name="isReserved">Per local slot: a participant occupies the slot.</param>
        /// <param name="isEliminated">Per local slot: that participant is permanently eliminated.</param>
        /// <returns>The local slot whose view carries the console's audio.</returns>
        public static int SelectSlot(IReadOnlyList<bool> isReserved, IReadOnlyList<bool> isEliminated)
        {
            if (isReserved == null)
            {
                throw new ArgumentNullException(nameof(isReserved));
            }

            if (isEliminated == null || isEliminated.Count != isReserved.Count)
            {
                throw new ArgumentException("Both lists must describe the same local slots.", nameof(isEliminated));
            }

            for (int slot = 0; slot < isReserved.Count; slot++)
            {
                if (isReserved[slot] && !isEliminated[slot])
                {
                    return slot;
                }
            }

            return 0;
        }
    }
}
