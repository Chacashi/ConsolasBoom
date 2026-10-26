using System;
using HouseEscape.Gameplay.Vitality;
using Unity.Netcode;

namespace HouseEscape.Networking.Vitality
{
    /// <summary>
    /// Replicated participant condition. The recovery deadline is a server time from which
    /// clients derive the unconscious countdown locally. Energy is replicated as its whole
    /// percentage, so the server rewrites the record only when that integer changes (at most a
    /// few times per second while running) instead of every frame.
    /// </summary>
    public struct NetworkParticipantVitalityRecord : INetworkSerializable, IEquatable<NetworkParticipantVitalityRecord>
    {
        public ulong ParticipantId;
        public byte Health;
        public byte State;
        public double RecoveryDeadline;
        public byte Energy;

        public NetworkParticipantVitalityRecord(ParticipantVitality vitality)
        {
            ParticipantId = vitality.ParticipantId.Value;
            Health = (byte)Math.Max(0, Math.Min(byte.MaxValue, vitality.Health));
            State = (byte)vitality.State;
            RecoveryDeadline = vitality.RecoveryDeadline;
            Energy = (byte)vitality.WholeEnergy;
        }

        public ParticipantVitality ToVitality()
        {
            return new ParticipantVitality(
                ParticipantId == 0
                    ? HouseEscape.Core.Participants.ParticipantId.None
                    : new HouseEscape.Core.Participants.ParticipantId(ParticipantId),
                Health,
                (VitalityState)State,
                RecoveryDeadline,
                Energy);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref RecoveryDeadline);
            serializer.SerializeValue(ref Energy);
        }

        public bool Equals(NetworkParticipantVitalityRecord other)
        {
            return ParticipantId == other.ParticipantId
                && Health == other.Health
                && State == other.State
                && RecoveryDeadline.Equals(other.RecoveryDeadline)
                && Energy == other.Energy;
        }

        public override bool Equals(object obj) => obj is NetworkParticipantVitalityRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Health;
                hashCode = (hashCode * 397) ^ State;
                hashCode = (hashCode * 397) ^ RecoveryDeadline.GetHashCode();
                return (hashCode * 397) ^ Energy;
            }
        }
    }
}
