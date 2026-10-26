using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>
    /// Chooses which living teammate an eliminated participant spectates. Candidates are visited
    /// in ascending participant ID order, wrapping around; the spectator itself is never chosen.
    /// </summary>
    public static class SpectatorTargetSelector
    {
        /// <summary>Keeps <paramref name="current"/> while it is a valid target, otherwise selects the next one.</summary>
        public static ParticipantId Resolve(
            IReadOnlyList<ParticipantVitality> participants,
            ParticipantId spectator,
            ParticipantId current)
        {
            return IsValidTarget(participants, spectator, current) ? current : Next(participants, spectator, current);
        }

        /// <summary>The next living teammate after <paramref name="current"/>, or None when there is none.</summary>
        public static ParticipantId Next(
            IReadOnlyList<ParticipantVitality> participants,
            ParticipantId spectator,
            ParticipantId current)
        {
            ParticipantId lowest = ParticipantId.None;
            ParticipantId nextAbove = ParticipantId.None;
            for (int index = 0; index < participants.Count; index++)
            {
                ParticipantVitality candidate = participants[index];
                if (!IsCandidate(candidate, spectator))
                {
                    continue;
                }

                ulong value = candidate.ParticipantId.Value;
                if (!lowest.IsValid || value < lowest.Value)
                {
                    lowest = candidate.ParticipantId;
                }

                if (value > current.Value && (!nextAbove.IsValid || value < nextAbove.Value))
                {
                    nextAbove = candidate.ParticipantId;
                }
            }

            return nextAbove.IsValid ? nextAbove : lowest;
        }

        public static bool IsValidTarget(
            IReadOnlyList<ParticipantVitality> participants,
            ParticipantId spectator,
            ParticipantId target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            for (int index = 0; index < participants.Count; index++)
            {
                if (participants[index].ParticipantId == target)
                {
                    return IsCandidate(participants[index], spectator);
                }
            }

            return false;
        }

        private static bool IsCandidate(ParticipantVitality candidate, ParticipantId spectator)
        {
            return candidate.ParticipantId.IsValid && candidate.ParticipantId != spectator && candidate.IsInHouse;
        }
    }
}
