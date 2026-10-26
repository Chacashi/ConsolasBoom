using System;
using HouseEscape.Gameplay.Items;
using Unity.Netcode;

namespace HouseEscape.Networking.Items
{
    /// <summary>Replicated carried item counts of one participant.</summary>
    public struct NetworkItemInventoryRecord : INetworkSerializable, IEquatable<NetworkItemInventoryRecord>
    {
        public ulong ParticipantId;
        public ushort Apples;
        public ushort Sandwiches;
        public ushort EnergyDrinks;
        public ushort FirstAidKits;
        public ushort FlashlightBatteries;

        public NetworkItemInventoryRecord(ParticipantItems items)
        {
            ParticipantId = items.ParticipantId.Value;
            Apples = ToCount(items.Apples);
            Sandwiches = ToCount(items.Sandwiches);
            EnergyDrinks = ToCount(items.EnergyDrinks);
            FirstAidKits = ToCount(items.FirstAidKits);
            FlashlightBatteries = ToCount(items.FlashlightBatteries);
        }

        public ParticipantItems ToItems()
        {
            return new ParticipantItems(
                ParticipantId == 0
                    ? HouseEscape.Core.Participants.ParticipantId.None
                    : new HouseEscape.Core.Participants.ParticipantId(ParticipantId),
                Apples,
                Sandwiches,
                EnergyDrinks,
                FirstAidKits,
                FlashlightBatteries);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Apples);
            serializer.SerializeValue(ref Sandwiches);
            serializer.SerializeValue(ref EnergyDrinks);
            serializer.SerializeValue(ref FirstAidKits);
            serializer.SerializeValue(ref FlashlightBatteries);
        }

        public bool Equals(NetworkItemInventoryRecord other)
        {
            return ParticipantId == other.ParticipantId
                && Apples == other.Apples
                && Sandwiches == other.Sandwiches
                && EnergyDrinks == other.EnergyDrinks
                && FirstAidKits == other.FirstAidKits
                && FlashlightBatteries == other.FlashlightBatteries;
        }

        public override bool Equals(object obj) => obj is NetworkItemInventoryRecord other && Equals(other);

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

        private static ushort ToCount(int count)
        {
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue, count));
        }
    }
}
