using System;

namespace OpenSimCity.Simulation.Buildings
{
    [Serializable]
    public readonly struct BuildingFootprint : IEquatable<BuildingFootprint>
    {
        public BuildingFootprint(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Building footprint width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Building footprint height must be positive.");

            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public int Area => checked(Width * Height);

        public bool Equals(BuildingFootprint other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is BuildingFootprint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString() => $"{Width}x{Height}";

        public static bool operator ==(BuildingFootprint left, BuildingFootprint right) => left.Equals(right);

        public static bool operator !=(BuildingFootprint left, BuildingFootprint right) => !left.Equals(right);
    }
}
