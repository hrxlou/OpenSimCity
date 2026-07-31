using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Immutable, presentation-safe comparison for one no-demolition road upgrade
    /// preview. It combines authoritative road definitions with a scratch directed
    /// graph and the current aggregate traffic read state. Scratch planners never
    /// mutate the live road network or its route cache.
    /// </summary>
    [Serializable]
    public sealed class RoadUpgradePreviewSnapshot
    {
        internal RoadUpgradePreviewSnapshot(
            bool isValid,
            StableId targetDefinitionId,
            RoadDirectionMode directionMode,
            RoadTravelDirection resolvedDirection,
            int cellCount,
            int upgradeCost,
            int currentBuildCost,
            int targetBuildCost,
            int currentMonthlyMaintenance,
            int targetMonthlyMaintenance,
            int currentCapacity,
            int targetCapacity,
            int currentMinimumSpeed,
            int targetMinimumSpeed,
            int currentNoise,
            int targetNoise,
            int currentMaximumVolumeCapacityPermille,
            int predictedMaximumVolumeCapacityPermille,
            int currentCongestedEdgeCount,
            int predictedCongestedEdgeCount,
            CellCoord predictedBottleneckA,
            CellCoord predictedBottleneckB,
            bool hasPredictedBottleneck,
            int reachableTripAmountBefore,
            int reachableTripAmountAfter,
            int unroutedTripAmountBefore,
            int unroutedTripAmountAfter,
            int averageFreeFlowSecondsBefore,
            int averageFreeFlowSecondsAfter,
            TrafficRouteFailureReason primaryNewFailureReason)
        {
            IsValid = isValid;
            TargetDefinitionId = targetDefinitionId;
            DirectionMode = directionMode;
            ResolvedDirection = resolvedDirection;
            CellCount = cellCount;
            UpgradeCost = upgradeCost;
            CurrentBuildCost = currentBuildCost;
            TargetBuildCost = targetBuildCost;
            CurrentMonthlyMaintenance = currentMonthlyMaintenance;
            TargetMonthlyMaintenance = targetMonthlyMaintenance;
            CurrentCapacity = currentCapacity;
            TargetCapacity = targetCapacity;
            CurrentMinimumSpeed = currentMinimumSpeed;
            TargetMinimumSpeed = targetMinimumSpeed;
            CurrentNoise = currentNoise;
            TargetNoise = targetNoise;
            CurrentMaximumVolumeCapacityPermille = currentMaximumVolumeCapacityPermille;
            PredictedMaximumVolumeCapacityPermille = predictedMaximumVolumeCapacityPermille;
            CurrentCongestedEdgeCount = currentCongestedEdgeCount;
            PredictedCongestedEdgeCount = predictedCongestedEdgeCount;
            PredictedBottleneckA = predictedBottleneckA;
            PredictedBottleneckB = predictedBottleneckB;
            HasPredictedBottleneck = hasPredictedBottleneck;
            ReachableTripAmountBefore = reachableTripAmountBefore;
            ReachableTripAmountAfter = reachableTripAmountAfter;
            UnroutedTripAmountBefore = unroutedTripAmountBefore;
            UnroutedTripAmountAfter = unroutedTripAmountAfter;
            AverageFreeFlowSecondsBefore = averageFreeFlowSecondsBefore;
            AverageFreeFlowSecondsAfter = averageFreeFlowSecondsAfter;
            PrimaryNewFailureReason = primaryNewFailureReason;
        }

        public bool IsValid { get; }
        public StableId TargetDefinitionId { get; }
        public RoadDirectionMode DirectionMode { get; }
        public RoadTravelDirection ResolvedDirection { get; }
        public int CellCount { get; }
        public int UpgradeCost { get; }
        public int CurrentBuildCost { get; }
        public int TargetBuildCost { get; }
        public int CurrentMonthlyMaintenance { get; }
        public int TargetMonthlyMaintenance { get; }
        public int CurrentCapacity { get; }
        public int TargetCapacity { get; }
        public int CurrentMinimumSpeed { get; }
        public int TargetMinimumSpeed { get; }
        public int CurrentNoise { get; }
        public int TargetNoise { get; }
        public int CurrentMaximumVolumeCapacityPermille { get; }
        public int PredictedMaximumVolumeCapacityPermille { get; }
        public int CurrentCongestedEdgeCount { get; }
        public int PredictedCongestedEdgeCount { get; }
        public CellCoord PredictedBottleneckA { get; }
        public CellCoord PredictedBottleneckB { get; }
        public bool HasPredictedBottleneck { get; }
        public int ReachableTripAmountBefore { get; }
        public int ReachableTripAmountAfter { get; }
        public int UnroutedTripAmountBefore { get; }
        public int UnroutedTripAmountAfter { get; }
        public int AverageFreeFlowSecondsBefore { get; }
        public int AverageFreeFlowSecondsAfter { get; }
        public TrafficRouteFailureReason PrimaryNewFailureReason { get; }
        public int ReachableTripAmountDelta => ReachableTripAmountAfter - ReachableTripAmountBefore;
        public int UnroutedTripAmountDelta => UnroutedTripAmountAfter - UnroutedTripAmountBefore;
        public int AverageFreeFlowSecondsDelta => AverageFreeFlowSecondsAfter - AverageFreeFlowSecondsBefore;
        public int PredictedMaximumVolumeCapacityDelta =>
            PredictedMaximumVolumeCapacityPermille - CurrentMaximumVolumeCapacityPermille;
    }

    public sealed class RoadUpgradePreviewReadModel
    {
        private readonly CityRuntime _runtime;

        public RoadUpgradePreviewReadModel(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public RoadUpgradePreviewSnapshot Build(
            UpgradeRoadCommand command,
            RoadCommandResult validation)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (validation == null)
                throw new ArgumentNullException(nameof(validation));

            var target = _runtime.RoadDefinitions.Get(command.TargetRoadDefinitionId);
            var resolvedDirection = ResolveDirection(command);
            var currentBuildCost = 0;
            var currentMaintenance = 0;
            var currentCapacity = 0;
            var currentSpeed = int.MaxValue;
            var currentNoise = 0;
            for (var index = 0; index < command.Cells.Count; index++)
            {
                var definitionId = _runtime.Roads.GetRoadDefinitionId(command.Cells[index]);
                if (!definitionId.IsValid)
                    continue;
                var current = _runtime.RoadDefinitions.Get(definitionId);
                currentBuildCost = checked(currentBuildCost + current.BuildCost);
                currentMaintenance = checked(currentMaintenance + current.MonthlyMaintenanceCost);
                currentCapacity = checked(currentCapacity + current.CapacityPerMonth);
                currentSpeed = Math.Min(currentSpeed, current.FreeFlowSpeedCellsPerHour);
                currentNoise = checked(currentNoise + current.NoiseEmission);
            }

            if (currentSpeed == int.MaxValue)
                currentSpeed = 0;

            var targetBuildCost = checked(target.BuildCost * command.Cells.Count);
            var targetMaintenance = checked(target.MonthlyMaintenanceCost * command.Cells.Count);
            var targetCapacity = checked(target.CapacityPerMonth * command.Cells.Count);
            var targetNoise = checked(target.NoiseEmission * command.Cells.Count);

            if (!validation.IsValid)
            {
                return new RoadUpgradePreviewSnapshot(
                    false,
                    command.TargetRoadDefinitionId,
                    command.DirectionMode,
                    resolvedDirection,
                    command.Cells.Count,
                    validation.Cost,
                    currentBuildCost,
                    targetBuildCost,
                    currentMaintenance,
                    targetMaintenance,
                    currentCapacity,
                    targetCapacity,
                    currentSpeed,
                    target.FreeFlowSpeedCellsPerHour,
                    currentNoise,
                    targetNoise,
                    0,
                    0,
                    0,
                    0,
                    CellCoord.Zero,
                    CellCoord.Zero,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    TrafficRouteFailureReason.None);
            }

            var beforeRoads = CloneRoads(_runtime.Roads);
            var afterRoads = CloneRoads(_runtime.Roads);
            var replacementDirections = BuildDirections(command);
            if (!afterRoads.TryApplyReplacement(
                    command.Cells,
                    command.TargetRoadDefinitionId,
                    replacementDirections))
            {
                throw new InvalidOperationException("Validated road upgrade could not be applied to scratch state.");
            }

            var beforePlanner = CreatePlanner(beforeRoads);
            var afterPlanner = CreatePlanner(afterRoads);
            var congestion = CompareCongestion(command.Cells, afterPlanner);
            var od = CompareOd(beforePlanner, afterPlanner);

            return new RoadUpgradePreviewSnapshot(
                true,
                command.TargetRoadDefinitionId,
                command.DirectionMode,
                resolvedDirection,
                command.Cells.Count,
                validation.Cost,
                currentBuildCost,
                targetBuildCost,
                currentMaintenance,
                targetMaintenance,
                currentCapacity,
                targetCapacity,
                currentSpeed,
                target.FreeFlowSpeedCellsPerHour,
                currentNoise,
                targetNoise,
                congestion.CurrentMaximumPermille,
                congestion.PredictedMaximumPermille,
                congestion.CurrentCongestedCount,
                congestion.PredictedCongestedCount,
                congestion.BottleneckA,
                congestion.BottleneckB,
                congestion.HasBottleneck,
                od.ReachableBefore,
                od.ReachableAfter,
                od.UnroutedBefore,
                od.UnroutedAfter,
                od.AverageSecondsBefore,
                od.AverageSecondsAfter,
                od.PrimaryNewFailureReason);
        }

        private DeterministicRoutePlanner CreatePlanner(RoadNetworkState roads)
        {
            var graph = new RoadConnectivityGraph(roads, _runtime.ExternalConnections);
            return new DeterministicRoutePlanner(roads, graph, _runtime.RoadDefinitions);
        }

        private CongestionComparison CompareCongestion(
            IReadOnlyList<CellCoord> selectedCells,
            DeterministicRoutePlanner afterPlanner)
        {
            var selected = new HashSet<CellCoord>(selectedCells);
            var visualization = new TrafficVisualizationReadModel(_runtime.TrafficRoutingModel);
            var edges = visualization.Edges;
            var currentMaximum = 0;
            var predictedMaximum = 0;
            var currentCongested = 0;
            var predictedCongested = 0;
            var bottleneckA = CellCoord.Zero;
            var bottleneckB = CellCoord.Zero;
            var hasBottleneck = false;

            for (var index = 0; index < edges.Count; index++)
            {
                var row = edges[index];
                if (!selected.Contains(row.Edge.A) && !selected.Contains(row.Edge.B))
                    continue;

                currentMaximum = Math.Max(currentMaximum, row.VolumeCapacityPermille);
                if (row.IsCongested)
                    currentCongested++;

                var roadEdge = new RoadEdgeKey(row.Edge.A, row.Edge.B);
                var capacity = afterPlanner.EdgeCapacity(roadEdge);
                var predicted = checked((int)((long)row.Volume * 1000 / capacity));
                if (predicted > 1000)
                    predictedCongested++;
                if (!hasBottleneck ||
                    predicted > predictedMaximum ||
                    (predicted == predictedMaximum && CompareEdge(
                        row.Edge.A,
                        row.Edge.B,
                        bottleneckA,
                        bottleneckB) < 0))
                {
                    predictedMaximum = predicted;
                    bottleneckA = row.Edge.A;
                    bottleneckB = row.Edge.B;
                    hasBottleneck = true;
                }
            }

            return new CongestionComparison(
                currentMaximum,
                predictedMaximum,
                currentCongested,
                predictedCongested,
                bottleneckA,
                bottleneckB,
                hasBottleneck);
        }

        private OdComparison CompareOd(
            DeterministicRoutePlanner beforePlanner,
            DeterministicRoutePlanner afterPlanner)
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth)
                return default;

            long weightedBefore = 0;
            long weightedAfter = 0;
            var reachableBefore = 0;
            var reachableAfter = 0;
            var unroutedBefore = 0;
            var unroutedAfter = 0;
            var failureAmounts = new SortedDictionary<TrafficRouteFailureReason, int>();
            var rows = _runtime.TripDemandSystem.State.Rows;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.Status != TripDemandStatus.Matched)
                    continue;

                var before = beforePlanner.GetRoutes(row.Origin.RoadCell, row.Destination.RoadCell);
                if (before.IsReachable)
                {
                    reachableBefore = checked(reachableBefore + row.Amount);
                    weightedBefore = checked(weightedBefore +
                        (long)row.Amount * before.Routes[0].FreeFlowTravelSeconds);
                }
                else
                {
                    unroutedBefore = checked(unroutedBefore + row.Amount);
                }

                var after = afterPlanner.GetRoutes(row.Origin.RoadCell, row.Destination.RoadCell);
                if (after.IsReachable)
                {
                    reachableAfter = checked(reachableAfter + row.Amount);
                    weightedAfter = checked(weightedAfter +
                        (long)row.Amount * after.Routes[0].FreeFlowTravelSeconds);
                }
                else
                {
                    unroutedAfter = checked(unroutedAfter + row.Amount);
                    failureAmounts.TryGetValue(after.FailureReason, out var current);
                    failureAmounts[after.FailureReason] = checked(current + row.Amount);
                }
            }

            var primaryReason = TrafficRouteFailureReason.None;
            var primaryAmount = 0;
            foreach (var pair in failureAmounts)
            {
                if (pair.Value > primaryAmount)
                {
                    primaryAmount = pair.Value;
                    primaryReason = pair.Key;
                }
            }

            return new OdComparison(
                reachableBefore,
                reachableAfter,
                unroutedBefore,
                unroutedAfter,
                ToAverage(weightedBefore, reachableBefore),
                ToAverage(weightedAfter, reachableAfter),
                primaryReason);
        }

        private static RoadNetworkState CloneRoads(RoadNetworkState source)
        {
            var clone = new RoadNetworkState(source.Width, source.Height);
            var cells = source.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (!cell.IsOccupied)
                    continue;
                if (!clone.TryPlaceRoad(cell.Coordinate, cell.DefinitionId, cell.TravelDirection))
                    throw new InvalidOperationException("Road scratch clone failed.");
            }
            return clone;
        }

        private static RoadTravelDirection ResolveDirection(UpgradeRoadCommand command)
        {
            if (command.DirectionMode == RoadDirectionMode.Bidirectional)
                return RoadTravelDirection.Bidirectional;
            if (command.Cells.Count < 2)
                return RoadTravelDirection.Bidirectional;
            return RoadTravelDirectionRules.FromPath(
                command.Start,
                command.End,
                command.DirectionMode == RoadDirectionMode.Reverse);
        }

        private static IReadOnlyList<RoadTravelDirection> BuildDirections(UpgradeRoadCommand command)
        {
            var resolved = ResolveDirection(command);
            var result = new RoadTravelDirection[command.Cells.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = resolved;
            return Array.AsReadOnly(result);
        }

        private static int CompareEdge(
            CellCoord leftA,
            CellCoord leftB,
            CellCoord rightA,
            CellCoord rightB)
        {
            var comparison = leftA.CompareTo(rightA);
            return comparison != 0 ? comparison : leftB.CompareTo(rightB);
        }

        private static int ToAverage(long weighted, int amount)
        {
            if (amount <= 0)
                return 0;
            var value = weighted / amount;
            if (value > int.MaxValue)
                throw new OverflowException("Road upgrade preview travel time exceeded Int32 range.");
            return (int)value;
        }

        private readonly struct CongestionComparison
        {
            public CongestionComparison(
                int currentMaximumPermille,
                int predictedMaximumPermille,
                int currentCongestedCount,
                int predictedCongestedCount,
                CellCoord bottleneckA,
                CellCoord bottleneckB,
                bool hasBottleneck)
            {
                CurrentMaximumPermille = currentMaximumPermille;
                PredictedMaximumPermille = predictedMaximumPermille;
                CurrentCongestedCount = currentCongestedCount;
                PredictedCongestedCount = predictedCongestedCount;
                BottleneckA = bottleneckA;
                BottleneckB = bottleneckB;
                HasBottleneck = hasBottleneck;
            }

            public int CurrentMaximumPermille { get; }
            public int PredictedMaximumPermille { get; }
            public int CurrentCongestedCount { get; }
            public int PredictedCongestedCount { get; }
            public CellCoord BottleneckA { get; }
            public CellCoord BottleneckB { get; }
            public bool HasBottleneck { get; }
        }

        private readonly struct OdComparison
        {
            public OdComparison(
                int reachableBefore,
                int reachableAfter,
                int unroutedBefore,
                int unroutedAfter,
                int averageSecondsBefore,
                int averageSecondsAfter,
                TrafficRouteFailureReason primaryNewFailureReason)
            {
                ReachableBefore = reachableBefore;
                ReachableAfter = reachableAfter;
                UnroutedBefore = unroutedBefore;
                UnroutedAfter = unroutedAfter;
                AverageSecondsBefore = averageSecondsBefore;
                AverageSecondsAfter = averageSecondsAfter;
                PrimaryNewFailureReason = primaryNewFailureReason;
            }

            public int ReachableBefore { get; }
            public int ReachableAfter { get; }
            public int UnroutedBefore { get; }
            public int UnroutedAfter { get; }
            public int AverageSecondsBefore { get; }
            public int AverageSecondsAfter { get; }
            public TrafficRouteFailureReason PrimaryNewFailureReason { get; }
        }
    }
}
