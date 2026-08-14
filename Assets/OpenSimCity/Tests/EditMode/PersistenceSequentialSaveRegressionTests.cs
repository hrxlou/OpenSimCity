using System;
using System.IO;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Platform.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PersistenceSequentialSaveRegressionTests
    {
        private const string SaveKey = "audit-sequential-city";
        private const string FixtureGameVersion = "audit-sequential-fixture";
        private const long FirstUtcTicks = 638900000000000001L;
        private const long SecondUtcTicks = 638900000000000002L;
        private const long ThirdUtcTicks = 638900000000000003L;

        private string _rootDirectory;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "OpenSimCity",
                "Audit",
                "PersistenceSequential",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        [Test]
        public void SequentialSavesSurviveServiceRestartAndBackupTracksImmediatePreviousCommit()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0xA0B0C0UL
            });
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            var service = new CityPersistenceService(storage, codec);

            var first = service.Save(SaveKey, runtime, FixtureGameVersion, FirstUtcTicks);
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.HasBackup, Is.False);

            AssertRoadPlacement(runtime, new CellCoord(0, 4), new CellCoord(2, 4));
            var second = service.Save(SaveKey, runtime, FixtureGameVersion, SecondUtcTicks);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.HasBackup, Is.True);
            var secondPayload = storage.Read(SaveKey).Payload;

            AssertRoadPlacement(runtime, new CellCoord(3, 4), new CellCoord(5, 4));
            var expected = codec.Encode(CitySnapshotSerializer.Capture(
                runtime,
                FixtureGameVersion,
                ThirdUtcTicks));
            Assert.That(expected.IsSuccess, Is.True);

            var third = service.Save(SaveKey, runtime, FixtureGameVersion, ThirdUtcTicks);
            Assert.That(third.IsSuccess, Is.True);
            Assert.That(third.HasBackup, Is.True);

            var destinationPath = Path.Combine(
                _rootDirectory,
                SaveKey + AtomicFileCitySaveStorage.SaveFileExtension);
            var backupPath = destinationPath + AtomicFileCitySaveStorage.BackupFileSuffix;
            var temporaryPath = destinationPath + AtomicFileCitySaveStorage.TemporaryFileSuffix;
            Assert.That(File.ReadAllBytes(backupPath), Is.EqualTo(secondPayload));
            Assert.That(File.Exists(temporaryPath), Is.False);

            var restartedService = new CityPersistenceService(
                new AtomicFileCitySaveStorage(_rootDirectory),
                codec);
            var loaded = restartedService.Load(SaveKey);
            Assert.That(loaded.IsSuccess, Is.True);

            var actual = codec.Encode(CitySnapshotSerializer.Capture(
                loaded.Runtime,
                FixtureGameVersion,
                ThirdUtcTicks));
            Assert.That(actual.IsSuccess, Is.True);
            Assert.That(actual.Payload, Is.EqualTo(expected.Payload));
        }

        private static void AssertRoadPlacement(CityRuntime runtime, CellCoord start, CellCoord end)
        {
            var execution = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                start,
                end));
            Assert.That(execution.IsApplied, Is.True, execution.Message);
        }
    }
}
