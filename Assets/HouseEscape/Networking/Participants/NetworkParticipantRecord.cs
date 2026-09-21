using System;
using HouseEscape.Core.Participants;
using Unity.Netcode;

namespace HouseEscape.Networking.Participants
{
    public struct NetworkParticipantRecord : INetworkSerializable, IEquatable<NetworkParticipantRecord>
    {
        public ulong ParticipantId;
        public ulong OwnerClientId;
        public byte LocalSlot;

        public NetworkParticipantRecord(ParticipantRecord record)
        {
            ParticipantId = record.Id.Value;
            OwnerClientId = record.OwnerClientId;
            LocalSlot = record.LocalSlot;
        }

        public ParticipantRecord ToCoreRecord()
        {
            return new ParticipantRecord(
                new ParticipantId(ParticipantId),
                OwnerClientId,
                LocalSlot);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref OwnerClientId);
            serializer.SerializeValue(ref LocalSlot);
        }

        public bool Equals(NetworkParticipantRecord other)
        {
            return ParticipantId == other.ParticipantId
                && OwnerClientId == other.OwnerClientId
                && LocalSlot == other.LocalSlot;
        }

        public override bool Equals(object obj) => obj is NetworkParticipantRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ OwnerClientId.GetHashCode();
                return (hashCode * 397) ^ LocalSlot.GetHashCode();
            }
        }
    }
}
