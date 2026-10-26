using System;
using Unity.Netcode;

namespace HouseEscape.Networking.Scoring
{
    public struct NetworkParticipantScore : INetworkSerializable, IEquatable<NetworkParticipantScore>
    {
        public ulong ParticipantId;
        public int Score;

        public NetworkParticipantScore(ulong participantId, int score)
        {
            ParticipantId = participantId;
            Score = score;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Score);
        }

        public bool Equals(NetworkParticipantScore other)
        {
            return ParticipantId == other.ParticipantId && Score == other.Score;
        }

        public override bool Equals(object obj) => obj is NetworkParticipantScore other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ParticipantId.GetHashCode() * 397) ^ Score;
            }
        }
    }
}
