using System.Diagnostics;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadUpgradeUxBenchmarkTests
    {
        private const ulong ExpectedRoadChecksum = 0xB7B3B055E1A33B45UL;

        [Test]
        public void LargeGridPreviewReconfigurationAndCommitRemainDeterministic()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 128,
                Height = 128,
                InitialFunds = 1_000_000
            });
            var start = new CellCoord(0, 0);
            var end = new CellCoord(127, 0);
            var placement = runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Street,
                start,
                end));
            Assert.That(placement.IsApplied, Is.True, placement.Message);

            var roadChecksumBefore = runtime.Roads.ComputeChecksum();
            var trafficChecksumBefore = runtime.TrafficRoutingSystem.State.ComputeChecksum();
            var fundsBefore = runtime.Funds;
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Forward);

            var previewWatch = Stopwatch.StartNew();
            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            previewWatch.Stop();

            Assert.That(tool.Preview.IsValid, Is.True, tool.Preview.FailureReason);
            Assert.That(tool.Preview.Cost, Is.EqualTo((25 - 10) * 128));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksumBefore));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(),
                Is.EqualTo(trafficChecksumBefore));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));

            var reconfigureWatch = Stopwatch.StartNew();
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Reverse);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Bidirectional);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Forward);
            reconfigureWatch.Stop();

            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksumBefore));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(),
                Is.EqualTo(trafficChecksumBefore));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));

            var commitWatch = Stopwatch.StartNew();
            var result = tool.CommitDrag();
            commitWatch.Stop();

            Assert.That(result.Committed, Is.True, result.Message);
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(ExpectedRoadChecksum));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore - result.Cost));
            Assert.That(runtime.Roads.OccupiedCellCount, Is.EqualTo(128));

            TestContext.WriteLine(
                $"P5-04B 128x128 UX benchmark: previewMs={previewWatch.Elapsed.TotalMilliseconds:F3}; " +
                $"reconfigure3xMs={reconfigureWatch.Elapsed.TotalMilliseconds:F3}; " +
                $"commitMs={commitWatch.Elapsed.TotalMilliseconds:F3}; " +
                $"previewRoadChecksum={roadChecksumBefore:X16}; " +
                $"previewTrafficChecksum={trafficChecksumBefore:X16}; " +
                $"finalRoadChecksum={runtime.Roads.ComputeChecksum():X16}");
        }
    }
}
