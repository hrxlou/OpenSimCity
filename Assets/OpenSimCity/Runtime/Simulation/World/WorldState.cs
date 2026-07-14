using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.World
{
    /// <summary>
    /// Pure runtime storage for a finite simulation grid.
    ///
    /// The default world is 64x64 cells split into 16x16 chunks. Cell and
    /// chunk arrays are allocated once by the constructor; all lookup and
    /// dirty-marking operations reuse those arrays.
    /// </summary>
    [Serializable]
    public sealed class WorldState
    {
        public const int DefaultWidth = 64;
        public const int DefaultHeight = 64;
        public const int DefaultChunkSize = 16;

        private readonly GridCell[] _cells;
        private readonly ChunkIndex[] _chunkIndices;
        private readonly bool[] _dirtyChunks;
        private readonly IReadOnlyList<GridCell> _readOnlyCells;
        private readonly IReadOnlyList<ChunkIndex> _readOnlyChunkIndices;
        private int _dirtyChunkCount;

        public WorldState()
            : this(DefaultWidth, DefaultHeight, DefaultChunkSize, 0UL)
        {
        }

        public WorldState(ulong seed)
            : this(DefaultWidth, DefaultHeight, DefaultChunkSize, seed)
        {
        }

        public WorldState(int seed)
            : this(DefaultWidth, DefaultHeight, DefaultChunkSize, ToSeed(seed))
        {
        }

        public WorldState(int width, int height, ulong seed)
            : this(width, height, DefaultChunkSize, seed)
        {
        }

        public WorldState(int width, int height, int seed)
            : this(width, height, DefaultChunkSize, ToSeed(seed))
        {
        }

        public WorldState(int width, int height, int chunkSize, ulong seed)
        {
            ValidateDimensions(width, height, chunkSize, out var cellCount, out var chunkColumns, out var chunkRows, out var chunkCount);

            Width = width;
            Height = height;
            ChunkSize = chunkSize;
            Seed = seed;
            CellCount = cellCount;
            ChunkColumns = chunkColumns;
            ChunkRows = chunkRows;
            ChunkCount = chunkCount;

            _cells = new GridCell[cellCount];
            _chunkIndices = new ChunkIndex[chunkCount];
            _dirtyChunks = new bool[chunkCount];
            _readOnlyCells = Array.AsReadOnly(_cells);
            _readOnlyChunkIndices = Array.AsReadOnly(_chunkIndices);

            BuildChunkIndex();
            BuildCells();
        }

        public WorldState(int width, int height, int chunkSize, int seed)
            : this(width, height, chunkSize, ToSeed(seed))
        {
        }

        public WorldState(WorldStateDto dto)
            : this(GetWidth(dto), GetHeight(dto), GetChunkSize(dto), GetSeed(dto))
        {
        }

        public int Width { get; }

        public int Height { get; }

        public int ChunkSize { get; }

        public ulong Seed { get; }

        public int CellCount { get; }

        public int ChunkColumns { get; }

        public int ChunkRows { get; }

        public int ChunkCount { get; }

        public int MapWidth => Width;

        public int MapHeight => Height;

        public IReadOnlyList<GridCell> Cells => _readOnlyCells;

        public IReadOnlyList<ChunkIndex> Chunks => _readOnlyChunkIndices;

        public IReadOnlyList<ChunkIndex> ChunkIndices => _readOnlyChunkIndices;

        public int DirtyChunkCount => _dirtyChunkCount;

        public bool HasDirtyChunks => _dirtyChunkCount != 0;

        /// <summary>
        /// Enumerates dirty chunks in deterministic chunk row-major order.
        /// </summary>
        public IEnumerable<ChunkIndex> DirtyChunks => EnumerateDirtyChunks();

        public IEnumerable<ChunkCoord> DirtyChunkCoordinates => EnumerateDirtyChunkCoordinates();

        public WorldStateDto ToDto()
        {
            return new WorldStateDto(Width, Height, ChunkSize, Seed);
        }

        public bool IsInBounds(CellCoord coordinate)
        {
            return (uint)coordinate.X < (uint)Width &&
                   (uint)coordinate.Y < (uint)Height;
        }

        public bool Contains(CellCoord coordinate) => IsInBounds(coordinate);

        public bool TryGetIndex(CellCoord coordinate, out int index)
        {
            if (!IsInBounds(coordinate))
            {
                index = -1;
                return false;
            }

            index = coordinate.Y * Width + coordinate.X;
            return true;
        }

        public int GetIndex(CellCoord coordinate)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    $"Cell coordinate {coordinate} is outside {Width}x{Height} world bounds.");
            }

            return index;
        }

        public int CoordinateToIndex(CellCoord coordinate) => GetIndex(coordinate);

        public bool TryCoordinateToIndex(CellCoord coordinate, out int index) => TryGetIndex(coordinate, out index);

        public bool TryGetCoordinate(int index, out CellCoord coordinate)
        {
            if (!IsValidCellIndex(index))
            {
                coordinate = CellCoord.Zero;
                return false;
            }

            coordinate = new CellCoord(index % Width, index / Width);
            return true;
        }

        public CellCoord GetCoordinate(int index)
        {
            if (!TryGetCoordinate(index, out var coordinate))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Cell index {index} is outside the range 0..{CellCount - 1}.");
            }

            return coordinate;
        }

        public CellCoord IndexToCoordinate(int index) => GetCoordinate(index);

        public bool TryGetCell(CellCoord coordinate, out GridCell cell)
        {
            if (!TryGetIndex(coordinate, out var index))
            {
                cell = default;
                return false;
            }

            cell = _cells[index];
            return true;
        }

        public GridCell GetCell(CellCoord coordinate) => _cells[GetIndex(coordinate)];

        public bool TryGetCellAtIndex(int index, out GridCell cell)
        {
            if (!IsValidCellIndex(index))
            {
                cell = default;
                return false;
            }

            cell = _cells[index];
            return true;
        }

        public bool TryGetCell(int index, out GridCell cell) => TryGetCellAtIndex(index, out cell);

        public GridCell GetCellAtIndex(int index)
        {
            if (!TryGetCellAtIndex(index, out var cell))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Cell index {index} is outside the range 0..{CellCount - 1}.");
            }

            return cell;
        }

        public GridCell GetCell(int index) => GetCellAtIndex(index);

        public bool TryGetChunkCoord(CellCoord coordinate, out ChunkCoord chunk)
        {
            if (!IsInBounds(coordinate))
            {
                chunk = ChunkCoord.Zero;
                return false;
            }

            chunk = new ChunkCoord(coordinate.X / ChunkSize, coordinate.Y / ChunkSize);
            return true;
        }

        public ChunkCoord GetChunkCoord(CellCoord coordinate)
        {
            if (!TryGetChunkCoord(coordinate, out var chunk))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    $"Cell coordinate {coordinate} is outside {Width}x{Height} world bounds.");
            }

            return chunk;
        }

        public bool IsChunkInBounds(ChunkCoord coordinate)
        {
            return (uint)coordinate.X < (uint)ChunkColumns &&
                   (uint)coordinate.Y < (uint)ChunkRows;
        }

        public bool TryGetChunkIndex(ChunkCoord coordinate, out int index)
        {
            if (!IsChunkInBounds(coordinate))
            {
                index = -1;
                return false;
            }

            index = coordinate.Y * ChunkColumns + coordinate.X;
            return true;
        }

        public bool TryGetChunkIndex(CellCoord coordinate, out int index)
        {
            if (!TryGetChunkCoord(coordinate, out var chunk))
            {
                index = -1;
                return false;
            }

            return TryGetChunkIndex(chunk, out index);
        }

        public int GetChunkIndex(ChunkCoord coordinate)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coordinate),
                    coordinate,
                    $"Chunk coordinate {coordinate} is outside {ChunkColumns}x{ChunkRows} world bounds.");
            }

            return index;
        }

        public int GetChunkIndex(CellCoord coordinate) => GetChunkIndex(GetChunkCoord(coordinate));

        public bool TryGetChunk(ChunkCoord coordinate, out ChunkIndex chunk)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
            {
                chunk = default;
                return false;
            }

            chunk = _chunkIndices[index];
            return true;
        }

        public bool TryGetChunk(CellCoord coordinate, out ChunkIndex chunk)
        {
            if (!TryGetChunkCoord(coordinate, out var chunkCoordinate))
            {
                chunk = default;
                return false;
            }

            return TryGetChunk(chunkCoordinate, out chunk);
        }

        public ChunkIndex GetChunk(ChunkCoord coordinate) => _chunkIndices[GetChunkIndex(coordinate)];

        public ChunkIndex GetChunk(CellCoord coordinate) => GetChunk(GetChunkCoord(coordinate));

        public bool TryGetChunkAtIndex(int index, out ChunkIndex chunk)
        {
            if (!IsValidChunkIndex(index))
            {
                chunk = default;
                return false;
            }

            chunk = _chunkIndices[index];
            return true;
        }

        public bool TryGetChunk(int index, out ChunkIndex chunk) => TryGetChunkAtIndex(index, out chunk);

        public ChunkIndex GetChunkAtIndex(int index)
        {
            if (!TryGetChunkAtIndex(index, out var chunk))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Chunk index {index} is outside the range 0..{ChunkCount - 1}.");
            }

            return chunk;
        }

        public ChunkIndex GetChunk(int index) => GetChunkAtIndex(index);

        public bool TryMarkChunkDirty(ChunkCoord coordinate)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
                return false;

            MarkChunkDirtyAtIndex(index);
            return true;
        }

        public bool TryMarkChunkDirty(CellCoord coordinate)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
                return false;

            MarkChunkDirtyAtIndex(index);
            return true;
        }

        public void MarkChunkDirty(ChunkCoord coordinate)
        {
            MarkChunkDirtyAtIndex(GetChunkIndex(coordinate));
        }

        public bool MarkChunkDirty(CellCoord coordinate)
        {
            return TryMarkChunkDirty(coordinate);
        }

        public void MarkDirty(ChunkCoord coordinate) => MarkChunkDirty(coordinate);

        public bool MarkDirty(CellCoord coordinate) => MarkChunkDirty(coordinate);

        public bool TryIsChunkDirty(ChunkCoord coordinate, out bool isDirty)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
            {
                isDirty = false;
                return false;
            }

            isDirty = _dirtyChunks[index];
            return true;
        }

        public bool IsChunkDirty(ChunkCoord coordinate) => _dirtyChunks[GetChunkIndex(coordinate)];

        public bool TryIsChunkDirty(int index, out bool isDirty)
        {
            if (!IsValidChunkIndex(index))
            {
                isDirty = false;
                return false;
            }

            isDirty = _dirtyChunks[index];
            return true;
        }

        public bool IsChunkDirty(int index)
        {
            if (!IsValidChunkIndex(index))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Chunk index {index} is outside the range 0..{ChunkCount - 1}.");
            }

            return _dirtyChunks[index];
        }

        public bool TryClearChunkDirty(ChunkCoord coordinate)
        {
            if (!TryGetChunkIndex(coordinate, out var index))
                return false;

            ClearChunkDirtyAtIndex(index);
            return true;
        }

        public void ClearChunkDirty(ChunkCoord coordinate)
        {
            ClearChunkDirtyAtIndex(GetChunkIndex(coordinate));
        }

        public void ClearDirtyChunks()
        {
            if (_dirtyChunkCount == 0)
                return;

            Array.Clear(_dirtyChunks, 0, _dirtyChunks.Length);
            _dirtyChunkCount = 0;
        }

        public void ClearDirty() => ClearDirtyChunks();

        private void BuildChunkIndex()
        {
            for (var chunkY = 0; chunkY < ChunkRows; chunkY++)
            {
                for (var chunkX = 0; chunkX < ChunkColumns; chunkX++)
                {
                    var index = chunkY * ChunkColumns + chunkX;
                    var originX = chunkX * ChunkSize;
                    var originY = chunkY * ChunkSize;
                    var chunkWidth = Math.Min(ChunkSize, Width - originX);
                    var chunkHeight = Math.Min(ChunkSize, Height - originY);
                    var firstCellIndex = originY * Width + originX;

                    _chunkIndices[index] = new ChunkIndex(
                        new ChunkCoord(chunkX, chunkY),
                        index,
                        new CellCoord(originX, originY),
                        chunkWidth,
                        chunkHeight,
                        firstCellIndex);
                }
            }
        }

        private void BuildCells()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var index = y * Width + x;
                    var coordinate = new CellCoord(x, y);
                    _cells[index] = new GridCell(coordinate, index, new ChunkCoord(x / ChunkSize, y / ChunkSize));
                }
            }
        }

        private void MarkChunkDirtyAtIndex(int index)
        {
            if (_dirtyChunks[index])
                return;

            _dirtyChunks[index] = true;
            _dirtyChunkCount++;
        }

        private void ClearChunkDirtyAtIndex(int index)
        {
            if (!_dirtyChunks[index])
                return;

            _dirtyChunks[index] = false;
            _dirtyChunkCount--;
        }

        private IEnumerable<ChunkIndex> EnumerateDirtyChunks()
        {
            for (var index = 0; index < _chunkIndices.Length; index++)
            {
                if (_dirtyChunks[index])
                    yield return _chunkIndices[index];
            }
        }

        private IEnumerable<ChunkCoord> EnumerateDirtyChunkCoordinates()
        {
            for (var index = 0; index < _chunkIndices.Length; index++)
            {
                if (_dirtyChunks[index])
                    yield return _chunkIndices[index].Coordinate;
            }
        }

        private bool IsValidCellIndex(int index) => (uint)index < (uint)CellCount;

        private bool IsValidChunkIndex(int index) => (uint)index < (uint)ChunkCount;

        private static void ValidateDimensions(
            int width,
            int height,
            int chunkSize,
            out int cellCount,
            out int chunkColumns,
            out int chunkRows,
            out int chunkCount)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "World width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "World height must be positive.");
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");

            var cells = (long)width * height;
            if (cells > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "The world contains too many cells for a single managed array.");
            }

            var columns = ((long)width + chunkSize - 1L) / chunkSize;
            var rows = ((long)height + chunkSize - 1L) / chunkSize;
            var chunks = columns * rows;
            if (columns > int.MaxValue || rows > int.MaxValue || chunks > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    "The world contains too many chunks for a single managed array.");
            }

            cellCount = (int)cells;
            chunkColumns = (int)columns;
            chunkRows = (int)rows;
            chunkCount = (int)chunks;
        }

        private static ulong ToSeed(int seed)
        {
            if (seed < 0)
                throw new ArgumentOutOfRangeException(nameof(seed), "A world seed cannot be negative.");

            return (ulong)seed;
        }

        private static int GetWidth(WorldStateDto dto)
        {
            ValidateDto(dto);
            return dto.Width;
        }

        private static int GetHeight(WorldStateDto dto)
        {
            ValidateDto(dto);
            return dto.Height;
        }

        private static int GetChunkSize(WorldStateDto dto)
        {
            ValidateDto(dto);
            return dto.ChunkSize;
        }

        private static ulong GetSeed(WorldStateDto dto)
        {
            ValidateDto(dto);
            return dto.Seed;
        }

        private static void ValidateDto(WorldStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
        }
    }
}
