using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PublicServicePersistenceGuardTests
    {
        [Test]
        public void FacilityAndPendingBudgetRoundTripRebuildSameFirstMonthlyResult()
        {
            var source = CreateRuntimeWithRoad();
            var sourceModule = PublicServiceRuntimeRegistry.GetOrCreate(source);
            var cell = new CellCoord(2, 3);
            var definitionId = PublicServiceDefinitionIds.Police;
            var facilityId = sourceModule.FacilityIdFor(definitionId, cell);
            Assert.That(sourceModule.Execute(new PlaceServiceFacilityCommand(
                facilityId,
                definitionId,
                cell)).IsApplied, Is.True);
            Assert.That(sourceModule.Execute(new SetServiceBudgetCommand(
                facilityId,
                500)).IsApplied, Is.True);
            var snapshot = sourceModule.Capture();

            var restoredRuntime = CreateRuntimeWithRoad();
            var restored = PublicServiceRuntimeRegistry.GetOrCreate(restoredRuntime);
            restored.Restore(snapshot);
            var restoredFacility = restored.Facilities.Facilities.Single();
            Assert.That(restoredFacility.Id, Is.EqualTo(facilityId));
            Assert.That(restoredFacility.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(restoredFacility.ActiveBudgetPermille, Is.EqualTo(1000));
            Assert.That(restoredFacility.HasPendingBudget, Is.True);
            Assert.That(restoredFacility.PendingBudgetPermille, Is.EqualTo(500));

            source.ProcessSimulationTick(source.MonthlyScheduler.NextRunTick);
            restoredRuntime.ProcessSimulationTick(restoredRuntime.MonthlyScheduler.NextRunTick);
            Assert.That(restored.System.State.ComputeChecksum(),
                Is.EqualTo(sourceModule.System.State.ComputeChecksum()));
            Assert.That(restored.Facilities.Facilities.Single().ActiveBudgetPermille,
                Is.EqualTo(500));
        }

        [Test]
        public void UnknownDefinitionIsRejectedBeforeReplacingExistingFacilities()
        {
            var runtime = CreateRuntimeWithRoad();
            var module = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            var cell = new CellCoord(2, 3);
            var facilityId = module.FacilityIdFor(PublicServiceDefinitionIds.Police, cell);
            Assert.That(module.Execute(new PlaceServiceFacilityCommand(
                facilityId,
                PublicServiceDefinitionIds.Police,
                cell)).IsApplied, Is.True);
            var snapshot = module.Capture();
            snapshot.Facilities[0].DefinitionId = 0xFFFF_FFFF_FFFF_FFFFUL;

            Assert.Throws<ArgumentException>(() => module.Restore(snapshot));
            Assert.That(module.Facilities.Facilities.Single().Id, Is.EqualTo(facilityId));
        }

        [Test]
        public void DuplicateOrDescendingFacilityIdsAreRejected()
        {
            var runtime = CreateRuntimeWithRoad();
            var module = PublicServiceRuntimeRegistry.GetOrCreate(runtime);
            var firstCell = new CellCoord(2, 3);
            var secondCell = new CellCoord(4, 3);
            var firstId = module.FacilityIdFor(PublicServiceDefinitionIds.Police, firstCell);
            var secondId = module.FacilityIdFor(PublicServiceDefinitionIds.Fire, secondCell);
            Assert.That(module.Execute(new PlaceServiceFacilityCommand(
                firstId, PublicServiceDefinitionIds.Police, firstCell)).IsApplied, Is.True);
            Assert.That(module.Execute(new PlaceServiceFacilityCommand(
                secondId, PublicServiceDefinitionIds.Fire, secondCell)).IsApplied, Is.True);
            var snapshot = module.Capture();
            Array.Reverse(snapshot.Facilities);

            Assert.Throws<ArgumentException>(() => module.Restore(snapshot));
        }

        private static CityRuntime CreateRuntimeWithRoad()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x701UL,
                InitialFunds = 20_000
            });
            var road = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 4),
                new CellCoord(6, 4)));
            Assert.That(road.IsApplied, Is.True, road.Message);
            return runtime;
        }
    }
}
