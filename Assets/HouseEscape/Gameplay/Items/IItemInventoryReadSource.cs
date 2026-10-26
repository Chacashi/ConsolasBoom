using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Items
{
    /// <summary>Read-only replicated carried items for presentation.</summary>
    public interface IItemInventoryReadSource
    {
        bool TryGetItems(ParticipantId participantId, out ParticipantItems items);

        event Action Changed;
    }
}
