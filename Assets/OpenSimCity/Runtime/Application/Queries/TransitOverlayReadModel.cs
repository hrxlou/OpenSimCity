using System;
using System.Collections.Generic;
using OpenSimCity.Application.Transit;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Queries
{
    public enum TransitOverlayPattern : byte
    {
        Solid,
        Dashed,
        DirectionArrow,
        Warning
    }

    [Serializable]
    public readonly struct TransitOverlayStopData
    {
        public TransitOverlayStopData(StableId id, CellCoord roadCell, string label)
        {
            Id = id;
            RoadCell = roadCell;
            Label = label ?? string.Empty;
        }
        public StableId Id { get; }
        public CellCoord RoadCell { get; }
        public string Label { get; }
    }

    [Serializable]
    public sealed class TransitOverlayLineData
    {
        public StableId LineId;
        public string Name = string.Empty;
        public IReadOnlyList<CellCoord> Cells = Array.AsReadOnly(Array.Empty<CellCoord>());
        public int BusTrips;
        public int Capacity;
        public int UtilizationPermille;
        public int OperatingExpense;
        public TransitOverlayPattern Pattern;
    }

    [Serializable]
    public readonly struct TransitOverlayIssueData
    {
        public TransitOverlayIssueData(
            StableId originId,
            StableId destinationId,
            CellCoord originRoadCell,
            CellCoord destinationRoadCell,
            int amount,
            TransitModeChoiceReasonView reason)
        {
            OriginId = originId;
            DestinationId = destinationId;
            OriginRoadCell = originRoadCell;
            DestinationRoadCell = destinationRoadCell;
            Amount = amount;
            Reason = reason;
        }
        public StableId OriginId { get; }
        public StableId DestinationId { get; }
        public CellCoord OriginRoadCell { get; }
        public CellCoord DestinationRoadCell { get; }
        public int Amount { get; }
        public TransitModeChoiceReasonView Reason { get; }
    }

    /// <summary>
    /// Route geometry, stop markers and explicit unmet reasons for transit overlays.
    /// It rebuilds from application/simulation queries and never edits transit state.
    /// </summary>
    public sealed class TransitOverlayReadModel
    {
        private readonly TransitRuntimeModule _module;
        private readonly DeterministicRoutePlanner _planner;
        private int _networkVersion = -1;
        private int _modeVersion = -1;
        private int _roadVersion = -1;
        private IReadOnlyList<TransitOverlayStopData> _stops =
            Array.AsReadOnly(Array.Empty<TransitOverlayStopData>());
        private IReadOnlyList<TransitOverlayLineData> _lines =
            Array.AsReadOnly(Array.Empty<TransitOverlayLineData>());
        private IReadOnlyList<TransitOverlayIssueData> _issues =
            Array.AsReadOnly(Array.Empty<TransitOverlayIssueData>());

        public TransitOverlayReadModel(TransitRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _planner = new DeterministicRoutePlanner(
                module.Runtime.Roads,
                module.Runtime.RoadGraph,
                module.Runtime.RoadDefinitions);
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public IReadOnlyList<TransitOverlayStopData> Stops { get { RefreshIfNeeded(); return _stops; } }
        public IReadOnlyList<TransitOverlayLineData> Lines { get { RefreshIfNeeded(); return _lines; } }
        public IReadOnlyList<TransitOverlayIssueData> Issues { get { RefreshIfNeeded(); return _issues; } }

        public bool RefreshIfNeeded()
        {
            var networkVersion = _module.Network.Version;
            var modeVersion = _module.ModeChoice.State.Version;
            var roadVersion = _module.Runtime.Roads.Version;
            if (_networkVersion == networkVersion && _modeVersion == modeVersion && _roadVersion == roadVersion)
                return false;

            var stopSource = _module.Network.Stops;
            var stops = new TransitOverlayStopData[stopSource.Count];
            for (var index = 0; index < stops.Length; index++)
                stops[index] = new TransitOverlayStopData(
                    stopSource[index].Id.Value,
                    stopSource[index].RoadCell,
                    stopSource[index].Name);

            var busByLine = new Dictionary<TransitLineId, int>();
            var modeRows = _module.ModeChoice.State.Rows;
            for (var index = 0; index < modeRows.Count; index++)
            {
                if (!modeRows[index].HasLine || modeRows[index].BusTrips <= 0)
                    continue;
                busByLine.TryGetValue(modeRows[index].LineId, out var current);
                busByLine[modeRows[index].LineId] = checked(current + modeRows[index].BusTrips);
            }

            var lineSource = _module.Network.Lines;
            var lines = new List<TransitOverlayLineData>();
            for (var index = 0; index < lineSource.Count; index++)
            {
                var line = lineSource[index];
                if (!TryBuildPath(line, out var cells))
                    continue;
                busByLine.TryGetValue(line.Id, out var busTrips);
                var capacity = line.IsEnabled
                    ? checked(line.ServiceUnits * _module.Parameters.CapacityPerServiceUnit *
                              _module.ModeChoice.State.EffectiveBudgetPermille / 1000)
                    : 0;
                var operating = checked(line.ServiceUnits * _module.Parameters.OperatingCostPerServiceUnit *
                                        _module.Network.ActiveBudgetPermille / 1000);
                lines.Add(new TransitOverlayLineData
                {
                    LineId = line.Id.Value,
                    Name = line.Name,
                    Cells = Array.AsReadOnly(cells),
                    BusTrips = busTrips,
                    Capacity = capacity,
                    UtilizationPermille = capacity <= 0 ? 0 : checked(busTrips * 1000 / capacity),
                    OperatingExpense = operating,
                    Pattern = line.IsEnabled ? TransitOverlayPattern.DirectionArrow : TransitOverlayPattern.Dashed
                });
            }

            var demandByKey = new Dictionary<TransitOdKey, TripDemandRow>();
            var demandRows = _module.Runtime.TripDemandSystem.State.Rows;
            for (var index = 0; index < demandRows.Count; index++)
            {
                var demand = demandRows[index];
                if (demand.Kind == TripDemandKind.Commute && demand.Status == TripDemandStatus.Matched)
                    demandByKey[new TransitOdKey(demand)] = demand;
            }
            var issues = new List<TransitOverlayIssueData>();
            for (var index = 0; index < modeRows.Count; index++)
            {
                var row = modeRows[index];
                if (row.Reason == TransitModeChoiceReason.None || !demandByKey.TryGetValue(row.Key, out var demand))
                    continue;
                issues.Add(new TransitOverlayIssueData(
                    demand.Origin.Id,
                    demand.Destination.Id,
                    demand.Origin.RoadCell,
                    demand.Destination.RoadCell,
                    row.RequestedTrips,
                    TransitReasonProjection.ToView(row.Reason)));
            }

            _stops = Array.AsReadOnly(stops);
            _lines = lines.AsReadOnly();
            _issues = issues.AsReadOnly();
            _networkVersion = networkVersion;
            _modeVersion = modeVersion;
            _roadVersion = roadVersion;
            Version = checked(Version + 1);
            return true;
        }

        private bool TryBuildPath(TransitLineState line, out CellCoord[] cells)
        {
            var result = new List<CellCoord>();
            for (var index = 0; index < line.StopIds.Count - 1; index++)
            {
                if (!_module.Network.TryGetStop(line.StopIds[index], out var from) ||
                    !_module.Network.TryGetStop(line.StopIds[index + 1], out var to))
                {
                    cells = Array.Empty<CellCoord>();
                    return false;
                }
                var routeSet = _planner.GetRoutes(from.RoadCell, to.RoadCell);
                if (!routeSet.IsReachable || routeSet.Routes.Count == 0)
                {
                    cells = Array.Empty<CellCoord>();
                    return false;
                }
                var route = routeSet.Routes[0].Cells;
                for (var cellIndex = 0; cellIndex < route.Count; cellIndex++)
                {
                    if (result.Count > 0 && cellIndex == 0 && result[result.Count - 1] == route[cellIndex])
                        continue;
                    result.Add(route[cellIndex]);
                }
            }
            cells = result.ToArray();
            return cells.Length > 0;
        }
    }
}
