using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Services;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ServiceResponseProjectionTests
    {
        [Test]
        public void OnlyUnmetRowsBecomeEventsAndOnlyAssignedRoadServiceGetsVehicleSample()
        {
            var runtime = BuildFixture(out var building);
            var module = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            var facilityCell = new CellCoord(1, 3);
            var facilityId = module.FacilityIdFor(ServiceDefinitionIds.Police, facilityCell);
            var placed = module.Execute(new PlaceServiceFacilityCommand(
                facilityId,
                ServiceDefinitionIds.Police,
                facilityCell));
            Assert.That(placed.IsApplied, Is.True, placed.Message);

            module.System.RunMonth(new SimulationDate(1, 1));
            module.ReadModel.RefreshIfNeeded();
            var projection = new ServiceResponseProjection(module);
            var police = projection.Events.Single(row => row.Kind == ServiceKindView.Police);

            Assert.That(projection.Events, Has.Count.EqualTo(5));
            Assert.That(police.BuildingId, Is.EqualTo(building.Id));
            Assert.That(police.FailureReason, Is.EqualTo(ServiceFailureReasonView.CapacityExceeded));
            Assert.That(police.Unmet, Is.GreaterThan(0));
            Assert.That(police.HasRoute, Is.True);
            Assert.That(projection.Samples, Has.Count.EqualTo(1));
            Assert.That(projection.Samples[0].EventId, Is.EqualTo(police.Id));
            Assert.That(projection.Samples[0].Path.Cells, Is.Not.Empty);
            Assert.That(projection.Events.Where(row => row.Kind != ServiceKindView.Police)
                .All(row => !row.HasRoute), Is.True);
        }

        [Test]
        public void SampleLimitAndObservationDoNotChangeServiceChecksum()
        {
            var runtime = BuildFixture(out _);
            var module = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            var facilityCell = new CellCoord(1, 3);
            Assert.That(module.Execute(new PlaceServiceFacilityCommand(
                module.FacilityIdFor(ServiceDefinitionIds.Police, facilityCell),
                ServiceDefinitionIds.Police,
                facilityCell)).IsApplied, Is.True);
            module.System.RunMonth(new SimulationDate(1, 1));
            module.ReadModel.RefreshIfNeeded();

            var projection = new ServiceResponseProjection(module);
            var checksum = module.System.State.ComputeChecksum();
            var firstId = projection.Samples.Single().Id;
            var firstPath = projection.Samples.Single().Path.Cells.ToArray();
            Assert.That(projection.SourceSimulationChecksum, Is.EqualTo(checksum));

            projection.SetMaximumSamples(0);
            projection.RefreshIfNeeded();
            Assert.That(projection.Samples, Is.Empty);
            Assert.That(module.System.State.ComputeChecksum(), Is.EqualTo(checksum));

            projection.SetMaximumSamples(16);
            projection.RefreshIfNeeded();
            Assert.That(projection.Samples.Single().Id, Is.EqualTo(firstId));
            Assert.That(projection.Samples.Single().Path.Cells, Is.EqualTo(firstPath));
            Assert.That(module.System.State.ComputeChecksum(), Is.EqualTo(checksum));
        }

        private static CityRuntime BuildFixture(out BuildingState building)
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 12,
                Height = 8,
                Seed = 0x703UL,
                InitialFunds = 100_000,
                TicksPerMonth = 1
            });
            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 4),
                new CellCoord(10, 4))).IsApplied, Is.True);
            Assert.That(runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow,
                new CellCoord(2, 2),
                new CellCoord(3, 3))).IsApplied, Is.True);
            runtime.Parcels.RefreshIfNeeded();
            var parcel = runtime.Parcels.Parcels.Single();
            var definition = runtime.BuildingDefinitions.Definitions.First(row =>
                row.ZoneType == ZoneType.ResidentialLow &&
                row.Footprint.Width <= parcel.Width &&
                row.Footprint.Height <= parcel.Height);
            building = new BuildingState(
                new StableId(0xB703000000000001UL),
                definition.Id,
                parcel.Id,
                parcel.Anchor,
                definition.Footprint,
                occupancy: 300,
                filledJobs: 0);
            runtime.Buildings.Add(building);
            return runtime;
        }
    }
}
