using System;
using HouseEscape.Core.Timeline;
using Unity.Netcode;

namespace HouseEscape.Networking.Timeline
{
    public struct NetworkMatchTimelineSnapshot : INetworkSerializable, IEquatable<NetworkMatchTimelineSnapshot>
    {
        public bool IsStarted;
        public bool IsComplete;
        public byte LightPhase;
        public double MatchStartedAt;
        public double MatchDeadline;
        public double PhaseEndsAt;
        public uint Revision;

        public NetworkMatchTimelineSnapshot(MatchTimelineSnapshot snapshot)
        {
            IsStarted = snapshot.IsStarted;
            IsComplete = snapshot.IsComplete;
            LightPhase = (byte)snapshot.LightPhase;
            MatchStartedAt = snapshot.MatchStartedAt;
            MatchDeadline = snapshot.MatchDeadline;
            PhaseEndsAt = snapshot.PhaseEndsAt;
            Revision = snapshot.Revision;
        }

        public MatchTimelineSnapshot ToCoreSnapshot()
        {
            return new MatchTimelineSnapshot(
                IsStarted,
                IsComplete,
                (HouseEscape.Core.Timeline.LightPhase)LightPhase,
                MatchStartedAt,
                MatchDeadline,
                PhaseEndsAt,
                Revision);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsStarted);
            serializer.SerializeValue(ref IsComplete);
            serializer.SerializeValue(ref LightPhase);
            serializer.SerializeValue(ref MatchStartedAt);
            serializer.SerializeValue(ref MatchDeadline);
            serializer.SerializeValue(ref PhaseEndsAt);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkMatchTimelineSnapshot other)
        {
            return IsStarted == other.IsStarted
                && IsComplete == other.IsComplete
                && LightPhase == other.LightPhase
                && MatchStartedAt.Equals(other.MatchStartedAt)
                && MatchDeadline.Equals(other.MatchDeadline)
                && PhaseEndsAt.Equals(other.PhaseEndsAt)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkMatchTimelineSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IsStarted.GetHashCode();
                hashCode = (hashCode * 397) ^ IsComplete.GetHashCode();
                hashCode = (hashCode * 397) ^ LightPhase.GetHashCode();
                hashCode = (hashCode * 397) ^ MatchStartedAt.GetHashCode();
                hashCode = (hashCode * 397) ^ MatchDeadline.GetHashCode();
                hashCode = (hashCode * 397) ^ PhaseEndsAt.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Revision;
                return hashCode;
            }
        }
    }
}
