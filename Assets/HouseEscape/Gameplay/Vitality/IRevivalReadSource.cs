using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Vitality
{
    /// <summary>Read-only replicated revival holds for presentation.</summary>
    public interface IRevivalReadSource
    {
        double SynchronizedTime { get; }

        bool TryGetRevivalByReviver(ParticipantId reviverId, out ParticipantId targetId, out double startedAt);

        bool TryGetRevivalOfTarget(ParticipantId targetId, out ParticipantId reviverId, out double startedAt);

        event Action Changed;
    }
}
