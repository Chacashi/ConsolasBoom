using System;
using HouseEscape.Core.Participants;

namespace HouseEscape.Gameplay.Flashlight
{
    /// <summary>
    /// One participant's flashlight: battery 0-100 and whether it is on. On clients the battery
    /// is the replicated whole percentage.
    /// </summary>
    public readonly struct ParticipantFlashlight : IEquatable<ParticipantFlashlight>
    {
        public ParticipantFlashlight(ParticipantId participantId, float battery, bool isOn)
        {
            ParticipantId = participantId;
            Battery = battery;
            IsOn = isOn;
        }

        public ParticipantId ParticipantId { get; }
        public float Battery { get; }
        public bool IsOn { get; }
        public int WholeBattery => FlashlightRules.ToWholeBattery(Battery);

        public bool Equals(ParticipantFlashlight other)
        {
            return ParticipantId == other.ParticipantId && Battery.Equals(other.Battery) && IsOn == other.IsOn;
        }

        public override bool Equals(object obj) => obj is ParticipantFlashlight other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ParticipantId.GetHashCode();
                hashCode = (hashCode * 397) ^ Battery.GetHashCode();
                return (hashCode * 397) ^ IsOn.GetHashCode();
            }
        }
    }
}
