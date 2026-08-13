using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Traffic;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadUpgradeConstructionFlowTests
    {
        [Test]
        public void PreviewCancelAndCommitKeepOneNoDemolitionContract()
        {
            var runtime = NewRuntime();
            var start = new CellCoord(0, 1);
            var end = new CellCoord(3, 1);
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Small,
                start,
                end)));

            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Forward);

            var fundsBefore = runtime.Funds;
            var checksumBefore = runtime.Roads.ComputeChecksum();
            var occupiedBefore = runtime.Roads.Cells.Count(cell => cell.IsOccupied);
            tool.BeginDrag(start);
            tool.UpdateDrag(end);

            Assert.That(tool.Preview.IsValid, Is.True, tool.Preview.FailureReason);
            Assert.That(tool.Preview.Operation, Is.EqualTo(ConstructionOperation.UpgradeRoad));
            Assert.That(tool.Preview.Cost, Is.EqualTo((25 - 6) * 4));
            Assert.That(tool.RoadUpgradePreview, Is.Not.Null);
            Assert.That(tool.RoadUpgradePreview.CurrentBuildCost, Is.EqualTo(6 * 4));
            Assert.That(tool.RoadUpgradePreview.TargetBuildCost, Is.EqualTo(25 * 4));
            Assert.That(tool.RoadUpgradePreview.CurrentMonthlyMaintenance, Is.Zero);
            Assert.That(tool.RoadUpgradePreview.TargetMonthlyMaintenance, Is.EqualTo(5 * 4));
            Assert.That(tool.RoadUpgradePreview.CurrentCapacity, Is.EqualTo(500 * 4));
            Assert.That(tool.RoadUpgradePreview.TargetCapacity, Is.EqualTo(2400 * 4));
            Assert.That(tool.RoadUpgradePreview.CurrentMinimumSpeed, Is.EqualTo(20));
            Assert.That(tool.RoadUpgradePreview.TargetMinimumSpeed, Is.EqualTo(45));
            Assert.That(tool.RoadUpgradePreview.CurrentNoise, Is.EqualTo(15 * 4));
            Assert.That(tool.RoadUpgradePreview.TargetNoise, Is.EqualTo(70 * 4));
            Assert.That(RoadUpgradePreviewFormatting.DirectionLabel(tool.RoadUpgradePreview),
                Is.EqualTo("→ 동쪽"));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(checksumBefore));

            tool.CancelDrag();

            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(checksumBefore));
            Assert.That(runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(occupiedBefore));

            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            var comparison = tool.RoadUpgradePreview;
            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True, result.Message);
            Assert.That(result.Cost, Is.EqualTo((25 - 6) * 4));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore - result.Cost));
            Assert.That(runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(occupiedBefore));
            Assert.That(tool.LastCommittedRoadUpgradePreview, Is.SameAs(comparison));
            for (var x = 0; x < 4; x++)
            {
                var cell = new CellCoord(x, 1);
                Assert.That(runtime.Roads.GetRoadDefinitionId(cell),
                    Is.EqualTo(RoadUpgradeDefinitionIds.Avenue));
                Assert.That(runtime.Roads.GetTravelDirection(cell),
                    Is.EqualTo(RoadTravelDirection.East));
            }

            var forward = runtime.TrafficRoutingSystem.Planner.GetRoutes(start, end);
            var reverse = runtime.TrafficRoutingSystem.Planner.GetRoutes(end, start);
            Assert.That(forward.IsReachable, Is.True);
            Assert.That(reverse.IsReachable, Is.False);
            Assert.That(reverse.FailureReason, Is.EqualTo(TrafficRouteFailureReason.NoPath));
        }

        [Test]
        public void ReconfiguringTierAndDirectionDuringDragRebuildsPreviewOnly()
        {
            var runtime = NewRuntime();
            var start = new CellCoord(0, 2);
            var end = new CellCoord(2, 2);
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Street,
                start,
                end)));
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            var checksum = runtime.Roads.ComputeChecksum();
            var funds = runtime.Funds;

            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Reverse);

            Assert.That(tool.Preview.IsValid, Is.True);
            Assert.That(tool.Preview.Cost, Is.EqualTo((25 - 10) * 3));
            Assert.That(RoadUpgradePreviewFormatting.DirectionLabel(tool.RoadUpgradePreview),
                Is.EqualTo("← 서쪽"));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(checksum));
            Assert.That(runtime.Funds, Is.EqualTo(funds));
        }

        private static CityRuntime NewRuntime()
        {
            return new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = 10_000
            });
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
