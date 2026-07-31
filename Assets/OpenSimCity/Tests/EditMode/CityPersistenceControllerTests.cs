using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Platform.Persistence;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CityPersistenceControllerTests
    {
        private const long FixtureUtcTicks = 638900000000000000L;
        private const string FixtureGameVersion = "p4-03-fixture";
        private const ulong ExpectedCrossPlatformFixtureChecksum = 0x6039FE7670E3C298UL;

        [Test]
        public void ManualSaveOverwriteAndMonthlyAutosaveExposeSlotMetadataAtTickBoundary()
        {
            var storage = new MemorySaveStorage();
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var service = new CityPersistenceService(storage, codec);
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 8,
                Seed = 0x403UL,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(2042, 1)
            });
            var states = new List<CityPersistenceOperationState>();
            var controller = new CityPersistenceController(
                runtime,
                service,
                replacement => runtime = replacement,
                () => FixtureUtcTicks,
                FixtureGameVersion);
            controller.ViewModelChanged += viewModel => states.Add(viewModel.State);

            Assert.That(FindSlot(controller.ViewModel, CityPersistenceController.ManualSaveKey).HasSave, Is.False);
            Assert.That(controller.RequestManualSave(), Is.True);
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saving));
            controller.ProcessPendingOperation();

            var firstManual = FindSlot(controller.ViewModel, CityPersistenceController.ManualSaveKey);
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saved));
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.One));
            Assert.That(firstManual.IsMetadataAvailable, Is.True);
            Assert.That(firstManual.FormatVersion, Is.EqualTo(CitySnapshotFormat.CurrentVersion));
            Assert.That(firstManual.NextMonthYear, Is.EqualTo(2042));
            Assert.That(firstManual.NextMonth, Is.EqualTo(1));
            Assert.That(firstManual.CreatedUtcTicks, Is.EqualTo(FixtureUtcTicks));
            Assert.That(storage.WriteCount(CityPersistenceController.ManualSaveKey), Is.EqualTo(1));

            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, 4),
                new CellCoord(3, 4))).IsApplied, Is.True);
            Assert.That(controller.RequestManualSave(), Is.True);
            controller.ProcessPendingOperation();

            Assert.That(storage.WriteCount(CityPersistenceController.ManualSaveKey), Is.EqualTo(2));
            Assert.That(controller.ViewModel.Message, Does.Contain("덮어썼습니다"));

            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
            Assert.That(runtime.Clock.CurrentTick.Value, Is.EqualTo(1));
            Assert.That(runtime.MonthlyScheduler.CompletedMonths, Is.EqualTo(1));
            Assert.That(controller.HasPendingOperation, Is.True);
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saving));
            Assert.That(controller.ViewModel.ActiveSaveKey, Is.EqualTo(CityPersistenceController.AutosaveKey));
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));

            controller.ProcessPendingOperation();
            var autosave = FindSlot(controller.ViewModel, CityPersistenceController.AutosaveKey);
            Assert.That(autosave.IsMetadataAvailable, Is.True);
            Assert.That(autosave.CurrentTick, Is.EqualTo(1));
            Assert.That(autosave.NextMonthYear, Is.EqualTo(2042));
            Assert.That(autosave.NextMonth, Is.EqualTo(2));
            Assert.That(controller.ViewModel.Message, Does.Contain("자동 저장"));
            Assert.That(states, Does.Contain(CityPersistenceOperationState.Saving));
            Assert.That(states, Does.Contain(CityPersistenceOperationState.Saved));
        }

        [Test]
        public void LoadPreservesPublishedReasonsCommandsCachesAndNextPeriodResult()
        {
            var storage = new MemorySaveStorage();
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var service = new CityPersistenceService(storage, codec);
            var active = CreateGateRuntime();
            var savedRuntime = active;
            var controller = new CityPersistenceController(
                active,
                service,
                replacement => active = replacement,
                () => FixtureUtcTicks,
                FixtureGameVersion);

            var beforeStatus = JsonUtility.ToJson(savedRuntime.DevelopmentStatus.Snapshot);
            var beforeHud = JsonUtility.ToJson(savedRuntime.HudModel.Snapshot);
            var recoveryCell = new CellCoord(0, 4);
            var recoveryCommand = new PlacePowerProducerCommand(
                savedRuntime.PowerProducerIdFor(recoveryCell),
                savedRuntime.DefaultPowerPlantDefinitionId,
                recoveryCell);
            var beforePreview = savedRuntime.Preview(recoveryCommand);

            Assert.That(controller.RequestManualSave(), Is.True);
            controller.ProcessPendingOperation();
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saved));
            var independentBaseline = service.Load(CityPersistenceController.ManualSaveKey).Runtime;

            Assert.That(savedRuntime.Execute(recoveryCommand).IsApplied, Is.True);
            Assert.That(controller.RequestLoad(CityPersistenceController.ManualSaveKey), Is.True);
            Assert.That(active, Is.SameAs(savedRuntime), "A pending load must not partially replace the city.");
            Assert.That(active.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            controller.ProcessPendingOperation();

            var loaded = active;
            Assert.That(loaded, Is.Not.SameAs(savedRuntime));
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Loaded));
            Assert.That(JsonUtility.ToJson(loaded.DevelopmentStatus.Snapshot), Is.EqualTo(beforeStatus));
            Assert.That(JsonUtility.ToJson(loaded.HudModel.Snapshot), Is.EqualTo(beforeHud));
            Assert.That(loaded.RoadGraph.Cells, Is.EqualTo(independentBaseline.RoadGraph.Cells));
            Assert.That(loaded.Parcels.Parcels.Select(ParcelKey),
                Is.EqualTo(independentBaseline.Parcels.Parcels.Select(ParcelKey)));
            Assert.That(loaded.Power.ComputeChecksum(), Is.EqualTo(independentBaseline.Power.ComputeChecksum()));

            var afterPreview = loaded.Preview(new PlacePowerProducerCommand(
                loaded.PowerProducerIdFor(recoveryCell),
                loaded.DefaultPowerPlantDefinitionId,
                recoveryCell));
            Assert.That(afterPreview.IsValid, Is.EqualTo(beforePreview.IsValid));
            Assert.That(afterPreview.Cost, Is.EqualTo(beforePreview.Cost));
            Assert.That(afterPreview.Message, Is.EqualTo(beforePreview.Message));

            controller.Dispose();
            ApplyRecoveryAndAdvance(independentBaseline, recoveryCell);
            ApplyRecoveryAndAdvance(loaded, recoveryCell);
            var expectedNextPeriod = codec.Encode(CitySnapshotSerializer.Capture(
                independentBaseline,
                FixtureGameVersion,
                FixtureUtcTicks));
            var actualNextPeriod = codec.Encode(CitySnapshotSerializer.Capture(
                loaded,
                FixtureGameVersion,
                FixtureUtcTicks));
            Assert.That(actualNextPeriod.Payload, Is.EqualTo(expectedNextPeriod.Payload));
        }

        [Test]
        public void CorruptAndUnknownVersionLoadsKeepCurrentCityAndProvideSafeRecoveryMessages()
        {
            AssertSafeLoadFailure(
                new byte[] { 0xFF, 0x00, 0x81 },
                CityPersistenceController.ManualSaveKey,
                "손상");

            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var unsupported = CitySnapshotSerializer.Capture(CreateGateRuntime());
            unsupported.Header.FormatVersion = CitySnapshotFormat.CurrentVersion + 1;
            AssertSafeLoadFailure(
                codec.Encode(unsupported).Payload,
                CityPersistenceController.AutosaveKey,
                "지원하지 않는 저장 버전");
        }

        [Test]
        public void VersionOneCrossPlatformGoldenSaveMigratesAndContinuesDeterministically()
        {
            var committedFixture = File.ReadAllBytes(FixturePath("p4-v1-roundtrip-windows.json"));
            Assert.That(committedFixture[committedFixture.Length - 1], Is.EqualTo((byte)'\n'));
            var versionOnePayload = committedFixture.Take(committedFixture.Length - 1).ToArray();
            Assert.That(Fnv1a64(versionOnePayload), Is.EqualTo(ExpectedCrossPlatformFixtureChecksum));

            var storage = new MemorySaveStorage();
            Assert.That(storage.WriteAtomically(
                CityPersistenceController.ManualSaveKey,
                versionOnePayload).IsSuccess, Is.True);
            var codec = new UnityJsonCitySnapshotPayloadCodec();
            var service = new CityPersistenceService(storage, codec);

            var load = service.Load(CityPersistenceController.ManualSaveKey);

            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            Assert.That(load.WasMigrated, Is.True);
            Assert.That(load.SourceFormatVersion, Is.EqualTo(1));
            Assert.That(load.Runtime, Is.Not.Null);
            Assert.That(load.Runtime.Roads.Cells
                .Where(cell => cell.IsOccupied)
                .All(cell => cell.TravelDirection == RoadTravelDirection.Bidirectional), Is.True);

            var baseline = CreateGateRuntime();
            var migratedPayload = codec.Encode(CitySnapshotSerializer.Capture(
                load.Runtime,
                FixtureGameVersion,
                FixtureUtcTicks)).Payload;
            var baselinePayload = codec.Encode(CitySnapshotSerializer.Capture(
                baseline,
                FixtureGameVersion,
                FixtureUtcTicks)).Payload;
            Assert.That(migratedPayload, Is.EqualTo(baselinePayload));

            var recoveryCell = new CellCoord(0, 4);
            ApplyRecoveryAndAdvance(baseline, recoveryCell);
            ApplyRecoveryAndAdvance(load.Runtime, recoveryCell);
            var expectedNextMonth = codec.Encode(CitySnapshotSerializer.Capture(
                baseline,
                FixtureGameVersion,
                FixtureUtcTicks)).Payload;
            var migratedNextMonth = codec.Encode(CitySnapshotSerializer.Capture(
                load.Runtime,
                FixtureGameVersion,
                FixtureUtcTicks)).Payload;

            TestContext.WriteLine(
                $"P5-04A v1 migration golden: sourceChecksum={Fnv1a64(versionOnePayload):X16}; " +
                $"migratedChecksum={Fnv1a64(migratedPayload):X16}; " +
                $"nextMonthChecksum={Fnv1a64(migratedNextMonth):X16}; " +
                $"Unity={UnityEngine.Application.unityVersion}; platform={UnityEngine.Application.platform}");
            Assert.That(migratedNextMonth, Is.EqualTo(expectedNextMonth));
        }

        private static void AssertSafeLoadFailure(
            byte[] payload,
            string saveKey,
            string expectedMessage)
        {
            var storage = new MemorySaveStorage();
            Assert.That(storage.WriteAtomically(saveKey, payload).IsSuccess, Is.True);
            var runtime = CreateGateRuntime();
            var replacementCount = 0;
            var controller = new CityPersistenceController(
                runtime,
                new CityPersistenceService(storage, new UnityJsonCitySnapshotPayloadCodec()),
                replacement => replacementCount++,
                () => FixtureUtcTicks,
                FixtureGameVersion);

            Assert.That(FindSlot(controller.ViewModel, saveKey).HasSave, Is.True);
            Assert.That(controller.RequestLoad(saveKey), Is.True);
            controller.ProcessPendingOperation();

            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Error));
            Assert.That(controller.ViewModel.Message, Does.Contain(expectedMessage));
            Assert.That(controller.ViewModel.Message, Does.Contain("현재 도시"));
            Assert.That(controller.ViewModel.CanRetry, Is.True);
            Assert.That(controller.Runtime, Is.SameAs(runtime));
            Assert.That(replacementCount, Is.Zero);
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.One));
            controller.DismissFeedback();
            Assert.That(controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Idle));
        }

        private static CityRuntime CreateGateRuntime()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 16,
                Height = 10,
                Seed = 0x403UL,
                InitialFunds = 100_000,
                TicksPerMonth = 1,
                FirstSimulationMonth = new SimulationDate(2043, 1)
            });

            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
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
            runtime.Parcels.RefreshIfNeeded();
            var commercialParcel = runtime.Parcels.Parcels.First(
                parcel => parcel.Anchor == new CellCoord(6, 3));
            runtime.Buildings.Create(
                new StableId(0x4031UL),
                runtime.BuildingDefinitions.Definitions[1].Id,
                commercialParcel.Id,
                commercialParcel.Anchor,
                occupancy: 0,
                filledJobs: 4);
            runtime.RefreshReadModels();
            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
            Assert.That(runtime.DevelopmentStatus.HasSnapshot, Is.True);
            Assert.That(
                runtime.DevelopmentStatus.Snapshot.TopBlockReasons.Any(
                    reason => reason.Reason == DevelopmentBlockReason.NoPowerCapacity),
                Is.True);
            return runtime;
        }

        private static void ApplyRecoveryAndAdvance(CityRuntime runtime, CellCoord recoveryCell)
        {
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(recoveryCell),
                runtime.DefaultPowerPlantDefinitionId,
                recoveryCell)));
            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
        }

        private static CitySaveSlotViewModel FindSlot(
            CityPersistenceViewModel viewModel,
            string saveKey)
        {
            return viewModel.Slots.Single(slot => slot.SaveKey == saveKey);
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

        private static string ParcelKey(ParcelState parcel)
        {
            return $"{parcel.Id.Value}:{parcel.Anchor.X}:{parcel.Anchor.Y}:{parcel.Width}:{parcel.Height}";
        }

        private static void AssertApplied(CommandExecutionResult result)
        {
            Assert.That(result.IsApplied, Is.True, result.Message);
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

        private sealed class MemorySaveStorage : ICitySaveStorage
        {
            private readonly Dictionary<string, byte[]> _payloads =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _writeCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public CitySaveStorageReadResult Read(string saveKey)
            {
                return _payloads.TryGetValue(saveKey, out var payload)
                    ? CitySaveStorageReadResult.Success(payload)
                    : CitySaveStorageReadResult.Failure(CitySaveStorageError.NotFound);
            }

            public CitySaveStorageWriteResult WriteAtomically(string saveKey, byte[] payload)
            {
                var hasBackup = _payloads.ContainsKey(saveKey);
                _payloads[saveKey] = (byte[])payload.Clone();
                _writeCounts.TryGetValue(saveKey, out var count);
                _writeCounts[saveKey] = count + 1;
                return CitySaveStorageWriteResult.Success(hasBackup);
            }

            public int WriteCount(string saveKey)
            {
                return _writeCounts.TryGetValue(saveKey, out var count) ? count : 0;
            }
        }
    }
}
