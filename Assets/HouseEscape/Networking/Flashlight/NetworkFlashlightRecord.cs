using System;
using HouseEscape.Gameplay.Flashlight;
using Unity.Netcode;

namespace HouseEscape.Networking.Flashlight
{
    /// <summary>
    /// Replicated flashlight of one participant. The battery is replicated as its whole
    /// percentage, so the server rewrites it at most a few times per second. Charging marks a
    /// participant holding a charging station.
    /// </summary>
    public struct NetworkFlashlightRecord : INetworkSerializable, IEquatable<NetworkFlashlightRecord>
    {
        public ulong ParticipantId;
        public byte Battery;
        public bool IsOn;
        public bool IsCharging;

        public NetworkFlashlightRecord(ParticipantFlashlight flashlight, bool isCharging)
        {
            ParticipantId = flashlight.ParticipantId.Value;
            Battery = (byte)flashlight.WholeBattery;
            IsOn = flashlight.IsOn;
            IsCharging = isCharging;
        }

        public ParticipantFlashlight ToFlashlight()
        {
            return new ParticipantFlashlight(
                ParticipantId == 0
                    ? HouseEscape.Core.Participants.ParticipantId.None
                    : new HouseEscape.Core.Participants.ParticipantId(ParticipantId),
                Battery,
                IsOn);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Battery);
            serializer.SerializeValue(ref IsOn);
            serializer.SerializeValue(ref IsCharging);
        }

        public bool Equals(NetworkFlashlightRecord other)
        {
            return ParticipantId == other.ParticipantId
                && Battery == other.Battery
                && IsOn == other.IsOn
                && IsCharging == other.IsCharging;
        }

        public override bool Equals(object obj) => obj is NetworkFlashlightRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Battery;
                hashCode = (hashCode * 397) ^ IsOn.GetHashCode();
                return (hashCode * 397) ^ IsCharging.GetHashCode();
            }
        }
    }
}
