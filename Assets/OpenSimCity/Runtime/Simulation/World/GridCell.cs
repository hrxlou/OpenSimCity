using System;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.World
{
    /// <summary>
    /// Immutable address of one cell in a <see cref="WorldState"/>.
    ///
    /// Cell state is intentionally not coupled to a Unity object. Systems can
    /// use the coordinate or row-major index to look up their own state arrays
    /// without making the grid depend on presentation concerns.
    /// </summary>
    [Serializable]
    public readonly struct GridCell : IEquatable<GridCell>, IComparable<GridCell>
    {
        public GridCell(CellCoord coordinate, int index, ChunkCoord chunk)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "A cell index cannot be negative.");

            Coordinate = coordinate;
            Index = index;
            Chunk = chunk;
        }

        public CellCoord Coordinate { get; }

        /// <summary>Short alias useful when adapting a grid query to a command.</summary>
        public CellCoord Coord => Coordinate;

        /// <summary>Row-major index: <c>Y * WorldState.Width + X</c>.</summary>
        public int Index { get; }

        public ChunkCoord Chunk { get; }

        public ChunkCoord ChunkCoordinate => Chunk;

        public int X => Coordinate.X;

        public int Y => Coordinate.Y;

        public int CompareTo(GridCell other)
        {
            var indexComparison = Index.CompareTo(other.Index);
            return indexComparison != 0
                ? indexComparison
                : Coordinate.CompareTo(other.Coordinate);
        }

        public bool Equals(GridCell other)
        {
            return Index == other.Index &&
                   Coordinate == other.Coordinate &&
                   Chunk == other.Chunk;
        }

        public override bool Equals(object obj) => obj is GridCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Coordinate.GetHashCode() * 397) ^ Index) * 397 ^ Chunk.GetHashCode();
            }
        }

        public override string ToString() => $"{Coordinate} [index={Index}, chunk={Chunk}]";

        public static bool operator ==(GridCell left, GridCell right) => left.Equals(right);

        public static bool operator !=(GridCell left, GridCell right) => !left.Equals(right);

        public static bool operator <(GridCell left, GridCell right) => left.CompareTo(right) < 0;

        public static bool operator >(GridCell left, GridCell right) => left.CompareTo(right) > 0;

        public static bool operator <=(GridCell left, GridCell right) => left.CompareTo(right) <= 0;

        public static bool operator >=(GridCell left, GridCell right) => left.CompareTo(right) >= 0;
    }
}
