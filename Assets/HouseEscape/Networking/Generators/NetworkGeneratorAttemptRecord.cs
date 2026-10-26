using System;
using Unity.Netcode;

namespace HouseEscape.Networking.Generators
{
    /// <summary>
    /// Replicated in-progress attempt. Clients derive hold progress locally from the
    /// server-relative start time and synchronized network time.
    /// </summary>
    public struct NetworkGeneratorAttemptRecord : INetworkSerializable, IEquatable<NetworkGeneratorAttemptRecord>
    {
        public ulong AttemptId;
        public ulong ParticipantId;
        public byte GeneratorNumber;
        public double StartedAt;

        public NetworkGeneratorAttemptRecord(ulong attemptId, ulong participantId, byte generatorNumber, double startedAt)
        {
            AttemptId = attemptId;
            ParticipantId = participantId;
            GeneratorNumber = generatorNumber;
            StartedAt = startedAt;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AttemptId);
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref GeneratorNumber);
            serializer.SerializeValue(ref StartedAt);
        }

        public bool Equals(NetworkGeneratorAttemptRecord other)
        {
            return AttemptId == other.AttemptId
                && ParticipantId == other.ParticipantId
                && GeneratorNumber == other.GeneratorNumber
                && StartedAt.Equals(other.StartedAt);
        }

        public override bool Equals(object obj) => obj is NetworkGeneratorAttemptRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = AttemptId.GetHashCode();
                hashCode = (hashCode * 397) ^ ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ GeneratorNumber.GetHashCode();
                return (hashCode * 397) ^ StartedAt.GetHashCode();
            }
        }
    }
}
