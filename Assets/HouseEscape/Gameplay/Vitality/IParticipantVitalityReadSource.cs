using System;
using System.Collections.Generic;
using HouseEscape.Core.Participants;
using HouseEscape.Core.Timeline;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>Read-only replicated participant vitality and match outcome for presentation.</summary>
    public interface IParticipantVitalityReadSource
    {
        double SynchronizedTime { get; }

        MatchOutcome Outcome { get; }

        bool TryGetVitality(ParticipantId participantId, out ParticipantVitality vitality);

        /// <summary>Replaces the contents of <paramref name="results"/> with every known participant.</summary>
        void GetVitalities(List<ParticipantVitality> results);

        event Action Changed;
    }
}
