using System.Collections.Generic;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Items
{
    /// <summary>
    /// Server-side carried items for every participant in a match. Participants start with the
    /// supplies of their spawn slot. Whether a use is allowed by the participant's condition and
    /// the match state, and what it does, is decided by the caller between
    /// <see cref="CanUse"/> and <see cref="TryRemove"/>.
    /// </summary>
    public sealed class ItemInventoryRoster
    {
        private readonly List<ParticipantItems> _participants =
            new List<ParticipantItems>(ParticipantRegistry.MaximumParticipants);
        private readonly List<ItemType> _startingItems = new List<ItemType>(2);

        public IReadOnlyList<ParticipantItems> Participants => _participants;

        /// <summary>Adds a participant with its spawn slot's starting items. False for invalid or duplicate IDs.</summary>
        public bool Register(ParticipantId participantId, int spawnSlot)
        {
            if (!participantId.IsValid || IndexOf(participantId) >= 0)
            {
                return false;
            }

            var items = new ParticipantItems(participantId, 0, 0, 0, 0, 0);
            _startingItems.Clear();
            ItemRules.GetStartingItems(spawnSlot, _startingItems);
            for (int index = 0; index < _startingItems.Count; index++)
            {
                items = items.WithDelta(_startingItems[index], 1);
            }

            _participants.Add(items);
            return true;
        }

        public bool TryGet(ParticipantId participantId, out ParticipantItems items)
        {
            int index = IndexOf(participantId);
            items = index >= 0 ? _participants[index] : default;
            return index >= 0;
        }

        public int GetCount(ParticipantId participantId, ItemType type)
        {
            return TryGet(participantId, out ParticipantItems items) ? items.GetCount(type) : 0;
        }

        public bool Add(ParticipantId participantId, ItemType type)
        {
            int index = IndexOf(participantId);
            if (index < 0 || !ItemRules.IsValid(type))
            {
                return false;
            }

            _participants[index] = _participants[index].WithDelta(type, 1);
            return true;
        }

        /// <summary>Checks that one <paramref name="type"/> could be consumed, without removing it.</summary>
        public ItemUseResult CanUse(ParticipantId participantId, ItemType type)
        {
            int index = IndexOf(participantId);
            if (index < 0)
            {
                return ItemUseResult.UnknownParticipant;
            }

            if (!ItemRules.IsValid(type))
            {
                return ItemUseResult.InvalidItem;
            }

            if (_participants[index].GetCount(type) <= 0)
            {
                return ItemUseResult.NoneLeft;
            }

            return ItemRules.IsUsable(type) ? ItemUseResult.Used : ItemUseResult.NotUsable;
        }

        /// <summary>Removes one item of <paramref name="type"/>. False when none is carried.</summary>
        public bool TryRemove(ParticipantId participantId, ItemType type)
        {
            int index = IndexOf(participantId);
            if (index < 0 || !ItemRules.IsValid(type) || _participants[index].GetCount(type) <= 0)
            {
                return false;
            }

            _participants[index] = _participants[index].WithDelta(type, -1);
            return true;
        }

        public void Clear()
        {
            _participants.Clear();
        }

        private int IndexOf(ParticipantId participantId)
        {
            for (int index = 0; index < _participants.Count; index++)
            {
                if (_participants[index].ParticipantId == participantId)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
