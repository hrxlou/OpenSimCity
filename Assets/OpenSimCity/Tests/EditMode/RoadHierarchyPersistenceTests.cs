using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Platform.Persistence;
using OpenSimCity.Simulation.Roads;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadHierarchyPersistenceTests
    {
        [Test]
        public void SnapshotV2RoundTripPreservesDefinitionDirectionAndCommandSequence()
        {
            var options = RuntimeOptions();
            var runtime = new CityRuntime(options);
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                CityRuntimeOptions.SmallRoadDefinitionId,
                CellCoord.Zero,
                new CellCoord(3, 0))));
            AssertApplied(runtime.Execute(new UpgradeRoadCommand(
                CityRuntimeOptions.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                CellCoord.Zero,
                new CellCoord(3, 0))));
            var expectedChecksum = runtime.Roads.ComputeChecksum();
            var expectedSequence = runtime.NextCommandSequence;

            var snapshot = CitySnapshotSerializer.Capture(runtime, "p5-04a", 1234L);
            Assert.That(snapshot.Header.FormatVersion, Is.EqualTo(2));
            Assert.That(snapshot.Roads.Cells, Has.Length.EqualTo(4));
            Assert.That(snapshot.Roads.Cells[0].DefinitionId,
                Is.EqualTo(CityRuntimeOptions.AvenueDefinitionId.Value));
            Assert.That(snapshot.Roads.Cells[0].TravelDirection,
                Is.EqualTo((int)RoadTravelDirection.East));

            var json = JsonUtility.ToJson(snapshot);
            var restoredDto = JsonUtility.FromJson<CitySnapshotDto>(json);
            var restored = CitySnapshotSerializer.Restore(restoredDto, options);

            Assert.That(restored.Roads.ComputeChecksum(), Is.EqualTo(expectedChecksum));
            Assert.That(restored.NextCommandSequence, Is.EqualTo(expectedSequence));
            for (var x = 0; x < 4; x++)
            {
                var cell = new CellCoord(x, 0);
                Assert.That(restored.Roads.GetRoadDefinitionId(cell),
                    Is.EqualTo(CityRuntimeOptions.AvenueDefinitionId));
                Assert.That(restored.Roads.GetTravelDirection(cell),
                    Is.EqualTo(RoadTravelDirection.East));
            }
        }

        [Test]
        public void VersionOneEnvelopeMigratesRoadsToExplicitBidirectionalState()
        {
            var options = RuntimeOptions();
            var runtime = new CityRuntime(options);
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                CityRuntimeOptions.StreetDefinitionId,
                CellCoord.Zero,
                new CellCoord(2, 0))));

            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var v1 = CitySnapshotSerializer.Capture(runtime, "legacy", 500L);
            v1.Header.FormatVersion = 1;
            for (var index = 0; index < v1.Roads.Cells.Length; index++)
                v1.Roads.Cells[index].TravelDirection = (int)RoadTravelDirection.West;
            var payload = codec.Encode(v1);
            var envelope = CitySnapshotEnvelope.Encode(1, "legacy", 500L, payload);
            var service = new CityPersistenceService(
                new NullSnapshotStore(),
                codec,
                options,
                "legacy",
                () => 500L);

            var restored = service.LoadBytes(envelope);

            Assert.That(CitySnapshotFormat.CurrentVersion, Is.EqualTo(2));
            for (var x = 0; x < 3; x++)
            {
                var cell = new CellCoord(x, 0);
                Assert.That(restored.Roads.GetRoadDefinitionId(cell),
                    Is.EqualTo(CityRuntimeOptions.StreetDefinitionId));
                Assert.That(restored.Roads.GetTravelDirection(cell),
                    Is.EqualTo(RoadTravelDirection.Bidirectional));
            }
        }

        [Test]
        public void SnapshotRejectsUndefinedRoadDirection()
        {
            var runtime = new CityRuntime(RuntimeOptions());
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                CityRuntimeOptions.StreetDefinitionId,
                CellCoord.Zero,
                new CellCoord(1, 0))));
            var snapshot = CitySnapshotSerializer.Capture(runtime);
            snapshot.Roads.Cells[0].TravelDirection = 99;

            var error = Assert.Throws<CitySnapshotValidationException>(() =>
                CitySnapshotSerializer.Restore(snapshot, RuntimeOptions()));
            Assert.That(error.FieldPath, Is.EqualTo("Roads.Cells[0].TravelDirection"));
        }

        private static CityRuntimeOptions RuntimeOptions()
        {
            return new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = 10_000,
                RoadBuildCost = 10,
                RoadMonthlyMaintenanceCost = 2
            };
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
        }

        private sealed class NullSnapshotStore : ICitySnapshotStore
        {
            public void WriteAtomic(string slotId, byte[] data, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public byte[] Read(string slotId, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public IReadOnlyList<CitySnapshotSlotInfo> ListSlots()
            {
                return Array.Empty<CitySnapshotSlotInfo>();
            }
        }
    }
}
