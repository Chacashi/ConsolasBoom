using System;

namespace HouseEscape.Gameplay.Movement
{
    public readonly struct MovementInput : IEquatable<MovementInput>
    {
        public static readonly MovementInput Zero = default;

        public MovementInput(float moveX, float moveY, float lookX, float lookY)
            : this(moveX, moveY, lookX, lookY, false)
        {
        }

        /// <param name="isRunRequested">Run intention (held L3); the server decides whether it applies.</param>
        public MovementInput(float moveX, float moveY, float lookX, float lookY, bool isRunRequested)
        {
            MoveX = moveX;
            MoveY = moveY;
            LookX = lookX;
            LookY = lookY;
            IsRunRequested = isRunRequested;
        }

        public float MoveX { get; }
        public float MoveY { get; }
        public float LookX { get; }
        public float LookY { get; }
        public bool IsRunRequested { get; }

        public bool HasMovement => MoveX != 0f || MoveY != 0f;

        public bool IsFiniteAndInRange()
        {
            return IsFiniteUnit(MoveX)
                && IsFiniteUnit(MoveY)
                && IsFiniteUnit(LookX)
                && IsFiniteUnit(LookY);
        }

        public bool Equals(MovementInput other)
        {
            return MoveX.Equals(other.MoveX)
                && MoveY.Equals(other.MoveY)
                && LookX.Equals(other.LookX)
                && LookY.Equals(other.LookY)
                && IsRunRequested == other.IsRunRequested;
        }

        public override bool Equals(object obj) => obj is MovementInput other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = MoveX.GetHashCode();
                hashCode = (hashCode * 397) ^ MoveY.GetHashCode();
                hashCode = (hashCode * 397) ^ LookX.GetHashCode();
                hashCode = (hashCode * 397) ^ LookY.GetHashCode();
                return (hashCode * 397) ^ IsRunRequested.GetHashCode();
            }
        }

        private static bool IsFiniteUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= -1f && value <= 1f;
        }
    }
}
