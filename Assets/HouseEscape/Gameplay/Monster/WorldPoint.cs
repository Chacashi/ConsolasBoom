using System;

namespace HouseEscape.Gameplay.Monster
{
    /// <summary>Engine-independent world position in metres.</summary>
    public readonly struct WorldPoint : IEquatable<WorldPoint>
    {
        public WorldPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static float DistanceSquared(WorldPoint a, WorldPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public bool Equals(WorldPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is WorldPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                return (hashCode * 397) ^ Z.GetHashCode();
            }
        }

        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
    }
}
