using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.World;

namespace OpenSimCity.Simulation.Spatial
{
    /// <summary>
    /// Integer cell fields rebuilt only for explicitly dirty row-major indices.
    /// Material resolution, render state and observation never participate.
    /// </summary>
    public sealed class SpatialFieldSystem
    {
        public const int MinimumValue = 0;
        public const int MaximumValue = 1000;
        public const int BaselineLandValue = 500;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly WorldState _world;
        private readonly SpatialSourceDefinitionCatalog _definitions;
        private readonly SortedDictionary<StableId, SpatialFieldSourceState> _sources =
            new SortedDictionary<StableId, SpatialFieldSourceState>();
        private readonly SortedSet<int> _dirtyIndices = new SortedSet<int>();
        private readonly int[] _landValue;
        private readonly int[] _pollution;
        private readonly int[] _noise;
        private readonly int[] _crime;
        private readonly int[] _parkAmenity;
        private IReadOnlyList<int> _lastRefreshedIndices = Array.AsReadOnly(Array.Empty<int>());
        private IReadOnlyList<ChunkCoord> _lastRefreshedChunks = Array.AsReadOnly(Array.Empty<ChunkCoord>());

        public SpatialFieldSystem(
            WorldState world,
            SpatialSourceDefinitionCatalog definitions = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _definitions = definitions ?? SpatialSourceDefinitionCatalog.CreateDefault();
            _landValue = new int[world.CellCount];
            _pollution = new int[world.CellCount];
            _noise = new int[world.CellCount];
            _crime = new int[world.CellCount];
            _parkAmenity = new int[world.CellCount];
            ForceFullRebuild();
        }

        public WorldState World => _world;
        public SpatialSourceDefinitionCatalog Definitions => _definitions;
        public IReadOnlyList<SpatialFieldSourceState> Sources =>
            new List<SpatialFieldSourceState>(_sources.Values).AsReadOnly();
        public int SourceVersion { get; private set; }
        public int FieldVersion { get; private set; }
        public int RefreshVersion { get; private set; }
        public int DirtyCellCount => _dirtyIndices.Count;
        public bool HasDirtyCells => _dirtyIndices.Count > 0;
        public IReadOnlyList<int> LastRefreshedIndices => _lastRefreshedIndices;
        public IReadOnlyList<ChunkCoord> LastRefreshedChunks => _lastRefreshedChunks;
        public int LastRefreshCellCount => _lastRefreshedIndices.Count;
        public int LastRefreshChunkCount => _lastRefreshedChunks.Count;

        public bool TryGetSource(StableId id, out SpatialFieldSourceState source) =>
            _sources.TryGetValue(id, out source);

        public bool UpsertSource(SpatialFieldSourceState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!_world.IsInBounds(source.Cell))
                throw new ArgumentOutOfRangeException(nameof(source), "Spatial source is outside world bounds.");
            if (!_definitions.TryGet(source.DefinitionId, out var definition))
                throw new ArgumentException("Spatial source uses an unknown definition.", nameof(source));

            if (_sources.TryGetValue(source.Id, out var previous))
            {
                if (previous.DefinitionId == source.DefinitionId &&
                    previous.Cell == source.Cell &&
                    previous.IsEnabled == source.IsEnabled)
                {
                    return false;
                }
                var previousDefinition = _definitions.Get(previous.DefinitionId);
                MarkAffected(previous.Cell, previousDefinition.Radius);
            }

            _sources[source.Id] = source;
            MarkAffected(source.Cell, definition.Radius);
            SourceVersion = checked(SourceVersion + 1);
            return true;
        }

        public bool RemoveSource(StableId id)
        {
            if (!_sources.TryGetValue(id, out var source))
                return false;
            _sources.Remove(id);
            MarkAffected(source.Cell, _definitions.Get(source.DefinitionId).Radius);
            SourceVersion = checked(SourceVersion + 1);
            return true;
        }

        public SpatialFieldCellValue Get(CellCoord cell)
        {
            var index = _world.GetIndex(cell);
            return GetAtIndex(index);
        }

        public SpatialFieldCellValue GetAtIndex(int index)
        {
            if ((uint)index >= (uint)_world.CellCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new SpatialFieldCellValue(
                _landValue[index],
                _pollution[index],
                _noise[index],
                _crime[index],
                _parkAmenity[index]);
        }

        public int Get(CellCoord cell, SpatialFieldKind kind) => Get(cell).Get(kind);

        public bool RefreshDirty()
        {
            if (_dirtyIndices.Count == 0)
                return false;

            var indices = new int[_dirtyIndices.Count];
            _dirtyIndices.CopyTo(indices);
            _dirtyIndices.Clear();
            var chunks = new SortedSet<ChunkCoord>();
            var changed = false;
            for (var position = 0; position < indices.Length; position++)
            {
                var index = indices[position];
                var cell = _world.GetCoordinate(index);
                chunks.Add(_world.GetChunkCoord(cell));
                var value = Evaluate(cell);
                if (_landValue[index] == value.LandValue &&
                    _pollution[index] == value.Pollution &&
                    _noise[index] == value.Noise &&
                    _crime[index] == value.Crime &&
                    _parkAmenity[index] == value.ParkAmenity)
                {
                    continue;
                }

                _landValue[index] = value.LandValue;
                _pollution[index] = value.Pollution;
                _noise[index] = value.Noise;
                _crime[index] = value.Crime;
                _parkAmenity[index] = value.ParkAmenity;
                changed = true;
            }

            _lastRefreshedIndices = Array.AsReadOnly(indices);
            var chunkArray = new ChunkCoord[chunks.Count];
            chunks.CopyTo(chunkArray);
            _lastRefreshedChunks = Array.AsReadOnly(chunkArray);
            RefreshVersion = checked(RefreshVersion + 1);
            if (changed)
                FieldVersion = checked(FieldVersion + 1);
            return changed;
        }

        public void ForceFullRebuild()
        {
            for (var index = 0; index < _world.CellCount; index++)
                _dirtyIndices.Add(index);
            RefreshDirty();
        }

        public SpatialFieldRuntimeSnapshotDto ToDto()
        {
            var rows = new SpatialFieldSourceStateDto[_sources.Count];
            var index = 0;
            foreach (var source in _sources.Values)
            {
                rows[index++] = new SpatialFieldSourceStateDto
                {
                    Id = source.Id.Value,
                    DefinitionId = source.DefinitionId.Value,
                    CellX = source.Cell.X,
                    CellY = source.Cell.Y,
                    IsEnabled = source.IsEnabled
                };
            }
            return new SpatialFieldRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Sources = rows
            };
        }

        public void RestoreFromDto(SpatialFieldRuntimeSnapshotDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            if (dto.SchemaVersion != 1)
                throw new InvalidOperationException("Unsupported spatial-field snapshot schema version.");

            _sources.Clear();
            var rows = dto.Sources ?? Array.Empty<SpatialFieldSourceStateDto>();
            ulong previousId = 0;
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index] ?? throw new ArgumentException("Spatial source rows cannot contain null.", nameof(dto));
                if (row.Id == 0 || row.Id <= previousId)
                    throw new ArgumentException("Spatial sources must be unique and stable-ID ordered.", nameof(dto));
                var definitionId = new StableId(row.DefinitionId);
                if (!_definitions.TryGet(definitionId, out _))
                    throw new ArgumentException("Spatial source snapshot uses an unknown definition.", nameof(dto));
                var cell = new CellCoord(row.CellX, row.CellY);
                if (!_world.IsInBounds(cell))
                    throw new ArgumentException("Spatial source snapshot is outside world bounds.", nameof(dto));
                var source = new SpatialFieldSourceState(
                    new StableId(row.Id),
                    definitionId,
                    cell,
                    row.IsEnabled);
                _sources.Add(source.Id, source);
                previousId = row.Id;
            }
            SourceVersion = 1;
            ForceFullRebuild();
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, _world.Width);
            AddInt(ref hash, _world.Height);
            AddInt(ref hash, _sources.Count);
            foreach (var source in _sources.Values)
            {
                AddUlong(ref hash, source.Id.Value);
                AddUlong(ref hash, source.DefinitionId.Value);
                AddInt(ref hash, source.Cell.X);
                AddInt(ref hash, source.Cell.Y);
                AddByte(ref hash, source.IsEnabled ? (byte)1 : (byte)0);
            }
            for (var index = 0; index < _world.CellCount; index++)
            {
                AddInt(ref hash, _landValue[index]);
                AddInt(ref hash, _pollution[index]);
                AddInt(ref hash, _noise[index]);
                AddInt(ref hash, _crime[index]);
                AddInt(ref hash, _parkAmenity[index]);
            }
            return hash;
        }

        private SpatialFieldCellValue Evaluate(CellCoord cell)
        {
            long directLandValue = 0;
            long pollution = 0;
            long noise = 0;
            long crime = 0;
            long park = 0;
            foreach (var source in _sources.Values)
            {
                if (!source.IsEnabled)
                    continue;
                var definition = _definitions.Get(source.DefinitionId);
                var distance = cell.ManhattanDistanceTo(source.Cell);
                if (distance > definition.Radius)
                    continue;
                directLandValue += definition.Contribution(definition.LandValueDelta, distance);
                pollution += definition.Contribution(definition.Pollution, distance);
                noise += definition.Contribution(definition.Noise, distance);
                crime += definition.Contribution(definition.Crime, distance);
                park += definition.Contribution(definition.ParkAmenity, distance);
            }

            var pollutionValue = Clamp(pollution);
            var noiseValue = Clamp(noise);
            var crimeValue = Clamp(crime);
            var parkValue = Clamp(park);
            var landValue = Clamp(
                BaselineLandValue +
                directLandValue +
                parkValue / 2L -
                pollutionValue * 2L / 5L -
                noiseValue / 4L -
                crimeValue / 3L);
            return new SpatialFieldCellValue(
                landValue,
                pollutionValue,
                noiseValue,
                crimeValue,
                parkValue);
        }

        private void MarkAffected(CellCoord center, int radius)
        {
            var minX = Math.Max(0, center.X - radius);
            var maxX = Math.Min(_world.Width - 1, center.X + radius);
            var minY = Math.Max(0, center.Y - radius);
            var maxY = Math.Min(_world.Height - 1, center.Y + radius);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                    _dirtyIndices.Add(_world.GetIndex(new CellCoord(x, y)));
            }
        }

        private static int Clamp(long value)
        {
            if (value <= MinimumValue) return MinimumValue;
            if (value >= MaximumValue) return MaximumValue;
            return (int)value;
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime64;
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }
    }
}
