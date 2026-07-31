using System;
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
                RoadHierarchyDefaults.SmallRoadDefinitionId,
                CellCoord.Zero,
                new CellCoord(3, 0))));
            AssertApplied(runtime.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                CellCoord.Zero,
                new CellCoord(3, 0))));
            var expectedChecksum = runtime.Roads.ComputeChecksum();
            var expectedSequence = runtime.NextCommandSequence;

            var snapshot = CitySnapshotSerializer.Capture(runtime, "p5-04a", 1234L);
            Assert.That(snapshot.Header.FormatVersion, Is.EqualTo(2));
            Assert.That(snapshot.Roads.Cells, Has.Length.EqualTo(4));
            Assert.That(snapshot.Roads.Cells[0].DefinitionId,
                Is.EqualTo(RoadHierarchyDefaults.AvenueDefinitionId.Value));
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
                    Is.EqualTo(RoadHierarchyDefaults.AvenueDefinitionId));
                Assert.That(restored.Roads.GetTravelDirection(cell),
                    Is.EqualTo(RoadTravelDirection.East));
            }
        }

        [Test]
        public void VersionOnePayloadMigratesRoadsToExplicitBidirectionalState()
        {
            var options = RuntimeOptions();
            var runtime = new CityRuntime(options);
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadHierarchyDefaults.StreetDefinitionId,
                CellCoord.Zero,
                new CellCoord(2, 0))));

            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var v1 = CitySnapshotSerializer.Capture(runtime, "legacy", 500L);
            v1.Header.FormatVersion = 1;
            for (var index = 0; index < v1.Roads.Cells.Length; index++)
                v1.Roads.Cells[index].TravelDirection = (int)RoadTravelDirection.West;

            var encoded = codec.Encode(v1);
            Assert.That(encoded.IsSuccess, Is.True);
            Assert.That(encoded.Payload, Is.Not.Null);
            var sourcePayload = (byte[])encoded.Payload.Clone();
            var storage = new MemorySaveStorage(sourcePayload);
            var service = new CityPersistenceService(
                storage,
                codec,
                new ICitySnapshotMigrationStep[]
                {
                    new CitySnapshotV1ToV2Migration(codec)
                });

            var loaded = service.Load("legacy", options);

            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());
            Assert.That(loaded.WasMigrated, Is.True);
            Assert.That(loaded.SourceFormatVersion, Is.EqualTo(1));
            Assert.That(storage.Payload, Is.EqualTo(sourcePayload));
            Assert.That(CitySnapshotFormat.CurrentVersion, Is.EqualTo(2));
            for (var x = 0; x < 3; x++)
            {
                var cell = new CellCoord(x, 0);
                Assert.That(loaded.Runtime.Roads.GetRoadDefinitionId(cell),
                    Is.EqualTo(RoadHierarchyDefaults.StreetDefinitionId));
                Assert.That(loaded.Runtime.Roads.GetTravelDirection(cell),
                    Is.EqualTo(RoadTravelDirection.Bidirectional));
            }
        }

        [Test]
        public void SnapshotRejectsUndefinedRoadDirection()
        {
            var runtime = new CityRuntime(RuntimeOptions());
            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                RoadHierarchyDefaults.StreetDefinitionId,
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

        private sealed class MemorySaveStorage : ICitySaveStorage
        {
            private byte[] _payload;

            public MemorySaveStorage(byte[] payload)
            {
                _payload = payload == null
                    ? throw new ArgumentNullException(nameof(payload))
                    : (byte[])payload.Clone();
            }

            public byte[] Payload => (byte[])_payload.Clone();

            public CitySaveStorageReadResult Read(string saveKey)
            {
                return CitySaveStorageReadResult.Success(_payload);
            }

            public CitySaveStorageWriteResult WriteAtomically(string saveKey, byte[] payload)
            {
                if (payload == null)
                    return CitySaveStorageWriteResult.Failure(CitySaveStorageError.WriteFailed);

                _payload = (byte[])payload.Clone();
                return CitySaveStorageWriteResult.Success(hasBackup: false);
            }
        }
    }
}
