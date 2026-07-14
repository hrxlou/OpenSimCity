using System;

namespace OpenSimCity.Core.Mathematics
{
    /// <summary>
    /// Identifies a chunk in the simulation grid. Chunk coordinates use the
    /// same row-major ordering as <see cref="CellCoord"/>: Y first, then X.
    /// </summary>
    [Serializable]
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>, IComparable<ChunkCoord>
    {
        public static readonly ChunkCoord Zero = new ChunkCoord(0, 0);

        public ChunkCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public ChunkCoord Offset(int deltaX, int deltaY)
        {
            return new ChunkCoord(
                checked(X + deltaX),
                checked(Y + deltaY));
        }

        public CellCoord OriginCell(int chunkSize)
        {
            ValidateChunkSize(chunkSize);
            return new CellCoord(
                checked(X * chunkSize),
                checked(Y * chunkSize));
        }

        public int CompareTo(ChunkCoord other)
        {
            var yComparison = Y.CompareTo(other.Y);
            return yComparison != 0 ? yComparison : X.CompareTo(other.X);
        }

        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);

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

        public static ChunkCoord FromCell(CellCoord cell, int chunkSize)
        {
            ValidateChunkSize(chunkSize);
            return new ChunkCoord(
                FloorDivide(cell.X, chunkSize),
                FloorDivide(cell.Y, chunkSize));
        }

        public static ChunkCoord operator +(ChunkCoord left, ChunkCoord right)
        {
            return new ChunkCoord(
                checked(left.X + right.X),
                checked(left.Y + right.Y));
        }

        public static ChunkCoord operator -(ChunkCoord left, ChunkCoord right)
        {
            return new ChunkCoord(
                checked(left.X - right.X),
                checked(left.Y - right.Y));
        }

        public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);

        public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);

        public static bool operator <(ChunkCoord left, ChunkCoord right) => left.CompareTo(right) < 0;

        public static bool operator >(ChunkCoord left, ChunkCoord right) => left.CompareTo(right) > 0;

        public static bool operator <=(ChunkCoord left, ChunkCoord right) => left.CompareTo(right) <= 0;

        public static bool operator >=(ChunkCoord left, ChunkCoord right) => left.CompareTo(right) >= 0;

        private static int FloorDivide(int value, int divisor)
        {
            var quotient = value / divisor;
            if (value % divisor < 0)
                quotient--;

            return quotient;
        }

        private static void ValidateChunkSize(int chunkSize)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");
        }
    }
}
