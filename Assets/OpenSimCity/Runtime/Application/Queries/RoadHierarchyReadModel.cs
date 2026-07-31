using System;
using System.Collections.Generic;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Application.Queries
{
    [Serializable]
    public readonly struct RoadHierarchyCellView
    {
        public RoadHierarchyCellView(
            CellCoord cell,
            StableId definitionId,
            RoadTravelDirection travelDirection,
            int buildCost,
            int monthlyMaintenanceCost,
            int capacityPerMonth,
            int freeFlowSpeedCellsPerHour,
            int noiseEmission,
            string presentationKey)
        {
            Cell = cell;
            DefinitionId = definitionId;
            TravelDirection = travelDirection;
            BuildCost = buildCost;
            MonthlyMaintenanceCost = monthlyMaintenanceCost;
            CapacityPerMonth = capacityPerMonth;
            FreeFlowSpeedCellsPerHour = freeFlowSpeedCellsPerHour;
            NoiseEmission = noiseEmission;
            PresentationKey = presentationKey ?? string.Empty;
        }

        public CellCoord Cell { get; }
        public StableId DefinitionId { get; }
        public RoadTravelDirection TravelDirection { get; }
        public int BuildCost { get; }
        public int MonthlyMaintenanceCost { get; }
        public int CapacityPerMonth { get; }
        public int FreeFlowSpeedCellsPerHour { get; }
        public int NoiseEmission { get; }
        public string PresentationKey { get; }
        public bool IsOneWay => TravelDirection != RoadTravelDirection.Bidirectional;
    }

    /// <summary>
    /// Row-major application projection for road tier and direction diagnostics.
    /// Definition values are copied from the immutable catalog and never recomputed
    /// by UI or Presentation.
    /// </summary>
    public sealed class RoadHierarchyReadModel
    {
        private readonly RoadNetworkState _roads;
        private readonly RoadDefinitionCatalog _definitions;
        private IReadOnlyList<RoadHierarchyCellView> _cells =
            Array.AsReadOnly(Array.Empty<RoadHierarchyCellView>());
        private int _sourceVersion = -1;

        public RoadHierarchyReadModel(
            RoadNetworkState roads,
            RoadDefinitionCatalog definitions)
        {
            _roads = roads ?? throw new ArgumentNullException(nameof(roads));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            RefreshIfNeeded();
        }

        public int Version { get; private set; }

        public IReadOnlyList<RoadHierarchyCellView> Cells
        {
            get
            {
                RefreshIfNeeded();
                return _cells;
            }
        }

        public bool RefreshIfNeeded()
        {
            if (_sourceVersion == _roads.Version)
                return false;

            var rows = new List<RoadHierarchyCellView>();
            var cells = _roads.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                var state = cells[index];
                if (!state.IsOccupied)
                    continue;
                var definition = _definitions.Get(state.DefinitionId);
                rows.Add(new RoadHierarchyCellView(
                    state.Coordinate,
                    state.DefinitionId,
                    state.TravelDirection,
                    definition.BuildCost,
                    definition.MonthlyMaintenanceCost,
                    definition.CapacityPerMonth,
                    definition.FreeFlowSpeedCellsPerHour,
                    definition.NoiseEmission,
                    definition.PresentationKey));
            }

            _cells = rows.AsReadOnly();
            _sourceVersion = _roads.Version;
            Version = checked(Version + 1);
            return true;
        }

        public bool TryGet(CellCoord cell, out RoadHierarchyCellView result)
        {
            RefreshIfNeeded();
            if (!_roads.IsInBounds(cell) || !_roads.IsOccupied(cell))
            {
                result = default;
                return false;
            }

            var state = _roads.GetCell(cell);
            var definition = _definitions.Get(state.DefinitionId);
            result = new RoadHierarchyCellView(
                state.Coordinate,
                state.DefinitionId,
                state.TravelDirection,
                definition.BuildCost,
                definition.MonthlyMaintenanceCost,
                definition.CapacityPerMonth,
                definition.FreeFlowSpeedCellsPerHour,
                definition.NoiseEmission,
                definition.PresentationKey);
            return true;
        }
    }
}
