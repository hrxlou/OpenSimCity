using System;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SpatialFieldSystemTests
    {
        [Test]
        public void LinearFalloffParkEffectAndAdverseCompositionUseIntegerRules()
        {
            var system = new SpatialFieldSystem(new WorldState(16, 16, 0UL));
            var center = new CellCoord(8, 8);
            Assert.That(system.UpsertSource(new SpatialFieldSourceState(
                new StableId(1), SpatialSourceDefinitionIds.Park, center)), Is.True);
            system.RefreshDirty();

            var centerValue = system.Get(center);
            var edgeValue = system.Get(new CellCoord(12, 8));
            var outside = system.Get(new CellCoord(13, 8));
            Assert.That(centerValue.ParkAmenity, Is.EqualTo(400));
            Assert.That(centerValue.LandValue, Is.EqualTo(780));
            Assert.That(edgeValue.ParkAmenity, Is.EqualTo(80));
            Assert.That(edgeValue.LandValue, Is.EqualTo(556));
            Assert.That(outside, Is.EqualTo(new SpatialFieldCellValue(500, 0, 0, 0, 0)));

            Assert.That(system.UpsertSource(new SpatialFieldSourceState(
                new StableId(2), SpatialSourceDefinitionIds.IndustrialEmitter, center)), Is.True);
            system.RefreshDirty();
            var combined = system.Get(center);
            Assert.That(combined.Pollution, Is.EqualTo(460));
            Assert.That(combined.Noise, Is.EqualTo(220));
            Assert.That(combined.Crime, Is.EqualTo(20));
            Assert.That(combined.LandValue, Is.EqualTo(485));
        }

        [Test]
        public void MultipleSourcesClampAndStableIdOrderDoesNotChangeChecksum()
        {
            var worldA = new WorldState(16, 16, 0UL);
            var worldB = new WorldState(16, 16, 0UL);
            var a = new SpatialFieldSystem(worldA);
            var b = new SpatialFieldSystem(worldB);
            var source1 = new SpatialFieldSourceState(
                new StableId(10), SpatialSourceDefinitionIds.IndustrialEmitter, new CellCoord(7, 7));
            var source2 = new SpatialFieldSourceState(
                new StableId(20), SpatialSourceDefinitionIds.IndustrialEmitter, new CellCoord(7, 7));
            var source3 = new SpatialFieldSourceState(
                new StableId(30), SpatialSourceDefinitionIds.CrimePressure, new CellCoord(7, 7));

            a.UpsertSource(source1);
            a.UpsertSource(source2);
            a.UpsertSource(source3);
            b.UpsertSource(source3);
            b.UpsertSource(source2);
            b.UpsertSource(source1);
            a.RefreshDirty();
            b.RefreshDirty();

            Assert.That(a.Get(new CellCoord(7, 7)).Pollution, Is.EqualTo(1000));
            Assert.That(a.ComputeChecksum(), Is.EqualTo(b.ComputeChecksum()));
            Assert.That(a.Sources.Select(source => source.Id.Value), Is.EqualTo(new ulong[] { 10, 20, 30 }));
        }

        [Test]
        public void DirtyRefreshTouchesOnlyOldAndNewBoundedRegions()
        {
            var system = new SpatialFieldSystem(new WorldState(32, 32, 16, 0UL));
            var far = new CellCoord(31, 31);
            var farBefore = system.Get(far);
            var id = new StableId(100);

            system.UpsertSource(new SpatialFieldSourceState(
                id, SpatialSourceDefinitionIds.Park, new CellCoord(5, 5)));
            Assert.That(system.DirtyCellCount, Is.EqualTo(81));
            system.RefreshDirty();
            Assert.That(system.LastRefreshCellCount, Is.EqualTo(81));
            Assert.That(system.Get(far), Is.EqualTo(farBefore));

            system.UpsertSource(new SpatialFieldSourceState(
                id, SpatialSourceDefinitionIds.Park, new CellCoord(20, 20)));
            Assert.That(system.DirtyCellCount, Is.EqualTo(162));
            system.RefreshDirty();
            Assert.That(system.LastRefreshCellCount, Is.EqualTo(162));
            Assert.That(system.LastRefreshCellCount, Is.LessThan(system.World.CellCount));
            Assert.That(system.Get(far), Is.EqualTo(farBefore));

            Assert.That(system.RemoveSource(id), Is.True);
            system.RefreshDirty();
            Assert.That(system.LastRefreshCellCount, Is.EqualTo(81));
            Assert.That(system.Get(new CellCoord(20, 20)).LandValue,
                Is.EqualTo(SpatialFieldSystem.BaselineLandValue));
        }

        [Test]
        public void DirtyRegionCrossingChunkBoundaryReportsOnlyTouchedChunks()
        {
            var system = new SpatialFieldSystem(new WorldState(32, 32, 16, 0UL));
            system.UpsertSource(new SpatialFieldSourceState(
                new StableId(101), SpatialSourceDefinitionIds.Park, new CellCoord(15, 15)));
            system.RefreshDirty();

            Assert.That(system.LastRefreshedChunks, Is.EqualTo(new[]
            {
                new ChunkCoord(0, 0),
                new ChunkCoord(1, 0),
                new ChunkCoord(0, 1),
                new ChunkCoord(1, 1)
            }));
            Assert.That(system.LastRefreshChunkCount, Is.EqualTo(4));
        }

        [Test]
        public void ParkAndPollutionChangeResidentialSuitabilityInOppositeDirections()
        {
            var system = new SpatialFieldSystem(new WorldState(20, 20, 0UL));
            var query = new SpatialSuitabilityQuery(system);
            var cell = new CellCoord(10, 10);
            var baseline = query.Evaluate(ZoneType.ResidentialLow, cell).Score;

            system.UpsertSource(new SpatialFieldSourceState(
                new StableId(102), SpatialSourceDefinitionIds.IndustrialEmitter, cell));
            system.RefreshDirty();
            var polluted = query.Evaluate(ZoneType.ResidentialLow, cell).Score;
            Assert.That(polluted, Is.LessThan(baseline));

            system.UpsertSource(new SpatialFieldSourceState(
                new StableId(103), SpatialSourceDefinitionIds.Park, cell));
            system.RefreshDirty();
            var withPark = query.Evaluate(ZoneType.ResidentialLow, cell).Score;
            Assert.That(withPark, Is.GreaterThan(polluted));
            Assert.That(query.Evaluate(ZoneType.IndustrialLow, cell).Score,
                Is.Not.EqualTo(withPark));
        }

        [Test]
        public void SnapshotRoundTripRebuildsIdenticalFieldsWithoutSavingCaches()
        {
            var source = new SpatialFieldSystem(new WorldState(24, 24, 0x601UL));
            source.UpsertSource(new SpatialFieldSourceState(
                new StableId(200), SpatialSourceDefinitionIds.Park, new CellCoord(3, 4)));
            source.UpsertSource(new SpatialFieldSourceState(
                new StableId(201), SpatialSourceDefinitionIds.CrimePressure, new CellCoord(18, 17), false));
            source.RefreshDirty();
            var dto = source.ToDto();
            var checksum = source.ComputeChecksum();

            var restored = new SpatialFieldSystem(new WorldState(24, 24, 0x601UL));
            restored.RestoreFromDto(dto);
            Assert.That(restored.ComputeChecksum(), Is.EqualTo(checksum));
            Assert.That(restored.Sources.Select(item => item.Id.Value), Is.EqualTo(new ulong[] { 200, 201 }));
            Assert.That(dto.Sources.Length, Is.EqualTo(2));
        }

        [Test]
        public void RuntimeDerivesRoadNoiseButCapabilitySnapshotKeepsOnlyCustomSources()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 16,
                Height = 16,
                Seed = 0x602UL,
                InitialFunds = 10_000
            });
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 8),
                new CellCoord(7, 8))));
            var module = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            module.RefreshIfNeeded();

            Assert.That(module.ReadModel.Snapshot.Sources.Count(source => source.IsDerived), Is.EqualTo(8));
            Assert.That(module.Fields.Get(new CellCoord(3, 8), SpatialFieldKind.Noise), Is.GreaterThan(0));
            var customId = new StableId(0xF630000000000001UL);
            AssertApplied(module.Execute(new UpsertSpatialSourceCommand(
                customId,
                SpatialSourceDefinitionIds.Park,
                new CellCoord(4, 5))));

            var snapshot = module.Capture();
            Assert.That(snapshot.Sources, Has.Length.EqualTo(1));
            Assert.That(snapshot.Sources[0].Id, Is.EqualTo(customId.Value));
            Assert.That(module.ReadModel.Snapshot.Sources.Count(source => source.IsDerived), Is.EqualTo(8));
        }

        [Test]
        public void OverlayCellsAndLegendMatchApplicationQueryValues()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 10,
                Height = 8,
                Seed = 0x603UL
            });
            var module = SpatialFieldRuntimeRegistry.GetOrCreate(runtime);
            AssertApplied(module.Execute(new UpsertSpatialSourceCommand(
                new StableId(0xF630000000000002UL),
                SpatialSourceDefinitionIds.CrimePressure,
                new CellCoord(5, 4))));
            var overlay = module.Overlay.Build(SpatialFieldViewKind.Crime);
            var query = module.ReadModel.Get(new CellCoord(5, 4));
            var row = overlay.Cells.Single(cell => cell.Cell == new CellCoord(5, 4));

            Assert.That(row.Value, Is.EqualTo(query.Crime));
            Assert.That(row.IsAdverse, Is.True);
            Assert.That(row.Band, Is.EqualTo(SpatialFieldOverlayReadModel.BandFor(query.Crime)));
            Assert.That(row.Label, Is.Not.Empty);
            Assert.That(overlay.Legend, Has.Count.EqualTo(5));
            Assert.That(overlay.Legend[0].Minimum, Is.EqualTo(0));
            Assert.That(overlay.Legend[4].Maximum, Is.EqualTo(1000));
        }

        [Test]
        public void DirtyFixtureEvaluatesFewerCellsThanFullRebuildContract()
        {
            var system = new SpatialFieldSystem(new WorldState(256, 256, 16, 0x604UL));
            system.UpsertSource(new SpatialFieldSourceState(
                new StableId(300), SpatialSourceDefinitionIds.Park, new CellCoord(128, 128)));
            system.RefreshDirty();
            var dirtyCount = system.LastRefreshCellCount;
            Assert.That(dirtyCount, Is.EqualTo(81));
            Assert.That(dirtyCount, Is.LessThan(system.World.CellCount / 100));

            system.ForceFullRebuild();
            Assert.That(system.LastRefreshCellCount, Is.EqualTo(system.World.CellCount));
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }
    }
}
