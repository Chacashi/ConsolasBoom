using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Hiding
{
    /// <summary>Read-only replicated hiding state for presentation and local input.</summary>
    public interface IHidingReadSource
    {
        double SynchronizedTime { get; }

        bool TryGetHiding(ParticipantId participantId, out HidingStatus status);

        event Action Changed;
    }
}
