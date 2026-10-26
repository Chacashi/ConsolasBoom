using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Flashlight
{
    /// <summary>Read-only replicated flashlight state for presentation.</summary>
    public interface IFlashlightReadSource
    {
        bool TryGetFlashlight(ParticipantId participantId, out ParticipantFlashlight flashlight);

        /// <summary>Whether the participant is holding a charging station.</summary>
        bool IsCharging(ParticipantId participantId);

        event Action Changed;
    }
}
