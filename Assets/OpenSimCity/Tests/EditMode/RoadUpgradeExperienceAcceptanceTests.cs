using System.Collections.Generic;
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
using OpenSimCity.Simulation.Traffic;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadUpgradeExperienceAcceptanceTests
    {
        [Test]
        public void SameOdAndBuildingsRecoverBottleneckAfterNoDemolitionAvenueUpgrade()
        {
            var runtime = CreateTrafficRuntime(initialFunds: 100_000);
            var start = new CellCoord(0, 5);
            var end = new CellCoord(15, 5);
            var beforeBuildings = BuildingSignatures(runtime);
            var beforeOccupiedRoads = runtime.Roads.Cells.Count(cell => cell.IsOccupied);
            var beforeAssignments = AssignmentAmount(runtime);
            var beforeUnrouted = UnroutedAmount(runtime);
            var beforeMaximumVc = MaximumVolumeCapacityPermille(runtime);
            var beforeAverageSeconds = WeightedAverageFreeFlowSeconds(runtime);

            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Bidirectional);
            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            var preview = tool.RoadUpgradePreview;
            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True, result.Message);
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.PredictedMaximumVolumeCapacityPermille,
                Is.LessThan(preview.CurrentMaximumVolumeCapacityPermille));
            runtime.TrafficRoutingSystem.RebuildFromCurrentState();
            runtime.RefreshReadModels();

            var afterBuildings = BuildingSignatures(runtime);
            var afterMaximumVc = MaximumVolumeCapacityPermille(runtime);
            var afterAverageSeconds = WeightedAverageFreeFlowSeconds(runtime);

            Assert.That(afterBuildings, Is.EqualTo(beforeBuildings),
                "Stable IDs, definitions, footprints, parcels, residents, and jobs must not change.");
            Assert.That(runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(beforeOccupiedRoads));
            Assert.That(AssignmentAmount(runtime), Is.EqualTo(beforeAssignments));
            Assert.That(UnroutedAmount(runtime), Is.EqualTo(beforeUnrouted));
            Assert.That(afterMaximumVc, Is.LessThan(beforeMaximumVc));
            Assert.That(afterAverageSeconds, Is.LessThan(beforeAverageSeconds));
            Assert.That(afterMaximumVc,
                Is.EqualTo(preview.PredictedMaximumVolumeCapacityPermille));
            Assert.That(afterAverageSeconds,
                Is.EqualTo(preview.AverageFreeFlowSecondsAfter));
        }

        [Test]
        public void InsufficientFundsPreviewAndCommitPublishReasonWithoutPartialUpgrade()
        {
            var runtime = CreateRoadOnlyRuntime(initialFunds: 30);
            var start = new CellCoord(0, 1);
            var end = new CellCoord(3, 1);
            var fundsBefore = runtime.Funds;
            var checksumBefore = runtime.Roads.ComputeChecksum();
            var occupiedBefore = runtime.Roads.Cells.Count(cell => cell.IsOccupied);
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Bidirectional);

            tool.BeginDrag(start);
            tool.UpdateDrag(end);
            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(RoadCommandFailureReason.InsufficientFunds.ToString()));
            Assert.That(result.Message, Is.Not.Empty);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.ComputeChecksum(), Is.EqualTo(checksumBefore));
            Assert.That(runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(occupiedBefore));
            for (var x = 0; x < 4; x++)
            {
                Assert.That(runtime.Roads.GetRoadDefinitionId(new CellCoord(x, 1)),
                    Is.EqualTo(RoadUpgradeDefinitionIds.Small));
            }
        }

        [Test]
        public void PublicImpactPublicationExposesApplyingMonthAndChangedOdReason()
        {
            var runtime = CreateTrafficRuntime(initialFunds: 100_000);
            var tool = new ConstructionTool(runtime);
            tool.SelectTool(ConstructionToolMode.RoadUpgrade);
            tool.ConfigureRoadUpgrade(RoadUpgradeDefinitionIds.Avenue, RoadDirectionMode.Reverse);
            tool.BeginDrag(new CellCoord(0, 5));
            tool.UpdateDrag(new CellCoord(15, 5));

            var publication = new RoadUpgradeImpactPublicationReadModel(runtime)
                .Publish(tool.RoadUpgradePreview);

            Assert.That(publication.AppliesToYear, Is.EqualTo(2045));
            Assert.That(publication.AppliesToMonth, Is.EqualTo(2));
            Assert.That(publication.AppliesToPeriodLabel, Is.EqualTo("2045-02"));
            Assert.That(publication.Impact.UnroutedTripAmountDelta, Is.GreaterThan(0));
            Assert.That(publication.NewReachabilityFailureReason,
                Is.EqualTo("방향 때문에 경로 없음"));
        }

        private static CityRuntime CreateRoadOnlyRuntime(int initialFunds)
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = initialFunds
            });
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Small,
                new CellCoord(0, 1),
                new CellCoord(3, 1))));
            return runtime;
        }

        private static CityRuntime CreateTrafficRuntime(int initialFunds)
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 16,
                Height = 10,
                Seed = 0x504BUL,
                InitialFunds = initialFunds,
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
            Assert.That(runtime.TripDemandSystem.State.Rows.Any(row =>
                row.Status == TripDemandStatus.Matched), Is.True);
            return runtime;
        }

        private static IReadOnlyList<string> BuildingSignatures(CityRuntime runtime)
        {
            return runtime.Buildings.Buildings.Select(building =>
            {
                var footprint = runtime.BuildingDefinitions.Get(building.DefinitionId).Footprint;
                return $"{building.Id.Value}:{building.DefinitionId.Value}:{building.ParcelId.Value}:" +
                       $"{building.Anchor.X}:{building.Anchor.Y}:{footprint.Width}:{footprint.Height}:" +
                       $"{building.Occupancy}:{building.FilledJobs}";
            }).ToArray();
        }

        private static int AssignmentAmount(CityRuntime runtime)
        {
            return runtime.TrafficRoutingSystem.State.Assignments.Sum(row => row.Amount);
        }

        private static int UnroutedAmount(CityRuntime runtime)
        {
            return runtime.TrafficRoutingSystem.State.Unrouted.Sum(row => row.Amount);
        }

        private static int MaximumVolumeCapacityPermille(CityRuntime runtime)
        {
            var rows = new TrafficVisualizationReadModel(runtime.TrafficRoutingModel).Edges;
            return rows.Count == 0 ? 0 : rows.Max(row => row.VolumeCapacityPermille);
        }

        private static int WeightedAverageFreeFlowSeconds(CityRuntime runtime)
        {
            long weighted = 0;
            var amount = 0;
            foreach (var assignment in runtime.TrafficRoutingSystem.State.Assignments)
            {
                weighted += (long)assignment.Amount * assignment.FreeFlowTravelSeconds;
                amount += assignment.Amount;
            }
            return amount == 0 ? 0 : (int)(weighted / amount);
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
