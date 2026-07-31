using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadHierarchyBenchmarkTests
    {
        [Test]
        public void LargeGridUpgradeAndDirectedRouteBaselineIsDeterministic()
        {
            const int size = 128;
            var roads = new RoadNetworkState(size, size);
            var allCells = new List<CellCoord>(size * size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    allCells.Add(new CellCoord(x, y));
            }
            Assert.That(roads.TryApplyPlacement(
                allCells,
                RoadHierarchyDefaults.SmallRoadDefinitionId), Is.True);

            var graph = new RoadConnectivityGraph(roads, Array.Empty<ExternalConnectionState>());
            var catalog = RoadHierarchyDefaults.CreateCatalog();
            var planner = new DeterministicRoutePlanner(
                roads,
                graph,
                catalog,
                new TrafficRoutingParameters(1));
            var budget = new RoadConstructionBudget(1_000_000);
            var handler = new RoadCommandHandler(roads, catalog, budget);
            var row = new CellCoord[size];
            for (var x = 0; x < size; x++)
                row[x] = new CellCoord(x, 0);

            var upgradeWatch = Stopwatch.StartNew();
            var upgrade = handler.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                row));
            upgradeWatch.Stop();

            var routeWatch = Stopwatch.StartNew();
            var forward = planner.GetRoutes(CellCoord.Zero, new CellCoord(size - 1, 0));
            var reverse = planner.GetRoutes(new CellCoord(size - 1, 0), CellCoord.Zero);
            routeWatch.Stop();

            Assert.That(upgrade.IsApplied, Is.True, upgrade.Message);
            Assert.That(upgrade.Cost, Is.EqualTo((25 - 6) * size));
            Assert.That(forward.IsReachable, Is.True);
            Assert.That(forward.Routes[0].EdgeCount, Is.EqualTo(size - 1));
            Assert.That(reverse.IsReachable, Is.False);
            Assert.That(reverse.FailureReason, Is.EqualTo(TrafficRouteFailureReason.NoPath));
            Assert.That(row.All(cell => roads.GetTravelDirection(cell) == RoadTravelDirection.East), Is.True);
            Assert.That(upgradeWatch.ElapsedMilliseconds, Is.LessThan(30_000));
            Assert.That(routeWatch.ElapsedMilliseconds, Is.LessThan(30_000));

            TestContext.Out.WriteLine(
                $"P5-04A 128x128 hierarchy baseline: upgradeMs={upgradeWatch.Elapsed.TotalMilliseconds:0.000}, " +
                $"directedQueriesMs={routeWatch.Elapsed.TotalMilliseconds:0.000}, edges={forward.Routes[0].EdgeCount}, " +
                $"roadChecksum={roads.ComputeChecksum()}");
        }
    }
}
