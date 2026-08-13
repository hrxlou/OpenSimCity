using System;
using System.Collections.Generic;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Spatial;

namespace OpenSimCity.Application.Queries
{
    public enum SpatialFieldViewKind : byte
    {
        LandValue,
        Pollution,
        Noise,
        Crime,
        ParkAmenity
    }

    [Serializable]
    public readonly struct SpatialFieldCellReadModel
    {
        public SpatialFieldCellReadModel(CellCoord cell, SpatialFieldCellValue value)
        {
            Cell = cell;
            LandValue = value.LandValue;
            Pollution = value.Pollution;
            Noise = value.Noise;
            Crime = value.Crime;
            ParkAmenity = value.ParkAmenity;
        }
        public CellCoord Cell { get; }
        public int LandValue { get; }
        public int Pollution { get; }
        public int Noise { get; }
        public int Crime { get; }
        public int ParkAmenity { get; }
        public int Get(SpatialFieldViewKind kind)
        {
            switch (kind)
            {
                case SpatialFieldViewKind.LandValue: return LandValue;
                case SpatialFieldViewKind.Pollution: return Pollution;
                case SpatialFieldViewKind.Noise: return Noise;
                case SpatialFieldViewKind.Crime: return Crime;
                case SpatialFieldViewKind.ParkAmenity: return ParkAmenity;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    [Serializable]
    public readonly struct SpatialFieldSourceReadModel
    {
        public SpatialFieldSourceReadModel(SpatialFieldSourceState source)
        {
            Id = source.Id;
            DefinitionId = source.DefinitionId;
            Cell = source.Cell;
            IsEnabled = source.IsEnabled;
            IsDerived = SpatialDerivedSourceIds.IsDerived(source.Id);
        }
        public StableId Id { get; }
        public StableId DefinitionId { get; }
        public CellCoord Cell { get; }
        public bool IsEnabled { get; }
        public bool IsDerived { get; }
    }

    [Serializable]
    public sealed class SpatialFieldReadModelSnapshot
    {
        public int Version;
        public int Width;
        public int Height;
        public int FieldVersion;
        public int SourceVersion;
        public int LastRefreshCellCount;
        public int LastRefreshChunkCount;
        public ulong Checksum;
        public IReadOnlyList<SpatialFieldCellReadModel> Cells =
            Array.AsReadOnly(Array.Empty<SpatialFieldCellReadModel>());
        public IReadOnlyList<SpatialFieldSourceReadModel> Sources =
            Array.AsReadOnly(Array.Empty<SpatialFieldSourceReadModel>());
    }

    public sealed class SpatialFieldReadModel
    {
        private readonly SpatialFieldRuntimeModule _module;
        private int _fieldVersion = -1;
        private int _sourceVersion = -1;
        private int _refreshVersion = -1;

        public SpatialFieldReadModel(SpatialFieldRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Snapshot = new SpatialFieldReadModelSnapshot();
        }

        public SpatialFieldReadModelSnapshot Snapshot { get; private set; }

        public bool RefreshIfNeeded()
        {
            var fields = _module.Fields;
            if (_fieldVersion == fields.FieldVersion &&
                _sourceVersion == fields.SourceVersion &&
                _refreshVersion == fields.RefreshVersion)
            {
                return false;
            }

            var cells = new SpatialFieldCellReadModel[fields.World.CellCount];
            for (var index = 0; index < cells.Length; index++)
                cells[index] = new SpatialFieldCellReadModel(
                    fields.World.GetCoordinate(index),
                    fields.GetAtIndex(index));

            var sourceState = fields.Sources;
            var sources = new SpatialFieldSourceReadModel[sourceState.Count];
            for (var index = 0; index < sources.Length; index++)
                sources[index] = new SpatialFieldSourceReadModel(sourceState[index]);

            Snapshot = new SpatialFieldReadModelSnapshot
            {
                Version = Snapshot.Version + 1,
                Width = fields.World.Width,
                Height = fields.World.Height,
                FieldVersion = fields.FieldVersion,
                SourceVersion = fields.SourceVersion,
                LastRefreshCellCount = fields.LastRefreshCellCount,
                LastRefreshChunkCount = fields.LastRefreshChunkCount,
                Checksum = fields.ComputeChecksum(),
                Cells = Array.AsReadOnly(cells),
                Sources = Array.AsReadOnly(sources)
            };
            _fieldVersion = fields.FieldVersion;
            _sourceVersion = fields.SourceVersion;
            _refreshVersion = fields.RefreshVersion;
            return true;
        }

        public SpatialFieldCellReadModel Get(CellCoord cell)
        {
            var snapshot = Snapshot;
            if ((uint)cell.X >= (uint)snapshot.Width || (uint)cell.Y >= (uint)snapshot.Height)
                throw new ArgumentOutOfRangeException(nameof(cell));
            return snapshot.Cells[cell.Y * snapshot.Width + cell.X];
        }
    }
}
