using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Population;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.Utilities;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Persistence
{
    public static class CitySnapshotFormat
    {
        public const string Magic = "OpenSimCity.CitySnapshot";
        public const int CurrentVersion = 1;
        public const string DevelopmentGameVersion = "development";
    }

    [Serializable]
    public sealed class CitySnapshotHeaderDto
    {
        public string Magic = CitySnapshotFormat.Magic;
        public int FormatVersion = CitySnapshotFormat.CurrentVersion;
        public string GameVersion = CitySnapshotFormat.DevelopmentGameVersion;
        public long CreatedUtcTicks;
        public ulong WorldSeed;
        public ulong DefaultRoadDefinitionId;
        public ulong DefaultPowerPlantDefinitionId;
    }

    [Serializable]
    public sealed class RoadCellSnapshotDto
    {
        public int CellIndex;
        public ulong DefinitionId;
    }

    [Serializable]
    public sealed class RoadNetworkSnapshotDto
    {
        public RoadCellSnapshotDto[] Cells = Array.Empty<RoadCellSnapshotDto>();
    }

    [Serializable]
    public sealed class ZoneCellSnapshotDto
    {
        public int CellIndex;
        public int ZoneType;
        public ulong DefinitionId;
    }

    [Serializable]
    public sealed class ZoneNetworkSnapshotDto
    {
        public ZoneCellSnapshotDto[] Cells = Array.Empty<ZoneCellSnapshotDto>();
    }

    [Serializable]
    public sealed class ParcelSnapshotDto
    {
        public ulong Id;
        public int ZoneType;
        public int AnchorX;
        public int AnchorY;
        public int Width;
        public int Height;
    }

    [Serializable]
    public sealed class ParcelNetworkSnapshotDto
    {
        public ParcelSnapshotDto[] Parcels = Array.Empty<ParcelSnapshotDto>();
    }

    [Serializable]
    public sealed class ExternalConnectionSnapshotDto
    {
        public ulong Id;
        public int Kind;
        public int CellX;
        public int CellY;
        public bool IsEnabled;
    }

    [Serializable]
    public sealed class ExternalConnectionCollectionSnapshotDto
    {
        public ExternalConnectionSnapshotDto[] Connections =
            Array.Empty<ExternalConnectionSnapshotDto>();
    }

    [Serializable]
    public sealed class CityTimelineSnapshotDto
    {
        public long CurrentTick;
        public ulong NextCommandSequence;
        public double FixedDeltaSeconds;
        public int MaxCatchUpTicks;
        public int SimulationSpeed;
        public double AccumulatedSeconds;
        public bool WasCatchUpClamped;
        public int TicksPerMonth;
        public int CompletedMonths;
        public int NextMonthYear;
        public int NextMonth;
        public long NextSchedulerRunTick;
    }

    /// <summary>
    /// Version-one field order is declaration order below. Every nested value is a
    /// serializable scalar, string, DTO, or DTO array; no Unity object can cross this
    /// boundary.
    /// </summary>
    [Serializable]
    public sealed class CitySnapshotDto
    {
        public CitySnapshotHeaderDto Header;
        public WorldStateDto World;
        public CityTimelineSnapshotDto Timeline;
        public RoadNetworkSnapshotDto Roads;
        public ZoneNetworkSnapshotDto Zones;
        public ParcelNetworkSnapshotDto Parcels;
        public ExternalConnectionCollectionSnapshotDto ExternalConnections;
        public BuildingStateCollectionDto Buildings;
        public PowerProducerCollectionDto PowerProducers;
        public GrowthDevelopmentSystemStateDto Growth;
        public PopulationEmploymentMigrationStateDto PopulationEmployment;
        public UrbanEconomySystemStateDto UrbanEconomy;
        public MunicipalFinanceStateDto MunicipalFinance;
    }

    public sealed class CitySnapshotValidationException : InvalidOperationException
    {
        public CitySnapshotValidationException(string fieldPath, string message)
            : base($"{fieldPath}: {message}")
        {
            FieldPath = fieldPath ?? string.Empty;
        }

        public string FieldPath { get; }
    }

    /// <summary>
    /// Reflection guard for the persistence schema. Only explicit field-only DTO
    /// graphs and scalar arrays are accepted, which excludes GameObject, Material,
    /// ScriptableObject, view, scene, prefab, and other runtime references without
    /// taking a UnityEngine dependency.
    /// </summary>
    public static class SnapshotContractValidator
    {
        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            ValidateType(typeof(CitySnapshotDto), "CitySnapshotDto", new HashSet<Type>(), issues);
            return issues.AsReadOnly();
        }

        private static void ValidateType(
            Type type,
            string path,
            ISet<Type> visited,
            ICollection<string> issues)
        {
            if (IsScalar(type))
                return;
            if (type.IsArray)
            {
                ValidateType(type.GetElementType(), path + "[]", visited, issues);
                return;
            }
            if (!type.IsClass ||
                !type.IsDefined(typeof(SerializableAttribute), inherit: false) ||
                !type.Name.EndsWith("Dto", StringComparison.Ordinal) ||
                type.Namespace == null ||
                !type.Namespace.StartsWith("OpenSimCity.", StringComparison.Ordinal) ||
                IsForbiddenLayer(type))
            {
                issues.Add($"{path} uses forbidden type {type.FullName}.");
                return;
            }
            if (!visited.Add(type))
                return;

            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].IsStatic)
                    continue;
                ValidateType(fields[index].FieldType, path + "." + fields[index].Name, visited, issues);
            }
        }

        private static bool IsScalar(Type type)
        {
            return type == typeof(bool) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(string);
        }

        private static bool IsForbiddenLayer(Type type)
        {
            return type.Name.IndexOf("View", StringComparison.Ordinal) >= 0 ||
                   type.Namespace.IndexOf(".Presentation", StringComparison.Ordinal) >= 0 ||
                   type.Namespace.IndexOf(".Gameplay", StringComparison.Ordinal) >= 0 ||
                   type.Namespace.IndexOf(".UI", StringComparison.Ordinal) >= 0 ||
                   type.Namespace.IndexOf(".Editor", StringComparison.Ordinal) >= 0 ||
                   type.Namespace.IndexOf(".Definitions", StringComparison.Ordinal) >= 0;
        }
    }

    /// <summary>
    /// Pure DTO mapper for P4-01. Encoding and file I/O are intentionally left to
    /// P4-02; callers may pass this field-only graph to JsonUtility for debug and
    /// cross-platform fixtures.
    /// </summary>
    public static class CitySnapshotSerializer
    {
        public static CitySnapshotDto Capture(
            CityRuntime runtime,
            string gameVersion = CitySnapshotFormat.DevelopmentGameVersion,
            long createdUtcTicks = 0)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            if (string.IsNullOrWhiteSpace(gameVersion))
                throw new ArgumentException("A game version is required.", nameof(gameVersion));
            if (createdUtcTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(createdUtcTicks));

            AssertContract();
            runtime.RefreshReadModels();

            return new CitySnapshotDto
            {
                Header = new CitySnapshotHeaderDto
                {
                    Magic = CitySnapshotFormat.Magic,
                    FormatVersion = CitySnapshotFormat.CurrentVersion,
                    GameVersion = gameVersion,
                    CreatedUtcTicks = createdUtcTicks,
                    WorldSeed = runtime.World.Seed,
                    DefaultRoadDefinitionId = runtime.DefaultRoadDefinitionId.Value,
                    DefaultPowerPlantDefinitionId = runtime.DefaultPowerPlantDefinitionId.Value
                },
                World = runtime.World.ToDto(),
                Timeline = CaptureTimeline(runtime),
                Roads = CaptureRoads(runtime),
                Zones = CaptureZones(runtime),
                Parcels = CaptureParcels(runtime),
                ExternalConnections = CaptureExternalConnections(runtime),
                Buildings = runtime.Buildings.ToDto(),
                PowerProducers = runtime.PowerProducers.ToDto(),
                Growth = runtime.GrowthSystem.ToDto(),
                PopulationEmployment = runtime.PopulationSystem.ToDto(),
                UrbanEconomy = runtime.UrbanEconomySystem.ToDto(),
                MunicipalFinance = runtime.Finance.ToDto()
            };
        }

        public static CityRuntime Restore(
            CitySnapshotDto snapshot,
            CityRuntimeOptions definitionSource = null)
        {
            AssertContract();
            ValidateRequiredFields(snapshot);
            var options = CreateRestoreOptions(snapshot, definitionSource ?? CityRuntimeOptions.Default);
            var runtime = new CityRuntime(options);

            ValidateDefinitions(snapshot, runtime);
            RestoreRoads(snapshot.Roads, runtime);
            RestoreZones(snapshot.Zones, runtime);
            runtime.RoadGraph.RefreshIfNeeded();
            runtime.Parcels.RefreshIfNeeded();
            ValidateParcels(snapshot.Parcels, runtime);
            ValidateExternalConnections(snapshot.ExternalConnections, runtime);
            RestoreBuildings(snapshot.Buildings, runtime);
            RestorePowerProducers(snapshot.PowerProducers, runtime);
            ValidateAggregateReferences(snapshot, runtime);

            runtime.GrowthSystem.RestoreFromDto(snapshot.Growth);
            runtime.PopulationSystem.RestoreFromDto(snapshot.PopulationEmployment);
            runtime.UrbanEconomySystem.RestoreFromDto(snapshot.UrbanEconomy);
            runtime.Finance.RestoreFromDto(snapshot.MunicipalFinance);
            RestoreTimeline(snapshot.Timeline, runtime);
            runtime.RefreshReadModels();
            return runtime;
        }

        private static void AssertContract()
        {
            var issues = SnapshotContractValidator.Validate();
            if (issues.Count != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, issues));
        }

        private static CityTimelineSnapshotDto CaptureTimeline(CityRuntime runtime)
        {
            return new CityTimelineSnapshotDto
            {
                CurrentTick = runtime.Clock.CurrentTick.Value,
                NextCommandSequence = runtime.NextCommandSequence.Value,
                FixedDeltaSeconds = runtime.Clock.FixedDeltaSeconds,
                MaxCatchUpTicks = runtime.Clock.MaxCatchUpTicks,
                SimulationSpeed = (int)runtime.Clock.Speed,
                AccumulatedSeconds = runtime.Clock.AccumulatedSeconds,
                WasCatchUpClamped = runtime.Clock.WasCatchUpClamped,
                TicksPerMonth = runtime.MonthlyScheduler.TicksPerMonth,
                CompletedMonths = runtime.MonthlyScheduler.CompletedMonths,
                NextMonthYear = runtime.MonthlyScheduler.NextMonth.Year,
                NextMonth = runtime.MonthlyScheduler.NextMonth.Month,
                NextSchedulerRunTick = runtime.MonthlyScheduler.NextRunTick.Value
            };
        }

        private static RoadNetworkSnapshotDto CaptureRoads(CityRuntime runtime)
        {
            var cells = new List<RoadCellSnapshotDto>();
            var source = runtime.Roads.Cells;
            for (var index = 0; index < source.Count; index++)
            {
                if (!source[index].IsOccupied)
                    continue;
                cells.Add(new RoadCellSnapshotDto
                {
                    CellIndex = index,
                    DefinitionId = source[index].DefinitionId.Value
                });
            }

            return new RoadNetworkSnapshotDto { Cells = cells.ToArray() };
        }

        private static ZoneNetworkSnapshotDto CaptureZones(CityRuntime runtime)
        {
            var cells = new List<ZoneCellSnapshotDto>();
            var source = runtime.Zones.Cells;
            for (var index = 0; index < source.Count; index++)
            {
                if (!source[index].IsZoned)
                    continue;
                cells.Add(new ZoneCellSnapshotDto
                {
                    CellIndex = index,
                    ZoneType = (int)source[index].ZoneType,
                    DefinitionId = source[index].DefinitionId.Value
                });
            }

            return new ZoneNetworkSnapshotDto { Cells = cells.ToArray() };
        }

        private static ParcelNetworkSnapshotDto CaptureParcels(CityRuntime runtime)
        {
            var source = runtime.Parcels.Parcels;
            var parcels = new ParcelSnapshotDto[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                parcels[index] = new ParcelSnapshotDto
                {
                    Id = source[index].Id.Value,
                    ZoneType = (int)source[index].ZoneType,
                    AnchorX = source[index].Anchor.X,
                    AnchorY = source[index].Anchor.Y,
                    Width = source[index].Width,
                    Height = source[index].Height
                };
            }

            return new ParcelNetworkSnapshotDto { Parcels = parcels };
        }

        private static ExternalConnectionCollectionSnapshotDto CaptureExternalConnections(
            CityRuntime runtime)
        {
            var source = runtime.ExternalConnections;
            var rows = new ExternalConnectionSnapshotDto[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                rows[index] = new ExternalConnectionSnapshotDto
                {
                    Id = source[index].Id.Value,
                    Kind = (int)source[index].Kind,
                    CellX = source[index].Cell.X,
                    CellY = source[index].Cell.Y,
                    IsEnabled = source[index].IsEnabled
                };
            }

            return new ExternalConnectionCollectionSnapshotDto { Connections = rows };
        }

        private static CityRuntimeOptions CreateRestoreOptions(
            CitySnapshotDto snapshot,
            CityRuntimeOptions source)
        {
            return new CityRuntimeOptions
            {
                Width = snapshot.World.Width,
                Height = snapshot.World.Height,
                Seed = snapshot.World.Seed,
                InitialFunds = Math.Max(0, snapshot.MunicipalFinance.Balance),
                RoadBuildCost = source.RoadBuildCost,
                RoadMonthlyMaintenanceCost = source.RoadMonthlyMaintenanceCost,
                DefaultRoadDefinitionId = RequireStableId(
                    snapshot.Header.DefaultRoadDefinitionId,
                    "Header.DefaultRoadDefinitionId"),
                PowerPlantDefinitionId = RequireStableId(
                    snapshot.Header.DefaultPowerPlantDefinitionId,
                    "Header.DefaultPowerPlantDefinitionId"),
                PowerPlantCapacity = source.PowerPlantCapacity,
                TicksPerMonth = snapshot.Timeline.TicksPerMonth,
                FirstSimulationMonth = new SimulationDate(
                    snapshot.Timeline.NextMonthYear,
                    snapshot.Timeline.NextMonth),
                UrbanEconomyParameters = source.UrbanEconomyParameters,
                GrowthDevelopmentParameters = source.GrowthDevelopmentParameters,
                MunicipalFinanceParameters = source.MunicipalFinanceParameters,
                InitialTaxRates = new UrbanEconomyTaxRates(
                    snapshot.MunicipalFinance.ActiveResidentialTaxPermille,
                    snapshot.MunicipalFinance.ActiveCommercialTaxPermille,
                    snapshot.MunicipalFinance.ActiveIndustrialTaxPermille)
            };
        }

        private static void ValidateRequiredFields(CitySnapshotDto snapshot)
        {
            if (snapshot == null)
                Fail("Snapshot", "Snapshot cannot be null.");
            Require(snapshot.Header, "Header");
            Require(snapshot.World, "World");
            Require(snapshot.Timeline, "Timeline");
            Require(snapshot.Roads, "Roads");
            Require(snapshot.Zones, "Zones");
            Require(snapshot.Parcels, "Parcels");
            Require(snapshot.ExternalConnections, "ExternalConnections");
            Require(snapshot.Buildings, "Buildings");
            Require(snapshot.PowerProducers, "PowerProducers");
            Require(snapshot.Growth, "Growth");
            Require(snapshot.PopulationEmployment, "PopulationEmployment");
            Require(snapshot.UrbanEconomy, "UrbanEconomy");
            Require(snapshot.MunicipalFinance, "MunicipalFinance");
            Require(snapshot.Growth.Development, "Growth.Development");
            Require(snapshot.PopulationEmployment.Population, "PopulationEmployment.Population");
            Require(snapshot.PopulationEmployment.Employment, "PopulationEmployment.Employment");
            Require(snapshot.PopulationEmployment.Migration, "PopulationEmployment.Migration");
            Require(snapshot.UrbanEconomy.Market, "UrbanEconomy.Market");
            Require(snapshot.UrbanEconomy.Rci, "UrbanEconomy.Rci");
            Require(snapshot.UrbanEconomy.Rci.Residential, "UrbanEconomy.Rci.Residential");
            Require(snapshot.UrbanEconomy.Rci.Commercial, "UrbanEconomy.Rci.Commercial");
            Require(snapshot.UrbanEconomy.Rci.Industrial, "UrbanEconomy.Rci.Industrial");

            if (!string.Equals(snapshot.Header.Magic, CitySnapshotFormat.Magic, StringComparison.Ordinal))
                Fail("Header.Magic", "Unknown snapshot kind.");
            if (snapshot.Header.FormatVersion != CitySnapshotFormat.CurrentVersion)
                Fail("Header.FormatVersion", "Unsupported snapshot version; migration is not part of P4-01.");
            if (string.IsNullOrWhiteSpace(snapshot.Header.GameVersion))
                Fail("Header.GameVersion", "Game version is required.");
            if (snapshot.Header.CreatedUtcTicks < 0)
                Fail("Header.CreatedUtcTicks", "UTC ticks cannot be negative.");
            if (snapshot.Header.WorldSeed != snapshot.World.Seed)
                Fail("Header.WorldSeed", "Header and world seed differ.");
            if (snapshot.Timeline.TicksPerMonth <= 0)
                Fail("Timeline.TicksPerMonth", "Monthly cadence must be positive.");
            if (snapshot.Timeline.CompletedMonths < 0)
                Fail("Timeline.CompletedMonths", "Completed month count cannot be negative.");
            if (snapshot.Timeline.CurrentTick < 0 || snapshot.Timeline.NextSchedulerRunTick < 0)
                Fail("Timeline", "Simulation ticks cannot be negative.");
        }

        private static void ValidateDefinitions(CitySnapshotDto snapshot, CityRuntime runtime)
        {
            var roads = snapshot.Roads.Cells ?? Array.Empty<RoadCellSnapshotDto>();
            for (var index = 0; index < roads.Length; index++)
            {
                Require(roads[index], $"Roads.Cells[{index}]");
                var id = RequireStableId(roads[index].DefinitionId, $"Roads.Cells[{index}].DefinitionId");
                if (!runtime.RoadDefinitions.TryGet(id, out _))
                    Fail($"Roads.Cells[{index}].DefinitionId", $"Unknown road definition ID {id.Value}.");
            }

            var zones = snapshot.Zones.Cells ?? Array.Empty<ZoneCellSnapshotDto>();
            for (var index = 0; index < zones.Length; index++)
            {
                Require(zones[index], $"Zones.Cells[{index}]");
                var id = RequireStableId(zones[index].DefinitionId, $"Zones.Cells[{index}].DefinitionId");
                if (!runtime.ZoneDefinitions.TryGet(id, out var definition))
                    Fail($"Zones.Cells[{index}].DefinitionId", $"Unknown zone definition ID {id.Value}.");
                if (zones[index].ZoneType != (int)definition.ZoneType)
                    Fail($"Zones.Cells[{index}].ZoneType", "Zone type does not match its definition.");
            }

            var buildings = snapshot.Buildings.Buildings ?? Array.Empty<BuildingStateDto>();
            for (var index = 0; index < buildings.Length; index++)
            {
                Require(buildings[index], $"Buildings.Buildings[{index}]");
                var id = RequireStableId(
                    buildings[index].DefinitionId,
                    $"Buildings.Buildings[{index}].DefinitionId");
                if (!runtime.BuildingDefinitions.TryGet(id, out _))
                    Fail($"Buildings.Buildings[{index}].DefinitionId", $"Unknown building definition ID {id.Value}.");
            }

            var producers = snapshot.PowerProducers.Producers ?? Array.Empty<PowerProducerStateDto>();
            for (var index = 0; index < producers.Length; index++)
            {
                Require(producers[index], $"PowerProducers.Producers[{index}]");
                var id = RequireStableId(
                    producers[index].DefinitionId,
                    $"PowerProducers.Producers[{index}].DefinitionId");
                if (!runtime.UtilityDefinitions.TryGet(id, out var definition))
                    Fail($"PowerProducers.Producers[{index}].DefinitionId", $"Unknown utility definition ID {id.Value}.");
                if (producers[index].CurrentProduction > definition.PowerCapacity)
                    Fail($"PowerProducers.Producers[{index}].CurrentProduction", "Production exceeds definition capacity.");
            }

            var sites = snapshot.Growth.Development?.Sites ?? Array.Empty<ConstructionSiteDto>();
            for (var index = 0; index < sites.Length; index++)
            {
                Require(sites[index], $"Growth.Development.Sites[{index}]");
                var id = RequireStableId(
                    sites[index].DefinitionId,
                    $"Growth.Development.Sites[{index}].DefinitionId");
                if (!runtime.BuildingDefinitions.TryGet(id, out _))
                    Fail($"Growth.Development.Sites[{index}].DefinitionId", $"Unknown building definition ID {id.Value}.");
            }

            var events = snapshot.Growth.LastEvents ?? Array.Empty<GrowthEventDto>();
            for (var index = 0; index < events.Length; index++)
            {
                Require(events[index], $"Growth.LastEvents[{index}]");
                var id = RequireStableId(
                    events[index].DefinitionId,
                    $"Growth.LastEvents[{index}].DefinitionId");
                if (!runtime.BuildingDefinitions.TryGet(id, out _))
                    Fail($"Growth.LastEvents[{index}].DefinitionId", $"Unknown building definition ID {id.Value}.");
            }
        }

        private static void RestoreRoads(RoadNetworkSnapshotDto dto, CityRuntime runtime)
        {
            var source = dto.Cells ?? Array.Empty<RoadCellSnapshotDto>();
            var previous = -1;
            for (var index = 0; index < source.Length; index++)
            {
                var row = source[index];
                if (row.CellIndex <= previous || row.CellIndex < 0 || row.CellIndex >= runtime.World.CellCount)
                    Fail($"Roads.Cells[{index}].CellIndex", "Road cells must be unique, in bounds, and row-major.");
                var cell = runtime.World.GetCoordinate(row.CellIndex);
                if (!runtime.Roads.TryPlaceRoad(cell, new StableId(row.DefinitionId)))
                    Fail($"Roads.Cells[{index}]", "Road state could not be restored.");
                previous = row.CellIndex;
            }
        }

        private static void RestoreZones(ZoneNetworkSnapshotDto dto, CityRuntime runtime)
        {
            var source = dto.Cells ?? Array.Empty<ZoneCellSnapshotDto>();
            var previous = -1;
            for (var index = 0; index < source.Length; index++)
            {
                var row = source[index];
                if (row.CellIndex <= previous || row.CellIndex < 0 || row.CellIndex >= runtime.World.CellCount)
                    Fail($"Zones.Cells[{index}].CellIndex", "Zone cells must be unique, in bounds, and row-major.");
                var cell = runtime.World.GetCoordinate(row.CellIndex);
                if (runtime.Roads.HasRoad(cell))
                    Fail($"Zones.Cells[{index}]", "A zone cell cannot overlap a road.");
                if (!runtime.Zones.TryApplyZone(
                        new[] { cell },
                        (ZoneType)row.ZoneType,
                        new StableId(row.DefinitionId)))
                {
                    Fail($"Zones.Cells[{index}]", "Zone state could not be restored.");
                }
                previous = row.CellIndex;
            }
        }

        private static void ValidateParcels(ParcelNetworkSnapshotDto dto, CityRuntime runtime)
        {
            var expected = dto.Parcels ?? Array.Empty<ParcelSnapshotDto>();
            var actual = runtime.Parcels.Parcels;
            if (expected.Length != actual.Count)
                Fail("Parcels.Parcels", "Rebuilt parcel count differs from the snapshot.");

            for (var index = 0; index < expected.Length; index++)
            {
                Require(expected[index], $"Parcels.Parcels[{index}]");
                var row = expected[index];
                var parcel = actual[index];
                if (row.Id != parcel.Id.Value ||
                    row.ZoneType != (int)parcel.ZoneType ||
                    row.AnchorX != parcel.Anchor.X ||
                    row.AnchorY != parcel.Anchor.Y ||
                    row.Width != parcel.Width ||
                    row.Height != parcel.Height)
                {
                    Fail($"Parcels.Parcels[{index}]", "Rebuilt parcel identity/topology differs from the snapshot.");
                }
            }
        }

        private static void ValidateExternalConnections(
            ExternalConnectionCollectionSnapshotDto dto,
            CityRuntime runtime)
        {
            var expected = dto.Connections ?? Array.Empty<ExternalConnectionSnapshotDto>();
            var actual = runtime.ExternalConnections;
            if (expected.Length != actual.Count)
                Fail("ExternalConnections.Connections", "External connection count differs from runtime definitions.");

            for (var index = 0; index < expected.Length; index++)
            {
                Require(expected[index], $"ExternalConnections.Connections[{index}]");
                var row = expected[index];
                var connection = actual[index];
                if (row.Id != connection.Id.Value ||
                    row.Kind != (int)connection.Kind ||
                    row.CellX != connection.Cell.X ||
                    row.CellY != connection.Cell.Y ||
                    row.IsEnabled != connection.IsEnabled)
                {
                    Fail($"ExternalConnections.Connections[{index}]", "External connection differs from runtime definitions.");
                }
            }
        }

        private static void RestoreBuildings(BuildingStateCollectionDto dto, CityRuntime runtime)
        {
            BuildingStateCollection restored;
            try
            {
                restored = BuildingStateCollection.FromDto(dto);
            }
            catch (Exception exception)
            {
                throw new CitySnapshotValidationException("Buildings", exception.Message);
            }

            var parcelIds = new List<StableId>();
            var parcels = runtime.Parcels.Parcels;
            for (var index = 0; index < parcels.Count; index++)
                parcelIds.Add(parcels[index].Id);
            var issues = BuildingStateReferenceValidator.Validate(
                restored.Buildings,
                runtime.BuildingDefinitions,
                parcelIds);
            if (issues.Count != 0)
            {
                Fail(
                    "Buildings",
                    $"Building {issues[0].BuildingId.Value} has invalid {issues[0].Kind} reference {issues[0].ReferencedId.Value}.");
            }

            for (var index = 0; index < restored.Buildings.Count; index++)
                runtime.Buildings.Add(restored.Buildings[index]);
        }

        private static void RestorePowerProducers(PowerProducerCollectionDto dto, CityRuntime runtime)
        {
            PowerProducerCollection restored;
            try
            {
                restored = PowerProducerCollection.FromDto(dto);
            }
            catch (Exception exception)
            {
                throw new CitySnapshotValidationException("PowerProducers", exception.Message);
            }

            for (var index = 0; index < restored.Producers.Count; index++)
                runtime.PowerProducers.Add(restored.Producers[index]);
        }

        private static void ValidateAggregateReferences(CitySnapshotDto snapshot, CityRuntime runtime)
        {
            var knownParcels = new HashSet<ulong>();
            var parcels = runtime.Parcels.Parcels;
            for (var index = 0; index < parcels.Count; index++)
                knownParcels.Add(parcels[index].Id.Value);

            var sites = snapshot.Growth.Development?.Sites ?? Array.Empty<ConstructionSiteDto>();
            var developedParcels = new HashSet<ulong>();
            var buildings = runtime.Buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
                developedParcels.Add(buildings[index].ParcelId.Value);
            for (var index = 0; index < sites.Length; index++)
            {
                if (!knownParcels.Contains(sites[index].ParcelId))
                    Fail($"Growth.Development.Sites[{index}].ParcelId", "Unknown parcel ID.");
                var buildingId = RequireStableId(
                    sites[index].BuildingId,
                    $"Growth.Development.Sites[{index}].BuildingId");
                if (runtime.Buildings.TryGet(buildingId, out _))
                    Fail($"Growth.Development.Sites[{index}].BuildingId", "Construction building ID already exists.");
                if (developedParcels.Contains(sites[index].ParcelId))
                    Fail($"Growth.Development.Sites[{index}].ParcelId", "Parcel is already developed.");
            }

            var population = snapshot.PopulationEmployment.Population?.Buildings ??
                             Array.Empty<BuildingPopulationStateDto>();
            for (var index = 0; index < population.Length; index++)
            {
                Require(population[index], $"PopulationEmployment.Population.Buildings[{index}]");
                var buildingId = RequireStableId(
                    population[index].BuildingId,
                    $"PopulationEmployment.Population.Buildings[{index}].BuildingId");
                if (!runtime.Buildings.TryGet(buildingId, out var building))
                    Fail($"PopulationEmployment.Population.Buildings[{index}].BuildingId", "Unknown building ID.");
                if (!runtime.BuildingDefinitions.TryGet(building.DefinitionId, out var definition))
                    Fail($"PopulationEmployment.Population.Buildings[{index}].BuildingId", "Unknown building definition.");
                if (population[index].Residents != building.Occupancy ||
                    population[index].Capacity != definition.OccupancyCapacity)
                {
                    Fail($"PopulationEmployment.Population.Buildings[{index}]", "Population row differs from building state/definition.");
                }
            }

            var employment = snapshot.PopulationEmployment.Employment?.Buildings ??
                             Array.Empty<OpenSimCity.Simulation.Employment.BuildingEmploymentStateDto>();
            for (var index = 0; index < employment.Length; index++)
            {
                Require(employment[index], $"PopulationEmployment.Employment.Buildings[{index}]");
                var buildingId = RequireStableId(
                    employment[index].BuildingId,
                    $"PopulationEmployment.Employment.Buildings[{index}].BuildingId");
                if (!runtime.Buildings.TryGet(buildingId, out var building))
                    Fail($"PopulationEmployment.Employment.Buildings[{index}].BuildingId", "Unknown building ID.");
                if (!runtime.BuildingDefinitions.TryGet(building.DefinitionId, out var definition))
                    Fail($"PopulationEmployment.Employment.Buildings[{index}].BuildingId", "Unknown building definition.");
                if (employment[index].FilledJobs != building.FilledJobs ||
                    employment[index].Capacity != definition.JobCapacity)
                {
                    Fail($"PopulationEmployment.Employment.Buildings[{index}]", "Employment row differs from building state/definition.");
                }
            }
        }

        private static void RestoreTimeline(CityTimelineSnapshotDto dto, CityRuntime runtime)
        {
            if (dto.FixedDeltaSeconds != runtime.Clock.FixedDeltaSeconds ||
                dto.MaxCatchUpTicks != runtime.Clock.MaxCatchUpTicks)
            {
                Fail("Timeline", "Clock configuration differs from this runtime version.");
            }
            if (!Enum.IsDefined(typeof(SimulationSpeed), dto.SimulationSpeed))
                Fail("Timeline.SimulationSpeed", "Unknown simulation speed.");
            if (dto.NextSchedulerRunTick % dto.TicksPerMonth != dto.TicksPerMonth - 1L)
                Fail("Timeline.NextSchedulerRunTick", "Scheduler tick is not aligned to its cadence.");
            if (dto.NextSchedulerRunTick < dto.CurrentTick ||
                dto.NextSchedulerRunTick - dto.CurrentTick >= dto.TicksPerMonth)
            {
                Fail("Timeline.NextSchedulerRunTick", "Scheduler next-run must be within the current monthly cadence.");
            }

            runtime.RestoreApplicationState(
                new CommandSequence(dto.NextCommandSequence),
                new SimulationTick(dto.CurrentTick),
                (SimulationSpeed)dto.SimulationSpeed,
                dto.AccumulatedSeconds,
                dto.WasCatchUpClamped,
                dto.CompletedMonths,
                new SimulationDate(dto.NextMonthYear, dto.NextMonth),
                new SimulationTick(dto.NextSchedulerRunTick));
        }

        private static StableId RequireStableId(ulong value, string path)
        {
            if (value == 0)
                Fail(path, "Stable ID must be non-zero.");
            return new StableId(value);
        }

        private static void Require(object value, string path)
        {
            if (value == null)
                Fail(path, "Required DTO is missing.");
        }

        private static void Fail(string path, string message)
        {
            throw new CitySnapshotValidationException(path, message);
        }
    }
}
