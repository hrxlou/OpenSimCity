using System;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Read-only spatial contract consumed by presentation-facing gameplay systems.
    /// It contains world dimensions, not the simulation cell storage.
    /// </summary>
    [Serializable]
    public readonly struct WorldBoundsReadModel : IEquatable<WorldBoundsReadModel>
    {
        public WorldBoundsReadModel(int width, int height, float cellSize)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "World width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "World height must be positive.");
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and positive.");

            Width = width;
            Height = height;
            CellSize = cellSize;
        }

        public int Width { get; }

        public int Height { get; }

        public float CellSize { get; }

        public float WorldWidth => Width * CellSize;

        public float WorldHeight => Height * CellSize;

        public float MinX => 0f;

        public float MaxX => WorldWidth;

        public float MinZ => 0f;

        public float MaxZ => WorldHeight;

        public float CenterX => WorldWidth * 0.5f;

        public float CenterZ => WorldHeight * 0.5f;

        public static WorldBoundsReadModel From(WorldState worldState, float cellSize = 1f)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            return new WorldBoundsReadModel(worldState.Width, worldState.Height, cellSize);
        }

        public bool Equals(WorldBoundsReadModel other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   CellSize.Equals(other.CellSize);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldBoundsReadModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ CellSize.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Width}x{Height} cells at {CellSize:0.###} units per cell";
        }

        public static bool operator ==(WorldBoundsReadModel left, WorldBoundsReadModel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldBoundsReadModel left, WorldBoundsReadModel right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Boundary used to provide world size without exposing simulation arrays.
    /// </summary>
    public interface IWorldBoundsQuery
    {
        WorldBoundsReadModel ReadWorldBounds();
    }
}
