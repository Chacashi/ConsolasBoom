using System.Collections.Generic;

namespace HouseEscape.Core.Participants
{
    public sealed class ParticipantRegistry
    {
        public const int MaximumParticipants = 4;

        private readonly List<ParticipantRecord> _records = new List<ParticipantRecord>(MaximumParticipants);
        private ulong _nextId = 1;

        public int Count => _records.Count;

        public IReadOnlyList<ParticipantRecord> Records => _records;

        public ParticipantRegistrationResult TryRegister(
            ulong ownerClientId,
            int localSlot,
            out ParticipantRecord participant)
        {
            participant = default;
            if (localSlot < 0 || localSlot >= MaximumParticipants)
            {
                return ParticipantRegistrationResult.InvalidLocalSlot;
            }

            for (int index = 0; index < _records.Count; index++)
            {
                ParticipantRecord existing = _records[index];
                if (existing.OwnerClientId == ownerClientId && existing.LocalSlot == localSlot)
                {
                    participant = existing;
                    return ParticipantRegistrationResult.LocalSlotAlreadyRegistered;
                }
            }

            if (_records.Count >= MaximumParticipants)
            {
                return ParticipantRegistrationResult.MatchIsFull;
            }

            participant = new ParticipantRecord(new ParticipantId(_nextId++), ownerClientId, (byte)localSlot);
            _records.Add(participant);
            return ParticipantRegistrationResult.Success;
        }

        public bool TryGet(ulong ownerClientId, int localSlot, out ParticipantRecord participant)
        {
            for (int index = 0; index < _records.Count; index++)
            {
                ParticipantRecord existing = _records[index];
                if (existing.OwnerClientId == ownerClientId && existing.LocalSlot == localSlot)
                {
                    participant = existing;
                    return true;
                }
            }

            participant = default;
            return false;
        }
    }
}
