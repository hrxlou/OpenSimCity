using System;

namespace OpenSimCity.Core.Mathematics
{
    [Serializable]
    public readonly struct CellCoord : IEquatable<CellCoord>, IComparable<CellCoord>
    {
        public static readonly CellCoord Zero = new CellCoord(0, 0);

        public CellCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public int ManhattanDistanceTo(CellCoord other)
        {
            var distance = Math.Abs((long)X - other.X) + Math.Abs((long)Y - other.Y);
            return checked((int)distance);
        }

        public long LongManhattanDistanceTo(CellCoord other)
        {
            return Math.Abs((long)X - other.X) + Math.Abs((long)Y - other.Y);
        }

        public CellCoord Offset(int deltaX, int deltaY)
        {
            return new CellCoord(
                checked(X + deltaX),
                checked(Y + deltaY));
        }

        public ChunkCoord ToChunkCoord(int chunkSize)
        {
            return ChunkCoord.FromCell(this, chunkSize);
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
                // This is a collection hash only. It must never be used as a
                // persisted seed or other cross-run simulation input.
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() => $"({X}, {Y})";

        public static CellCoord operator +(CellCoord left, CellCoord right)
        {
            return new CellCoord(
                checked(left.X + right.X),
                checked(left.Y + right.Y));
        }

        public static CellCoord operator -(CellCoord left, CellCoord right)
        {
            return new CellCoord(
                checked(left.X - right.X),
                checked(left.Y - right.Y));
        }

        public static bool operator ==(CellCoord left, CellCoord right) => left.Equals(right);

        public static bool operator !=(CellCoord left, CellCoord right) => !left.Equals(right);

        public static bool operator <(CellCoord left, CellCoord right) => left.CompareTo(right) < 0;

        public static bool operator >(CellCoord left, CellCoord right) => left.CompareTo(right) > 0;

        public static bool operator <=(CellCoord left, CellCoord right) => left.CompareTo(right) <= 0;

        public static bool operator >=(CellCoord left, CellCoord right) => left.CompareTo(right) >= 0;
    }
}
