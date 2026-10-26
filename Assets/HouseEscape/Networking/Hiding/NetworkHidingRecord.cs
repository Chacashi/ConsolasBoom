using System;
using HouseEscape.Gameplay.Hiding;
using Unity.Netcode;

namespace HouseEscape.Networking.Hiding
{
    /// <summary>
    /// Replicated hiding state of one participant: phase, hiding place and the server times at which
    /// the phase started, ends on its own and the cooldown ends. Rewritten only on phase changes.
    /// </summary>
    public struct NetworkHidingRecord : INetworkSerializable, IEquatable<NetworkHidingRecord>
    {
        public ulong ParticipantId;
        public byte Phase;
        public sbyte SpotIndex;
        public byte SpotType;
        public double PhaseStartedAt;
        public double PhaseEndsAt;
        public double CooldownEndsAt;
        public bool IsProtected;

        public NetworkHidingRecord(HidingStatus status)
        {
            ParticipantId = status.ParticipantId.Value;
            Phase = (byte)status.Phase;
            SpotIndex = (sbyte)Math.Max(-1, Math.Min(sbyte.MaxValue, status.SpotIndex));
            SpotType = (byte)status.SpotType;
            PhaseStartedAt = status.PhaseStartedAt;
            PhaseEndsAt = status.PhaseEndsAt;
            CooldownEndsAt = status.CooldownEndsAt;
            IsProtected = status.IsProtected;
        }

        /// <summary>Client view; whether the monster was lost is server-only and not replicated.</summary>
        public HidingStatus ToStatus()
        {
            return new HidingStatus(
                ParticipantId == 0
                    ? HouseEscape.Core.Participants.ParticipantId.None
                    : new HouseEscape.Core.Participants.ParticipantId(ParticipantId),
                (HidingPhase)Phase,
                SpotIndex,
                (HidingSpotType)SpotType,
                PhaseStartedAt,
                PhaseEndsAt,
                CooldownEndsAt,
                IsProtected,
                false);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref SpotIndex);
            serializer.SerializeValue(ref SpotType);
            serializer.SerializeValue(ref PhaseStartedAt);
            serializer.SerializeValue(ref PhaseEndsAt);
            serializer.SerializeValue(ref CooldownEndsAt);
            serializer.SerializeValue(ref IsProtected);
        }

        public bool Equals(NetworkHidingRecord other)
        {
            return ParticipantId == other.ParticipantId
                && Phase == other.Phase
                && SpotIndex == other.SpotIndex
                && SpotType == other.SpotType
                && PhaseStartedAt.Equals(other.PhaseStartedAt)
                && PhaseEndsAt.Equals(other.PhaseEndsAt)
                && CooldownEndsAt.Equals(other.CooldownEndsAt)
                && IsProtected == other.IsProtected;
        }

        public override bool Equals(object obj) => obj is NetworkHidingRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Phase;
                hashCode = (hashCode * 397) ^ SpotIndex;
                hashCode = (hashCode * 397) ^ PhaseStartedAt.GetHashCode();
                return (hashCode * 397) ^ CooldownEndsAt.GetHashCode();
            }
        }
    }
}
