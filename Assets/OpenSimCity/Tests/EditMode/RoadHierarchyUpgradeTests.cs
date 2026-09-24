using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadHierarchyUpgradeTests
    {
        [Test]
        public void DefaultHierarchyUsesStableIdsAndMonotonicTradeoffs()
        {
            var catalog = RoadHierarchyDefaults.CreateCatalog();
            var small = catalog.Get(RoadHierarchyDefaults.SmallRoadDefinitionId);
            var street = catalog.Get(RoadHierarchyDefaults.StreetDefinitionId);
            var avenue = catalog.Get(RoadHierarchyDefaults.AvenueDefinitionId);
            var boulevard = catalog.Get(RoadHierarchyDefaults.BoulevardDefinitionId);

            Assert.That(catalog.Count, Is.EqualTo(4));
            Assert.That(catalog.Definitions.Select(row => row.Id), Is.Ordered);
            Assert.That(small.BuildCost, Is.LessThan(street.BuildCost));
            Assert.That(street.BuildCost, Is.LessThan(avenue.BuildCost));
            Assert.That(avenue.BuildCost, Is.LessThan(boulevard.BuildCost));
            Assert.That(small.MonthlyMaintenanceCost, Is.LessThan(street.MonthlyMaintenanceCost));
            Assert.That(street.MonthlyMaintenanceCost, Is.LessThan(avenue.MonthlyMaintenanceCost));
            Assert.That(avenue.MonthlyMaintenanceCost, Is.LessThan(boulevard.MonthlyMaintenanceCost));
            Assert.That(small.CapacityPerMonth, Is.LessThan(street.CapacityPerMonth));
            Assert.That(street.CapacityPerMonth, Is.LessThan(avenue.CapacityPerMonth));
            Assert.That(avenue.CapacityPerMonth, Is.LessThan(boulevard.CapacityPerMonth));
            Assert.That(small.FreeFlowSpeedCellsPerHour, Is.LessThan(street.FreeFlowSpeedCellsPerHour));
            Assert.That(street.FreeFlowSpeedCellsPerHour, Is.LessThan(avenue.FreeFlowSpeedCellsPerHour));
            Assert.That(avenue.FreeFlowSpeedCellsPerHour, Is.LessThan(boulevard.FreeFlowSpeedCellsPerHour));
            Assert.That(small.NoiseEmission, Is.LessThan(street.NoiseEmission));
            Assert.That(street.NoiseEmission, Is.LessThan(avenue.NoiseEmission));
            Assert.That(avenue.NoiseEmission, Is.LessThan(boulevard.NoiseEmission));
        }

        [Test]
        public void UpgradeChangesDefinitionAndDirectionWithoutRemovingRoadCells()
        {
            var roads = new RoadNetworkState(3, 1);
            var budget = new RoadConstructionBudget(100);
            var handler = new RoadCommandHandler(roads, RoadHierarchyDefaults.CreateCatalog(), budget);
            var path = new[] { new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0) };

            var placement = handler.Execute(new PlaceRoadCommand(
                RoadHierarchyDefaults.SmallRoadDefinitionId,
                path));
            var versionBeforeUpgrade = roads.Version;
            var fundsBeforeUpgrade = budget.Funds;
            var result = handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                path));

            Assert.That(placement.IsApplied, Is.True);
            Assert.That(result.IsApplied, Is.True, result.Message);
            Assert.That(result.Cost, Is.EqualTo((25 - 6) * 3));
            Assert.That(budget.Funds, Is.EqualTo(fundsBeforeUpgrade - result.Cost));
            Assert.That(roads.Version, Is.EqualTo(versionBeforeUpgrade + 1));
            Assert.That(path.All(roads.IsOccupied), Is.True);
            Assert.That(path.All(cell =>
                roads.GetRoadDefinitionId(cell) == RoadHierarchyDefaults.AvenueDefinitionId), Is.True);
            Assert.That(path.All(cell => roads.GetTravelDirection(cell) == RoadTravelDirection.East), Is.True);
        }

        [Test]
        public void DowngradeHasNoRefundAndRepeatedSettingIsRejected()
        {
            var roads = new RoadNetworkState(2, 1);
            Assert.That(roads.TryApplyPlacement(
                new[] { CellCoord.Zero, new CellCoord(1, 0) },
                RoadHierarchyDefaults.AvenueDefinitionId), Is.True);
            var budget = new RoadConstructionBudget(50);
            var handler = new RoadCommandHandler(roads, RoadHierarchyDefaults.CreateCatalog(), budget);
            var command = new UpgradeRoadCommand(
                RoadHierarchyDefaults.SmallRoadDefinitionId,
                RoadDirectionMode.Bidirectional,
                CellCoord.Zero,
                new CellCoord(1, 0));

            var downgrade = handler.Execute(command);
            var repeated = handler.Execute(command);

            Assert.That(downgrade.IsApplied, Is.True);
            Assert.That(downgrade.Cost, Is.Zero);
            Assert.That(budget.Funds, Is.EqualTo(50));
            Assert.That(repeated.IsApplied, Is.False);
            Assert.That(repeated.FailureReason, Is.EqualTo(RoadCommandFailureReason.NoChange));
            Assert.That(budget.Funds, Is.EqualTo(50));
        }

        [Test]
        public void InsufficientFundsAndInvalidBatchLeaveEveryStateUnchanged()
        {
            var roads = new RoadNetworkState(3, 1);
            Assert.That(roads.TryApplyPlacement(
                new[] { CellCoord.Zero, new CellCoord(1, 0) },
                RoadHierarchyDefaults.SmallRoadDefinitionId), Is.True);
            var budget = new RoadConstructionBudget(10);
            var handler = new RoadCommandHandler(roads, RoadHierarchyDefaults.CreateCatalog(), budget);
            var checksum = roads.ComputeChecksum();
            var version = roads.Version;

            var insufficient = handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                CellCoord.Zero,
                new CellCoord(1, 0)));
            var invalid = handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.StreetDefinitionId,
                RoadDirectionMode.Bidirectional,
                CellCoord.Zero,
                new CellCoord(2, 0)));

            Assert.That(insufficient.FailureReason, Is.EqualTo(RoadCommandFailureReason.InsufficientFunds));
            Assert.That(invalid.FailureReason, Is.EqualTo(RoadCommandFailureReason.NoRoad));
            Assert.That(budget.Funds, Is.EqualTo(10));
            Assert.That(roads.Version, Is.EqualTo(version));
            Assert.That(roads.ComputeChecksum(), Is.EqualTo(checksum));
        }

        [Test]
        public void OneWayDirectionProducesForwardRouteAndExplicitReverseNoPath()
        {
            var roads = new RoadNetworkState(3, 1);
            Assert.That(roads.TryApplyPlacement(
                new[] { CellCoord.Zero, new CellCoord(1, 0), new CellCoord(2, 0) },
                RoadHierarchyDefaults.StreetDefinitionId), Is.True);
            var handler = new RoadCommandHandler(
                roads,
                RoadHierarchyDefaults.CreateCatalog(),
                new RoadConstructionBudget(100));
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                RoadHierarchyDefaults.CreateCatalog(),
                new TrafficRoutingParameters(1));

            Assert.That(handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.StreetDefinitionId,
                RoadDirectionMode.Forward,
                CellCoord.Zero,
                new CellCoord(2, 0))).IsApplied, Is.True);

            Assert.That(planner.GetRoutes(CellCoord.Zero, new CellCoord(2, 0)).IsReachable, Is.True);
            var reverse = planner.GetRoutes(new CellCoord(2, 0), CellCoord.Zero);
            Assert.That(reverse.IsReachable, Is.False);
            Assert.That(reverse.FailureReason, Is.EqualTo(TrafficRouteFailureReason.NoPath));

            Assert.That(handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.StreetDefinitionId,
                RoadDirectionMode.Reverse,
                CellCoord.Zero,
                new CellCoord(2, 0))).IsApplied, Is.True);
            Assert.That(planner.GetRoutes(new CellCoord(2, 0), CellCoord.Zero).IsReachable, Is.True);
            Assert.That(planner.GetRoutes(CellCoord.Zero, new CellCoord(2, 0)).FailureReason,
                Is.EqualTo(TrafficRouteFailureReason.NoPath));
        }

        [Test]
        public void DirectionEditInvalidatesAffectedComponentAndRetainsUnrelatedRoute()
        {
            var roads = new RoadNetworkState(8, 4);
            Assert.That(roads.TryApplyPlacement(new[]
            {
                new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0),
                new CellCoord(5, 3), new CellCoord(6, 3), new CellCoord(7, 3)
            }, RoadHierarchyDefaults.StreetDefinitionId), Is.True);
            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                RoadHierarchyDefaults.CreateCatalog(),
                new TrafficRoutingParameters(1));
            var affected = planner.GetRoutes(new CellCoord(0, 0), new CellCoord(2, 0));
            var unrelated = planner.GetRoutes(new CellCoord(5, 3), new CellCoord(7, 3));

            Assert.That(roads.TryApplyReplacement(
                new[] { new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0) },
                RoadHierarchyDefaults.StreetDefinitionId,
                new[]
                {
                    RoadTravelDirection.West,
                    RoadTravelDirection.West,
                    RoadTravelDirection.West
                }), Is.True);
            var retained = planner.GetRoutes(new CellCoord(5, 3), new CellCoord(7, 3));

            Assert.That(affected.IsReachable, Is.True);
            Assert.That(retained, Is.SameAs(unrelated));
            Assert.That(planner.LastInvalidatedEntryCount, Is.EqualTo(1));
            Assert.That(planner.LastRetainedEntryCount, Is.EqualTo(1));
            Assert.That(planner.GetRoutes(new CellCoord(0, 0), new CellCoord(2, 0)).FailureReason,
                Is.EqualTo(TrafficRouteFailureReason.NoPath));
        }

        [Test]
        public void ReadModelAndReplayExposeIdenticalPureRoadState()
        {
            var first = ExecuteReplay();
            var second = ExecuteReplay();
            var model = new RoadHierarchyReadModel(first.Roads, RoadHierarchyDefaults.CreateCatalog());

            Assert.That(first.Roads.ComputeChecksum(), Is.EqualTo(second.Roads.ComputeChecksum()));
            Assert.That(first.Budget.Funds, Is.EqualTo(second.Budget.Funds));
            Assert.That(model.Cells, Has.Count.EqualTo(3));
            Assert.That(model.Cells.All(row => row.DefinitionId == RoadHierarchyDefaults.AvenueDefinitionId), Is.True);
            Assert.That(model.Cells.All(row => row.TravelDirection == RoadTravelDirection.East), Is.True);
            Assert.That(model.Cells.All(row => row.CapacityPerMonth == 2400), Is.True);
            Assert.That(model.Cells.All(row => row.NoiseEmission == 70), Is.True);

            var assemblyReferences = typeof(RoadNetworkState).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine"));
            Assert.That(assemblyReferences, Does.Not.Contain("UnityEngine.CoreModule"));
        }

        private static ReplayFixture ExecuteReplay()
        {
            var roads = new RoadNetworkState(3, 1);
            var budget = new RoadConstructionBudget(100);
            var handler = new RoadCommandHandler(roads, RoadHierarchyDefaults.CreateCatalog(), budget);
            Assert.That(handler.Execute(new PlaceRoadCommand(
                RoadHierarchyDefaults.SmallRoadDefinitionId,
                CellCoord.Zero,
                new CellCoord(2, 0))).IsApplied, Is.True);
            Assert.That(handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                CellCoord.Zero,
                new CellCoord(2, 0))).IsApplied, Is.True);
            return new ReplayFixture(roads, budget);
        }

        private sealed class ReplayFixture
        {
            public ReplayFixture(RoadNetworkState roads, RoadConstructionBudget budget)
            {
                Roads = roads;
                Budget = budget;
            }

            public RoadNetworkState Roads { get; }
            public RoadConstructionBudget Budget { get; }
        }
    }
}
