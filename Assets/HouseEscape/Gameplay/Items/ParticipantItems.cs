using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Items
{
    /// <summary>One participant's carried item counts (unlimited per type, GDD 3.4).</summary>
    public readonly struct ParticipantItems : IEquatable<ParticipantItems>
    {
        public ParticipantItems(
            ParticipantId participantId,
            int apples,
            int sandwiches,
            int energyDrinks,
            int firstAidKits,
            int flashlightBatteries)
        {
            ParticipantId = participantId;
            Apples = apples;
            Sandwiches = sandwiches;
            EnergyDrinks = energyDrinks;
            FirstAidKits = firstAidKits;
            FlashlightBatteries = flashlightBatteries;
        }

        public ParticipantId ParticipantId { get; }
        public int Apples { get; }
        public int Sandwiches { get; }
        public int EnergyDrinks { get; }
        public int FirstAidKits { get; }
        public int FlashlightBatteries { get; }

        public int GetCount(ItemType type)
        {
            switch (type)
            {
                case ItemType.Apple:
                    return Apples;
                case ItemType.Sandwich:
                    return Sandwiches;
                case ItemType.EnergyDrink:
                    return EnergyDrinks;
                case ItemType.FirstAidKit:
                    return FirstAidKits;
                case ItemType.FlashlightBattery:
                    return FlashlightBatteries;
                default:
                    return 0;
            }
        }

        /// <summary>Returns a copy with <paramref name="delta"/> added to one type, never below zero.</summary>
        public ParticipantItems WithDelta(ItemType type, int delta)
        {
            return new ParticipantItems(
                ParticipantId,
                Adjust(type, ItemType.Apple, Apples, delta),
                Adjust(type, ItemType.Sandwich, Sandwiches, delta),
                Adjust(type, ItemType.EnergyDrink, EnergyDrinks, delta),
                Adjust(type, ItemType.FirstAidKit, FirstAidKits, delta),
                Adjust(type, ItemType.FlashlightBattery, FlashlightBatteries, delta));
        }

        public bool Equals(ParticipantItems other)
        {
            return ParticipantId == other.ParticipantId
                && Apples == other.Apples
                && Sandwiches == other.Sandwiches
                && EnergyDrinks == other.EnergyDrinks
                && FirstAidKits == other.FirstAidKits
                && FlashlightBatteries == other.FlashlightBatteries;
        }

        public override bool Equals(object obj) => obj is ParticipantItems other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Apples;
                hashCode = (hashCode * 397) ^ Sandwiches;
                hashCode = (hashCode * 397) ^ EnergyDrinks;
                hashCode = (hashCode * 397) ^ FirstAidKits;
                return (hashCode * 397) ^ FlashlightBatteries;
            }
        }

        private static int Adjust(ItemType changed, ItemType candidate, int count, int delta)
        {
            return candidate == changed ? Math.Max(0, count + delta) : count;
        }
    }
}
