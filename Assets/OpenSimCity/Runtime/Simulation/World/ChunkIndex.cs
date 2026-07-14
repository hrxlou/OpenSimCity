using System;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.World
{
    /// <summary>
    /// Deterministic lookup metadata for one chunk in a finite world grid.
    /// Chunks are ordered row-major (chunk Y first, then chunk X).
    /// </summary>
    [Serializable]
    public readonly struct ChunkIndex : IEquatable<ChunkIndex>, IComparable<ChunkIndex>
    {
        public ChunkIndex(
            ChunkCoord coordinate,
            int index,
            CellCoord origin,
            int width,
            int height,
            int firstCellIndex)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "A chunk index cannot be negative.");
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "A chunk width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "A chunk height must be positive.");
            if (firstCellIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(firstCellIndex), "A cell index cannot be negative.");

            Coordinate = coordinate;
            Index = index;
            Origin = origin;
            Width = width;
            Height = height;
            FirstCellIndex = firstCellIndex;
        }

        public ChunkCoord Coordinate { get; }

        public ChunkCoord Chunk => Coordinate;

        /// <summary>Row-major index in the world's chunk index array.</summary>
        public int Index { get; }

        public CellCoord Origin { get; }

        public int Width { get; }

        public int Height { get; }

        public int CellWidth => Width;

        public int CellHeight => Height;

        /// <summary>
        /// Row-major index of the chunk's top-left cell. Rows inside a chunk
        /// are not assumed to be contiguous in the world cell array.
        /// </summary>
        public int FirstCellIndex { get; }

        public int CellStartIndex => FirstCellIndex;

        public int CellCount => checked(Width * Height);

        public bool Contains(CellCoord coordinate)
        {
            var maxX = (long)Origin.X + Width;
            var maxY = (long)Origin.Y + Height;
            return coordinate.X >= Origin.X && coordinate.X < maxX &&
                   coordinate.Y >= Origin.Y && coordinate.Y < maxY;
        }

        public int CompareTo(ChunkIndex other)
        {
            var indexComparison = Index.CompareTo(other.Index);
            return indexComparison != 0
                ? indexComparison
                : Coordinate.CompareTo(other.Coordinate);
        }

        public bool Equals(ChunkIndex other)
        {
            return Coordinate == other.Coordinate &&
                   Index == other.Index &&
                   Origin == other.Origin &&
                   Width == other.Width &&
                   Height == other.Height &&
                   FirstCellIndex == other.FirstCellIndex;
        }

        public override bool Equals(object obj) => obj is ChunkIndex other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Coordinate.GetHashCode();
                hash = (hash * 397) ^ Index;
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ Width;
                hash = (hash * 397) ^ Height;
                return (hash * 397) ^ FirstCellIndex;
            }
        }

        public override string ToString()
        {
            return $"{Coordinate} [index={Index}, origin={Origin}, size={Width}x{Height}]";
        }

        public static bool operator ==(ChunkIndex left, ChunkIndex right) => left.Equals(right);

        public static bool operator !=(ChunkIndex left, ChunkIndex right) => !left.Equals(right);

        public static bool operator <(ChunkIndex left, ChunkIndex right) => left.CompareTo(right) < 0;

        public static bool operator >(ChunkIndex left, ChunkIndex right) => left.CompareTo(right) > 0;

        public static bool operator <=(ChunkIndex left, ChunkIndex right) => left.CompareTo(right) <= 0;

        public static bool operator >=(ChunkIndex left, ChunkIndex right) => left.CompareTo(right) >= 0;
    }
}
