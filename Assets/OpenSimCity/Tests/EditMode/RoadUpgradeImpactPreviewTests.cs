using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadUpgradeImpactPreviewTests
    {
        [Test]
        public void AvenuePreviewImprovesCapacityAndFreeFlowWithoutMutatingLiveTraffic()
        {
            var runtime = CreateTrafficRuntime();
            var start = new CellCoord(0, 5);
            var end = new CellCoord(15, 5);
            var roadChecksum = runtime.Roads.ComputeChecksum();
            var trafficChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();
            var funds = runtime.Funds;
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Bidirectional);

            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            var comparison = tool.RoadUpgradePreview;

            Assert.That(comparison, Is.Not.Null);
            Assert.That(comparison.IsValid, Is.True);
            Assert.That(comparison.ReachableTripAmountBefore, Is.GreaterThan(0));
            Assert.That(comparison.ReachableTripAmountAfter,
                Is.EqualTo(comparison.ReachableTripAmountBefore));
            Assert.That(comparison.UnroutedTripAmountAfter,
                Is.EqualTo(comparison.UnroutedTripAmountBefore));
            Assert.That(comparison.CurrentMaximumVolumeCapacityPermille, Is.GreaterThan(0));
            Assert.That(comparison.PredictedMaximumVolumeCapacityPermille,
                Is.LessThan(comparison.CurrentMaximumVolumeCapacityPermille));
            Assert.That(comparison.AverageFreeFlowSecondsBefore, Is.GreaterThan(0));
            Assert.That(comparison.AverageFreeFlowSecondsAfter,
                Is.LessThan(comparison.AverageFreeFlowSecondsBefore));
            Assert.That(comparison.HasPredictedBottleneck, Is.True);
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksum));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(trafficChecksum));
            Assert.That(runtime.Funds, Is.EqualTo(funds));
        }

        [Test]
        public void ReverseOneWayPreviewReportsExplicitNoPathTripRisk()
        {
            var runtime = CreateTrafficRuntime();
            var start = new CellCoord(0, 5);
            var end = new CellCoord(15, 5);
            var roadChecksum = runtime.Roads.ComputeChecksum();
            var trafficChecksum = runtime.TrafficRoutingSystem.State.ComputeChecksum();
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Reverse);

            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            var comparison = tool.RoadUpgradePreview;

            Assert.That(comparison, Is.Not.Null);
            Assert.That(comparison.IsValid, Is.True);
            Assert.That(comparison.UnroutedTripAmountAfter,
                Is.GreaterThan(comparison.UnroutedTripAmountBefore));
            Assert.That(comparison.ReachableTripAmountAfter,
                Is.LessThan(comparison.ReachableTripAmountBefore));
            Assert.That(comparison.UnroutedTripAmountDelta, Is.GreaterThan(0));
            Assert.That(RoadUpgradePreviewFormatting.PrimaryFailureLabel(comparison),
                Is.EqualTo("방향 때문에 경로 없음"));
            Assert.That(RoadUpgradePreviewFormatting.DirectionLabel(comparison),
                Is.EqualTo("← 서쪽"));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksum));
            Assert.That(runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(trafficChecksum));
        }

        private static CityRuntime CreateTrafficRuntime()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 16,
                Height = 10,
                Seed = 0x504BUL,
                InitialFunds = 100_000,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(2045, 1)
            });

            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Street,
                new CellCoord(0, 5),
                new CellCoord(15, 5))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(1, 3),
                new CellCoord(4, 4))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.CommercialLow,
                new CellCoord(6, 3),
                new CellCoord(9, 4))));
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new CellCoord(11, 3),
                new CellCoord(14, 4))));
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(new CellCoord(0, 4)),
                runtime.DefaultPowerPlantDefinitionId,
                new CellCoord(0, 4))));

            runtime.Parcels.RefreshIfNeeded();
            AddBuilding(runtime, 0x504B01UL, 0, new CellCoord(1, 3), occupancy: 4, filledJobs: 0);
            AddBuilding(runtime, 0x504B02UL, 1, new CellCoord(6, 3), occupancy: 0, filledJobs: 4);
            AddBuilding(runtime, 0x504B03UL, 2, new CellCoord(11, 3), occupancy: 0, filledJobs: 4);
            runtime.RefreshReadModels();

            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
            Assert.That(runtime.TripDemandSystem.State.HasProcessedMonth, Is.True);
            Assert.That(runtime.TripDemandSystem.State.Rows.Any(row =>
                row.Status == OpenSimCity.Simulation.Traffic.TripDemandStatus.Matched), Is.True);
            Assert.That(runtime.TrafficRoutingSystem.State.Edges, Is.Not.Empty);
            return runtime;
        }

        private static void AddBuilding(
            CityRuntime runtime,
            ulong buildingId,
            int definitionIndex,
            CellCoord anchor,
            int occupancy,
            int filledJobs)
        {
            ParcelState parcel = null;
            for (var index = 0; index < runtime.Parcels.Parcels.Count; index++)
            {
                if (runtime.Parcels.Parcels[index].Anchor == anchor)
                {
                    parcel = runtime.Parcels.Parcels[index];
                    break;
                }
            }

            Assert.That(parcel, Is.Not.Null, $"Expected parcel at {anchor}.");
            runtime.Buildings.Create(
                new StableId(buildingId),
                runtime.BuildingDefinitions.Definitions[definitionIndex].Id,
                parcel.Id,
                anchor,
                occupancy,
                filledJobs);
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
