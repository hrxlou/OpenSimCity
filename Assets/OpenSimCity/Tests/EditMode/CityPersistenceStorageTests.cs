using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Platform.Persistence;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CityPersistenceStorageTests
    {
        private const string SaveKey = "p4-02-city";
        private const long FixtureUtcTicks = 638900000000000000L;
        private const string FixtureGameVersion = "p4-02-fixture";

        private string _rootDirectory;
        private UnityJsonCitySnapshotPayloadCodec _codec;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "OpenSimCity",
                "P4-02",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDirectory);
            _codec = new UnityJsonCitySnapshotPayloadCodec();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        [Test]
        public void SaveAndLoadRoundTripThroughApplicationPorts()
        {
            var runtime = CreateRuntime();
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            var service = new CityPersistenceService(storage, _codec);
            var expected = _codec.Encode(CitySnapshotSerializer.Capture(
                runtime,
                FixtureGameVersion,
                FixtureUtcTicks));

            var saved = service.Save(
                SaveKey,
                runtime,
                FixtureGameVersion,
                FixtureUtcTicks);
            var loaded = service.Load(SaveKey);
            var actual = _codec.Encode(CitySnapshotSerializer.Capture(
                loaded.Runtime,
                FixtureGameVersion,
                FixtureUtcTicks));

            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(saved.HasBackup, Is.False);
            Assert.That(loaded.IsSuccess, Is.True);
            Assert.That(loaded.WasMigrated, Is.False);
            Assert.That(loaded.SourceFormatVersion, Is.EqualTo(CitySnapshotFormat.CurrentVersion));
            Assert.That(actual.Payload, Is.EqualTo(expected.Payload));
            Assert.That(File.Exists(DestinationPath()), Is.True);
            Assert.That(File.Exists(TemporaryPath()), Is.False);
            TestContext.WriteLine(
                $"P4-02 storage root={_rootDirectory}; payloadBytes={actual.Payload.Length}; " +
                $"checksum={Fnv1a64(actual.Payload):X16}; platform={UnityEngine.Application.platform}");
        }

        [Test]
        public void SuccessfulReplaceKeepsPreviousSaveAsBackupAndCleansTemporaryFile()
        {
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            var first = Encoding.UTF8.GetBytes("first-save");
            var second = Encoding.UTF8.GetBytes("second-save");

            Assert.That(storage.WriteAtomically(SaveKey, first).IsSuccess, Is.True);
            var replaced = storage.WriteAtomically(SaveKey, second);

            Assert.That(replaced.IsSuccess, Is.True);
            Assert.That(replaced.HasBackup, Is.True);
            Assert.That(File.ReadAllBytes(DestinationPath()), Is.EqualTo(second));
            Assert.That(File.ReadAllBytes(BackupPath()), Is.EqualTo(first));
            Assert.That(File.Exists(TemporaryPath()), Is.False);
        }

        [TestCase(AtomicFileSaveStage.BeforeWrite, CitySaveStorageError.WriteFailed)]
        [TestCase(AtomicFileSaveStage.BeforeFlush, CitySaveStorageError.FlushFailed)]
        [TestCase(AtomicFileSaveStage.BeforeReplace, CitySaveStorageError.ReplaceFailed)]
        public void ForcedFailurePreservesExistingSaveAndCleansTemporaryFile(
            AtomicFileSaveStage stage,
            CitySaveStorageError expectedError)
        {
            var original = Encoding.UTF8.GetBytes("known-good");
            var baselineStorage = new AtomicFileCitySaveStorage(_rootDirectory);
            Assert.That(baselineStorage.WriteAtomically(SaveKey, original).IsSuccess, Is.True);

            var failingStorage = new AtomicFileCitySaveStorage(
                _rootDirectory,
                new ThrowingFailureInjector(stage));
            var result = failingStorage.WriteAtomically(
                SaveKey,
                Encoding.UTF8.GetBytes("replacement"));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(expectedError));
            Assert.That(File.ReadAllBytes(DestinationPath()), Is.EqualTo(original));
            Assert.That(File.Exists(TemporaryPath()), Is.False);
            Assert.That(File.Exists(BackupPath()), Is.False);
        }

        [Test]
        public void WindowsExclusiveLockMapsToApplicationLockedResult()
        {
            if (UnityEngine.Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Windows sharing semantics are verified on the Windows device.");

            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            var service = new CityPersistenceService(storage, _codec);
            Assert.That(
                service.Save(
                    SaveKey,
                    CreateRuntime(),
                    FixtureGameVersion,
                    FixtureUtcTicks).IsSuccess,
                Is.True);

            using (new FileStream(
                       DestinationPath(),
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                var result = service.Load(SaveKey);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.EqualTo(CityPersistenceError.Locked));
            }
        }

        [Test]
        public void CorruptBinaryAndTruncatedJsonReturnErrorsWithoutChangingBytes()
        {
            var corruptPayloads = new[]
            {
                new byte[] { 0xFF, 0x00, 0x81, 0x7F },
                Encoding.UTF8.GetBytes(
                    "{\"Header\":{\"Magic\":\"OpenSimCity.CitySnapshot\",\"FormatVersion\":1}")
            };

            for (var index = 0; index < corruptPayloads.Length; index++)
            {
                var key = SaveKey + "-" + index;
                var storage = new AtomicFileCitySaveStorage(_rootDirectory);
                var service = new CityPersistenceService(storage, _codec);
                Assert.That(storage.WriteAtomically(key, corruptPayloads[index]).IsSuccess, Is.True);
                var before = storage.Read(key).Payload;

                var result = service.Load(key);
                var after = storage.Read(key).Payload;

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.EqualTo(CityPersistenceError.CorruptData));
                Assert.That(after, Is.EqualTo(before));
            }
        }

        [Test]
        public void UnknownVersionIsRejectedWithoutGuessingAMigration()
        {
            var snapshot = CitySnapshotSerializer.Capture(CreateRuntime());
            snapshot.Header.FormatVersion = CitySnapshotFormat.CurrentVersion + 1;
            var payload = _codec.Encode(snapshot);
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            Assert.That(storage.WriteAtomically(SaveKey, payload.Payload).IsSuccess, Is.True);

            var result = new CityPersistenceService(storage, _codec).Load(SaveKey);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(CityPersistenceError.UnsupportedVersion));
            Assert.That(
                result.SourceFormatVersion,
                Is.EqualTo(CitySnapshotFormat.CurrentVersion + 1));
        }

        [Test]
        public void CodecPreflightPreventsCorruptPayloadFromReplacingKnownGoodSave()
        {
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);
            var goodService = new CityPersistenceService(storage, _codec);
            Assert.That(
                goodService.Save(
                    SaveKey,
                    CreateRuntime(),
                    FixtureGameVersion,
                    FixtureUtcTicks).IsSuccess,
                Is.True);
            var before = storage.Read(SaveKey).Payload;

            var corruptingService = new CityPersistenceService(
                storage,
                new CorruptingPayloadCodec());
            var result = corruptingService.Save(
                SaveKey,
                CreateRuntime(),
                FixtureGameVersion,
                FixtureUtcTicks);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(CityPersistenceError.EncodingFailed));
            Assert.That(storage.Read(SaveKey).Payload, Is.EqualTo(before));
            Assert.That(File.Exists(TemporaryPath()), Is.False);
        }

        [Test]
        public void V1ToVNextFixtureMigrationMatchesGoldenWithoutMutatingSource()
        {
            var source = File.ReadAllBytes(FixturePath("p4-v1-minimal.json"));
            var sourceBefore = (byte[])source.Clone();
            var golden = File.ReadAllBytes(FixturePath("p4-vnext-minimal.json"));
            var dispatcher = new CitySnapshotMigrationDispatcher(
                _codec,
                targetVersion: 2,
                new ICitySnapshotMigrationStep[] { new V1ToVNextFixtureMigration() });

            var result = dispatcher.Dispatch(source);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.SourceVersion, Is.EqualTo(1));
            Assert.That(result.TargetVersion, Is.EqualTo(2));
            Assert.That(result.WasMigrated, Is.True);
            Assert.That(result.Payload, Is.EqualTo(golden));
            Assert.That(source, Is.EqualTo(sourceBefore));
            Assert.That(ReferenceEquals(source, result.Payload), Is.False);
            Assert.That(_codec.ReadFormatVersion(result.Payload).FormatVersion, Is.EqualTo(2));
            TestContext.WriteLine(
                $"P4-02 migration fixture: sourceBytes={source.Length}; " +
                $"sourceChecksum={Fnv1a64(source):X16}; targetBytes={golden.Length}; " +
                $"targetChecksum={Fnv1a64(golden):X16}");
        }

        [Test]
        public void PathsAndPlatformTypesDoNotLeakIntoApplicationPersistenceContract()
        {
            var applicationAssembly = typeof(ICitySaveStorage).Assembly;
            var referenceNames = applicationAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var storage = new AtomicFileCitySaveStorage(_rootDirectory);

            Assert.That(referenceNames, Does.Not.Contain("OpenSimCity.Platform"));
            Assert.That(referenceNames.Any(name => name.StartsWith("UnityEngine")), Is.False);
            Assert.That(
                storage.WriteAtomically("../outside", new byte[] { 1 }).Error,
                Is.EqualTo(CitySaveStorageError.InvalidSaveKey));
            Assert.That(Directory.GetFiles(_rootDirectory), Is.Empty);
        }

        private CityRuntime CreateRuntime()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x402UL
            });
            var execution = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 4),
                new CellCoord(4, 4)));
            Assert.That(execution.IsApplied, Is.True, execution.Message);
            return runtime;
        }

        private string DestinationPath(string saveKey = SaveKey)
        {
            return Path.Combine(
                _rootDirectory,
                saveKey + AtomicFileCitySaveStorage.SaveFileExtension);
        }

        private string TemporaryPath(string saveKey = SaveKey)
        {
            return DestinationPath(saveKey) + AtomicFileCitySaveStorage.TemporaryFileSuffix;
        }

        private string BackupPath(string saveKey = SaveKey)
        {
            return DestinationPath(saveKey) + AtomicFileCitySaveStorage.BackupFileSuffix;
        }

        private static string FixturePath(string fileName)
        {
            return Path.Combine(
                UnityEngine.Application.dataPath,
                "OpenSimCity",
                "Tests",
                "Fixtures",
                "Persistence",
                fileName);
        }

        private static ulong Fnv1a64(byte[] bytes)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= prime;
            }

            return hash;
        }

        private sealed class ThrowingFailureInjector : IAtomicFileSaveFailureInjector
        {
            private readonly AtomicFileSaveStage _stage;

            public ThrowingFailureInjector(AtomicFileSaveStage stage)
            {
                _stage = stage;
            }

            public void ThrowIfRequested(AtomicFileSaveStage stage)
            {
                if (stage == _stage)
                    throw new IOException("Injected P4-02 failure.");
            }
        }

        private sealed class CorruptingPayloadCodec : ICitySnapshotPayloadCodec
        {
            public SnapshotPayloadVersionResult ReadFormatVersion(byte[] payload)
            {
                return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
            }

            public SnapshotPayloadEncodeResult Encode(CitySnapshotDto snapshot)
            {
                return SnapshotPayloadEncodeResult.Success(new byte[] { 0xFF, 0x00, 0x81 });
            }

            public SnapshotPayloadDecodeResult Decode(byte[] payload)
            {
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);
            }
        }

        private sealed class V1ToVNextFixtureMigration : ICitySnapshotMigrationStep
        {
            public int SourceVersion => 1;
            public int TargetVersion => 2;

            public SnapshotMigrationStepResult Migrate(byte[] sourcePayload)
            {
                var sourceJson = new UTF8Encoding(false, true).GetString(sourcePayload);
                var source = JsonUtility.FromJson<MigrationFixtureV1Dto>(sourceJson);
                if (source == null || source.Header == null || source.Fixture == null)
                    return SnapshotMigrationStepResult.Failure();

                var migrated = new MigrationFixtureVNextDto
                {
                    Header = new MigrationFixtureHeaderDto
                    {
                        Magic = source.Header.Magic,
                        FormatVersion = TargetVersion
                    },
                    Fixture = new MigrationFixtureVNextPayloadDto
                    {
                        Id = source.Fixture.Id,
                        Value = source.Fixture.Value,
                        MigratedFromVersion = SourceVersion
                    }
                };
                return SnapshotMigrationStepResult.Success(
                    new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(migrated) + "\n"));
            }
        }

        [Serializable]
        private sealed class MigrationFixtureHeaderDto
        {
            public string Magic;
            public int FormatVersion;
        }

        [Serializable]
        private sealed class MigrationFixtureV1PayloadDto
        {
            public string Id;
            public int Value;
        }

        [Serializable]
        private sealed class MigrationFixtureVNextPayloadDto
        {
            public string Id;
            public int Value;
            public int MigratedFromVersion;
        }

        [Serializable]
        private sealed class MigrationFixtureV1Dto
        {
            public MigrationFixtureHeaderDto Header;
            public MigrationFixtureV1PayloadDto Fixture;
        }

        [Serializable]
        private sealed class MigrationFixtureVNextDto
        {
            public MigrationFixtureHeaderDto Header;
            public MigrationFixtureVNextPayloadDto Fixture;
        }
    }
}
