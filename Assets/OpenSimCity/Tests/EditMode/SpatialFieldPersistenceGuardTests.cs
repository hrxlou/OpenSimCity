using System;
using NUnit.Framework;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Simulation.Spatial;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpatialFieldPersistenceGuardTests
    {
        [Test]
        public void CapabilityRestoreRejectsDerivedRoadAndBuildingSourceIds()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x605UL
            });
            var module = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            var invalid = new SpatialFieldRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Sources = new[]
                {
                    new SpatialFieldSourceStateDto
                    {
                        Id = SpatialDerivedSourceIds.Road(3).Value,
                        DefinitionId = SpatialSourceDefinitionIds.TrafficNoise.Value,
                        CellX = 3,
                        CellY = 0,
                        IsEnabled = true
                    }
                }
            };

            Assert.Throws<InvalidOperationException>(() => module.Restore(invalid));
            Assert.That(module.Capture().Sources, Is.Empty);
        }

        [Test]
        public void PublicCommandsRejectReservedDerivedIdsWithoutMutatingFields()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x606UL
            });
            var module = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            var checksum = module.Fields.ComputeChecksum();
            var result = module.Execute(new UpsertSpatialSourceCommand(
                SpatialDerivedSourceIds.Building(new StableId(42)),
                SpatialSourceDefinitionIds.IndustrialEmitter,
                new OpenSimCity.Core.Mathematics.CellCoord(2, 2)));

            Assert.That(result.IsApplied, Is.False);
            Assert.That(result.Details, Is.TypeOf<SpatialFieldCommandResult>());
            Assert.That(((SpatialFieldCommandResult)result.Details).FailureReason,
                Is.EqualTo(SpatialFieldCommandFailureReason.ReservedDerivedId));
            Assert.That(module.Fields.ComputeChecksum(), Is.EqualTo(checksum));
        }
    }
}
