using System;

namespace OpenSimCity.Simulation.World
{
    /// <summary>
    /// Serializable input for creating a world grid. Chunk metadata and dirty
    /// flags are derived runtime data and are intentionally not persisted here.
    /// </summary>
    [Serializable]
    public sealed class WorldStateDto
    {
        public WorldStateDto()
        {
            Width = WorldState.DefaultWidth;
            Height = WorldState.DefaultHeight;
            ChunkSize = WorldState.DefaultChunkSize;
        }

        public WorldStateDto(int width, int height, ulong seed)
            : this(width, height, WorldState.DefaultChunkSize, seed)
        {
        }

        public WorldStateDto(int width, int height, int seed)
            : this(width, height, WorldState.DefaultChunkSize, ToSeed(seed))
        {
        }

        public WorldStateDto(int width, int height, int chunkSize, ulong seed)
        {
            Width = width;
            Height = height;
            ChunkSize = chunkSize;
            Seed = seed;
        }

        public WorldStateDto(int width, int height, int chunkSize, int seed)
            : this(width, height, chunkSize, ToSeed(seed))
        {
        }

        public int Width;

        public int Height;

        public int ChunkSize;

        public ulong Seed;

        private static ulong ToSeed(int seed)
        {
            if (seed < 0)
                throw new ArgumentOutOfRangeException(nameof(seed), "A world seed cannot be negative.");

            return (ulong)seed;
        }
    }
}
