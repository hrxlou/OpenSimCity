using System;

namespace OpenSimCity.Core.Mathematics
{
    [Serializable]
    public readonly struct CellCoord : IEquatable<CellCoord>, IComparable<CellCoord>
    {
        public CellCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public int ManhattanDistanceTo(CellCoord other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public int CompareTo(CellCoord other)
        {
            var yComparison = Y.CompareTo(other.Y);
            return yComparison != 0 ? yComparison : X.CompareTo(other.X);
        }

        public bool Equals(CellCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is CellCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(CellCoord left, CellCoord right) => left.Equals(right);

        public static bool operator !=(CellCoord left, CellCoord right) => !left.Equals(right);
    }
}
