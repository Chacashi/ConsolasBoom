using System;

namespace HouseEscape.Core.Participants
{
    public readonly struct ParticipantRecord : IEquatable<ParticipantRecord>
    {
        public ParticipantRecord(ParticipantId id, ulong ownerClientId, byte localSlot)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A participant record requires a valid ID.", nameof(id));
            }

            if (localSlot >= ParticipantRegistry.MaximumParticipants)
            {
                throw new ArgumentOutOfRangeException(nameof(localSlot));
            }

            Id = id;
            OwnerClientId = ownerClientId;
            LocalSlot = localSlot;
        }

        public ParticipantId Id { get; }

        public ulong OwnerClientId { get; }

        public byte LocalSlot { get; }

        public bool Equals(ParticipantRecord other)
        {
            return Id == other.Id
                && OwnerClientId == other.OwnerClientId
                && LocalSlot == other.LocalSlot;
        }

        public override bool Equals(object obj) => obj is ParticipantRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ OwnerClientId.GetHashCode();
                return (hashCode * 397) ^ LocalSlot.GetHashCode();
            }
        }
    }
}
