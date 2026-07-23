using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CitySnapshotSerializationTests
    {
        private const long FixtureUtcTicks = 638900000000000000L;
        private const string FixtureGameVersion = "p4-01-fixture";
        private const ulong ExpectedFixtureJsonChecksum = 0x262B9801635F35BDUL;

        [Test]
        public void GrowingCityRoundTripRestoresAuthoritativeStateCachesAndContinuation()
        {
            var original = CreateGrowingRuntime();
            var snapshot = CitySnapshotSerializer.Capture(
                original,
                FixtureGameVersion,
                FixtureUtcTicks);
            var json = JsonUtility.ToJson(snapshot);
            var transported = JsonUtility.FromJson<CitySnapshotDto>(json);
            var restored = CitySnapshotSerializer.Restore(transported);
            var restoredJson = JsonUtility.ToJson(CitySnapshotSerializer.Capture(
                restored,
                FixtureGameVersion,
                FixtureUtcTicks));

            Assert.That(restoredJson, Is.EqualTo(json));
            Assert.That(restored.RoadGraph.Cells, Is.EqualTo(original.RoadGraph.Cells));
            Assert.That(restored.Power.ComputeChecksum(), Is.EqualTo(original.Power.ComputeChecksum()));
            Assert.That(restored.Parcels.Parcels.Select(ToParcelKey),
                Is.EqualTo(original.Parcels.Parcels.Select(ToParcelKey)));
            Assert.That(restored.Clock.CurrentTick, Is.EqualTo(original.Clock.CurrentTick));
            Assert.That(restored.Clock.AccumulatedSeconds, Is.EqualTo(original.Clock.AccumulatedSeconds));
            Assert.That(restored.NextCommandSequence, Is.EqualTo(original.NextCommandSequence));
            Assert.That(restored.MonthlyScheduler.NextRunTick, Is.EqualTo(original.MonthlyScheduler.NextRunTick));
            Assert.That(restored.GrowthSystem.RandomState, Is.EqualTo(original.GrowthSystem.RandomState));

            for (var step = 0; step < 12; step++)
            {
                Assert.That(restored.AdvanceSimulation(0.05d), Is.EqualTo(original.AdvanceSimulation(0.05d)));
            }

            var continuedOriginal = JsonUtility.ToJson(CitySnapshotSerializer.Capture(
                original,
                FixtureGameVersion,
                FixtureUtcTicks));
            var continuedRestored = JsonUtility.ToJson(CitySnapshotSerializer.Capture(
                restored,
                FixtureGameVersion,
                FixtureUtcTicks));
            Assert.That(continuedRestored, Is.EqualTo(continuedOriginal));

            TestContext.WriteLine(
                $"P4-01 growing city JSON UTF-8 bytes: {Encoding.UTF8.GetByteCount(json)}; " +
                $"roads={snapshot.Roads.Cells.Length}; zones={snapshot.Zones.Cells.Length}; " +
                $"parcels={snapshot.Parcels.Parcels.Length}; buildings={snapshot.Buildings.Buildings.Length}; " +
                $"sites={snapshot.Growth.Development.Sites.Length}");
        }

        [Test]
        public void UnknownDefinitionIdIsRejectedBeforeRuntimeMutation()
        {
            var snapshot = CitySnapshotSerializer.Capture(CreateGrowingRuntime());
            Assert.That(snapshot.Buildings.Buildings, Is.Not.Empty);
            snapshot.Buildings.Buildings[0].DefinitionId = 0xFFFF_FFFF_FFFF_FFF0UL;

            var exception = Assert.Throws<CitySnapshotValidationException>(
                () => CitySnapshotSerializer.Restore(snapshot));

            Assert.That(exception.FieldPath, Is.EqualTo("Buildings.Buildings[0].DefinitionId"));
            Assert.That(exception.Message, Does.Contain("Unknown building definition ID"));
        }

        [Test]
        public void UnsupportedFormatVersionIsRejectedWithoutApplyingMigration()
        {
            var snapshot = CitySnapshotSerializer.Capture(CreateGrowingRuntime());
            snapshot.Header.FormatVersion = CitySnapshotFormat.CurrentVersion + 1;

            var exception = Assert.Throws<CitySnapshotValidationException>(
                () => CitySnapshotSerializer.Restore(snapshot));

            Assert.That(exception.FieldPath, Is.EqualTo("Header.FormatVersion"));
            Assert.That(exception.Message, Does.Contain("migration is not part of P4-01"));
        }

        [Test]
        public void SnapshotDtoGraphContainsOnlySerializableValueDataAndNoUnityObjects()
        {
            Assert.That(SnapshotContractValidator.Validate(), Is.Empty);

            var visited = new HashSet<Type>();
            var fields = new List<FieldInfo>();
            CollectFields(typeof(CitySnapshotDto), visited, fields);

            Assert.That(fields.Any(field =>
                typeof(UnityEngine.Object).IsAssignableFrom(UnwrapArray(field.FieldType))), Is.False);
            Assert.That(fields.Any(field =>
                field.FieldType.Namespace != null &&
                (field.FieldType.Namespace.Contains(".Presentation") ||
                 field.FieldType.Namespace.Contains(".Gameplay") ||
                 field.FieldType.Namespace.Contains(".UI") ||
                 field.FieldType.Namespace.Contains(".Editor"))), Is.False);
        }

        [Test]
        public void CanonicalGrowingCityFixtureHasStableJsonChecksumForCrossPlatformHandoff()
        {
            var snapshot = CitySnapshotSerializer.Capture(
                CreateGrowingRuntime(),
                FixtureGameVersion,
                FixtureUtcTicks);
            var json = JsonUtility.ToJson(snapshot);
            var checksum = Fnv1a64(Encoding.UTF8.GetBytes(json));

            TestContext.WriteLine(
                $"P4-01 canonical fixture: checksum={checksum:X16}; " +
                $"utf8Bytes={Encoding.UTF8.GetByteCount(json)}; Unity={UnityEngine.Application.unityVersion}");
            Assert.That(checksum, Is.EqualTo(ExpectedFixtureJsonChecksum));
        }

        private static CityRuntime CreateGrowingRuntime()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 16,
                Height = 10,
                Seed = 0x401UL,
                InitialFunds = 100_000,
                TicksPerMonth = 2,
                FirstSimulationMonth = new SimulationDate(2040, 1)
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
            AssertApplied(runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow,
                new CellCoord(11, 3),
                new CellCoord(14, 4))));
            AssertApplied(runtime.Execute(new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(new CellCoord(0, 4)),
                runtime.DefaultPowerPlantDefinitionId,
                new CellCoord(0, 4))));
            AssertApplied(runtime.Execute(new SetMunicipalTaxRatesCommand(100, 110, 120)));

            runtime.Parcels.RefreshIfNeeded();
            AddBuilding(runtime, 0x4011UL, 0, new CellCoord(1, 3), occupancy: 4, filledJobs: 0);
            AddBuilding(runtime, 0x4012UL, 1, new CellCoord(6, 3), occupancy: 0, filledJobs: 4);
            AddBuilding(runtime, 0x4013UL, 2, new CellCoord(11, 3), occupancy: 0, filledJobs: 4);
            runtime.RefreshReadModels();

            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
            Assert.That(runtime.AdvanceSimulation(0.1d), Is.EqualTo(1));
            Assert.That(runtime.MonthlyScheduler.CompletedMonths, Is.EqualTo(1));
            Assert.That(runtime.GrowthSystem.State.Count, Is.GreaterThan(0));

            AssertApplied(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(15, 6),
                new CellCoord(15, 7))));
            runtime.SetSimulationSpeed(SimulationSpeed.Two);
            Assert.That(runtime.AdvanceSimulation(0.025d), Is.Zero);
            Assert.That(runtime.Clock.AccumulatedSeconds, Is.EqualTo(0.05d).Within(1e-12d));
            return runtime;
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

        private static string ToParcelKey(ParcelState parcel)
        {
            return $"{parcel.Id.Value}:{(int)parcel.ZoneType}:{parcel.Anchor.X}:{parcel.Anchor.Y}:{parcel.Width}:{parcel.Height}";
        }

        private static void CollectFields(
            Type type,
            ISet<Type> visited,
            ICollection<FieldInfo> fields)
        {
            type = UnwrapArray(type);
            if (type == typeof(string) || type.IsPrimitive || !type.IsClass || !visited.Add(type))
                return;

            var declared = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < declared.Length; index++)
            {
                if (declared[index].IsStatic)
                    continue;
                fields.Add(declared[index]);
                CollectFields(declared[index].FieldType, visited, fields);
            }
        }

        private static Type UnwrapArray(Type type)
        {
            return type.IsArray ? type.GetElementType() : type;
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
    }
}
