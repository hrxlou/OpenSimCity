using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Queries
{
    [Flags]
    public enum RoadConnectionReadMask : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public enum RoadAccessibilityBand : byte
    {
        Unreachable,
        Gate,
        Near,
        Medium,
        Far
    }

    public enum RoadAccessibilityUnavailableReason : byte
    {
        None,
        NoRoad,
        NoEnabledExternalRoadGate,
        DisconnectedFromExternalRoadGate
    }

    /// <summary>
    /// Application-owned DTO for overlays and UI. Presentation consumers do
    /// not need a direct reference to the Simulation assembly and must not
    /// recalculate these values from rendered roads.
    /// </summary>
    public readonly struct RoadAccessibilityCellReadModel : IEquatable<RoadAccessibilityCellReadModel>
    {
        internal RoadAccessibilityCellReadModel(RoadConnectivityResult result)
        {
            Cell = result.Cell;
            ConnectionMask = (RoadConnectionReadMask)result.ConnectionMask;
            ComponentId = result.ComponentId;
            HasRoad = result.HasRoad;
            IsReachable = result.IsReachable;
            NetworkDistanceToExternalGate = result.NetworkDistanceToExternalGate;
            NearestExternalGateId = result.NearestExternalGateId;
            DistanceBand = (RoadAccessibilityBand)result.DistanceBand;
            UnavailableReason = (RoadAccessibilityUnavailableReason)result.UnreachableReason;
        }

        public CellCoord Cell { get; }

        public RoadConnectionReadMask ConnectionMask { get; }

        /// <summary>
        /// Rebuildable graph-local label. Never use this as a persistent ID.
        /// </summary>
        public int ComponentId { get; }

        public bool HasRoad { get; }

        public bool IsReachable { get; }

        public int NetworkDistanceToExternalGate { get; }

        public StableId NearestExternalGateId { get; }

        public RoadAccessibilityBand DistanceBand { get; }

        public RoadAccessibilityUnavailableReason UnavailableReason { get; }

        public bool Equals(RoadAccessibilityCellReadModel other)
        {
            return Cell == other.Cell &&
                   ConnectionMask == other.ConnectionMask &&
                   ComponentId == other.ComponentId &&
                   HasRoad == other.HasRoad &&
                   IsReachable == other.IsReachable &&
                   NetworkDistanceToExternalGate == other.NetworkDistanceToExternalGate &&
                   NearestExternalGateId == other.NearestExternalGateId &&
                   DistanceBand == other.DistanceBand &&
                   UnavailableReason == other.UnavailableReason;
        }

        public override bool Equals(object obj)
        {
            return obj is RoadAccessibilityCellReadModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Cell.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)ConnectionMask;
                hashCode = (hashCode * 397) ^ ComponentId;
                hashCode = (hashCode * 397) ^ HasRoad.GetHashCode();
                hashCode = (hashCode * 397) ^ IsReachable.GetHashCode();
                hashCode = (hashCode * 397) ^ NetworkDistanceToExternalGate;
                hashCode = (hashCode * 397) ^ NearestExternalGateId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)DistanceBand;
                return (hashCode * 397) ^ (int)UnavailableReason;
            }
        }

        public static bool operator ==(
            RoadAccessibilityCellReadModel left,
            RoadAccessibilityCellReadModel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            RoadAccessibilityCellReadModel left,
            RoadAccessibilityCellReadModel right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Versioned bulk read model shared by the road overlay and cell queries.
    /// It allocates a new snapshot only after the simulation graph changes.
    /// </summary>
    public sealed class RoadAccessibilityReadModel
    {
        public const int UnreachableDistance = RoadConnectivityGraph.UnreachableDistance;
        public const int NearDistanceMaximum = RoadConnectivityGraph.NearDistanceMaximum;
        public const int MediumDistanceMaximum = RoadConnectivityGraph.MediumDistanceMaximum;

        private readonly RoadConnectivityGraph _graph;
        private RoadAccessibilityCellReadModel[] _cells = Array.Empty<RoadAccessibilityCellReadModel>();
        private IReadOnlyList<RoadAccessibilityCellReadModel> _readOnlyCells =
            Array.AsReadOnly(Array.Empty<RoadAccessibilityCellReadModel>());
        private IReadOnlyList<CellCoord> _dirtyCells = Array.AsReadOnly(Array.Empty<CellCoord>());
        private int _graphVersion = -1;

        public RoadAccessibilityReadModel(RoadConnectivityGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            RefreshIfNeeded();
        }

        public int Width => _graph.Width;

        public int Height => _graph.Height;

        public int GraphVersion
        {
            get
            {
                RefreshIfNeeded();
                return _graphVersion;
            }
        }

        public IReadOnlyList<RoadAccessibilityCellReadModel> Cells
        {
            get
            {
                RefreshIfNeeded();
                return _readOnlyCells;
            }
        }

        public IReadOnlyList<CellCoord> DirtyCells
        {
            get
            {
                RefreshIfNeeded();
                return _dirtyCells;
            }
        }

        public bool HasDirtyRegion
        {
            get
            {
                RefreshIfNeeded();
                return _graph.DirtyRegion.HasValue;
            }
        }

        public CellCoord DirtyMinimum
        {
            get
            {
                RefreshIfNeeded();
                return _graph.DirtyRegion.Minimum;
            }
        }

        public CellCoord DirtyMaximum
        {
            get
            {
                RefreshIfNeeded();
                return _graph.DirtyRegion.Maximum;
            }
        }

        public bool RefreshIfNeeded()
        {
            var currentVersion = _graph.GraphVersion;
            if (_graphVersion == currentVersion)
                return false;

            var graphCells = _graph.Cells;
            var cells = new RoadAccessibilityCellReadModel[graphCells.Count];
            for (var index = 0; index < cells.Length; index++)
                cells[index] = new RoadAccessibilityCellReadModel(graphCells[index]);

            var graphDirtyCells = _graph.DirtyCells;
            var dirtyCells = new CellCoord[graphDirtyCells.Count];
            for (var index = 0; index < dirtyCells.Length; index++)
                dirtyCells[index] = graphDirtyCells[index];

            _cells = cells;
            _readOnlyCells = Array.AsReadOnly(_cells);
            _dirtyCells = Array.AsReadOnly(dirtyCells);
            _graphVersion = currentVersion;
            return true;
        }

        public bool TryGet(CellCoord cell, out RoadAccessibilityCellReadModel result)
        {
            RefreshIfNeeded();
            if ((uint)cell.X >= (uint)Width || (uint)cell.Y >= (uint)Height)
            {
                result = default;
                return false;
            }

            result = _cells[cell.Y * Width + cell.X];
            return true;
        }

        public RoadAccessibilityCellReadModel Get(CellCoord cell)
        {
            if (!TryGet(cell, out var result))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cell),
                    cell,
                    $"Cell coordinate {cell} is outside {Width}x{Height} accessibility bounds.");
            }

            return result;
        }
    }
}
