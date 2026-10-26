using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Generators
{
    /// <summary>Read-only replicated generator race state for presentation.</summary>
    public interface IGeneratorRaceReadSource
    {
        int ActiveGeneratorCount { get; }

        double SynchronizedTime { get; }

        bool TryGetAttempt(ParticipantId participantId, out int generatorNumber, out double startedAt);

        int GetScore(ParticipantId participantId);

        event Action Changed;
    }
}
