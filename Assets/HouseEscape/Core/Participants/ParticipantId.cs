using System;

namespace HouseEscape.Core.Participants
{
    public readonly struct ParticipantId : IEquatable<ParticipantId>
    {
        public static readonly ParticipantId None = default;

        public ParticipantId(ulong value)
        {
            if (value == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A participant ID must be greater than zero.");
            }

            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(ParticipantId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ParticipantId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => IsValid ? Value.ToString() : "None";

        public static bool operator ==(ParticipantId left, ParticipantId right) => left.Equals(right);

        public static bool operator !=(ParticipantId left, ParticipantId right) => !left.Equals(right);
    }
}
