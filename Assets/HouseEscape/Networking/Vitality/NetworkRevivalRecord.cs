using System;
using Unity.Netcode;

namespace HouseEscape.Networking.Vitality
{
    /// <summary>
    /// Replicated revival hold. Clients derive progress locally from the server start time and
    /// synchronized time; nothing is replicated per frame.
    /// </summary>
    public struct NetworkRevivalRecord : INetworkSerializable, IEquatable<NetworkRevivalRecord>
    {
        public ulong ReviverId;
        public ulong TargetId;
        public double StartedAt;

        public NetworkRevivalRecord(ulong reviverId, ulong targetId, double startedAt)
        {
            ReviverId = reviverId;
            TargetId = targetId;
            StartedAt = startedAt;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ReviverId);
            serializer.SerializeValue(ref TargetId);
            serializer.SerializeValue(ref StartedAt);
        }

        public bool Equals(NetworkRevivalRecord other)
        {
            return ReviverId == other.ReviverId && TargetId == other.TargetId && StartedAt.Equals(other.StartedAt);
        }

        public override bool Equals(object obj) => obj is NetworkRevivalRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ReviverId.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetId.GetHashCode();
                return (hashCode * 397) ^ StartedAt.GetHashCode();
            }
        }
    }
}
