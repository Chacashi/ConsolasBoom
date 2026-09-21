using System;

namespace HouseEscape.Core.Timeline
{
    public readonly struct InGameTime : IEquatable<InGameTime>
    {
        public InGameTime(int hour, int minute)
        {
            Hour = hour;
            Minute = minute;
        }

        public int Hour { get; }

        public int Minute { get; }

        public bool Equals(InGameTime other)
        {
            return Hour == other.Hour && Minute == other.Minute;
        }

        public override bool Equals(object obj)
        {
            return obj is InGameTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Hour * 397) ^ Minute;
            }
        }

        public override string ToString()
        {
            return Hour == 0 ? $"12:{Minute:00} AM" : $"{Hour}:{Minute:00} AM";
        }
    }
}
