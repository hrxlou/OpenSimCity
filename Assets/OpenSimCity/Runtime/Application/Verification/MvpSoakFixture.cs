using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Verification
{
    public enum MvpRoadLayout : byte
    {
        SimpleGrid = 1,
        EdgeBranch = 2
    }

    public enum MvpCommandKind : byte
    {
        PlaceRoad = 1,
        DemolishRoad = 2,
        PaintZone = 3,
        PlacePowerProducer = 4,
        RemovePowerProducer = 5,
        SetTaxRates = 6
    }

    /// <summary>Serializable value-only entry in the fixed P3 MVP command log.</summary>
    [Serializable]
    public sealed class MvpCommandLogEntry
    {
        public int ApplyBeforeMonth;
        public MvpCommandKind Kind;
        public int StartX;
        public int StartY;
        public int EndX;
        public int EndY;
        public int ZoneType;
        public int ResidentialTaxPermille;
        public int CommercialTaxPermille;
        public int IndustrialTaxPermille;
    }

    public sealed class MvpMonthlyRecord
    {
        public MvpMonthlyRecord(
            SimulationDate month,
            ulong checksum,
            int population,
            int employedWorkers,
            int taxIncome,
            int buildingCount,
            int feasibleParcelCount,
            string topBlockReason,
            bool hasExternalConnection,
            bool hasConnectedPowerProducer)
        {
            Month = month;
            Checksum = checksum;
            Population = population;
            EmployedWorkers = employedWorkers;
            TaxIncome = taxIncome;
            BuildingCount = buildingCount;
            FeasibleParcelCount = feasibleParcelCount;
            TopBlockReason = topBlockReason ?? string.Empty;
            HasExternalConnection = hasExternalConnection;
            HasConnectedPowerProducer = hasConnectedPowerProducer;
        }

        public SimulationDate Month { get; }
        public ulong Checksum { get; }
        public int Population { get; }
        public int EmployedWorkers { get; }
        public int TaxIncome { get; }
        public int BuildingCount { get; }
        public int FeasibleParcelCount { get; }
        public string TopBlockReason { get; }
        public bool HasExternalConnection { get; }
        public bool HasConnectedPowerProducer { get; }
    }

    public sealed class MvpSoakReport
    {
        internal MvpSoakReport(
            MvpRoadLayout layout,
            int simulatedMinutes,
            IReadOnlyList<MvpMonthlyRecord> months)
        {
            Layout = layout;
            SimulatedMinutes = simulatedMinutes;
            Months = months;
        }

        public MvpRoadLayout Layout { get; }
        public int SimulatedMinutes { get; }
        public IReadOnlyList<MvpMonthlyRecord> Months { get; }
        public ulong FinalChecksum => Months[Months.Count - 1].Checksum;
        public string SuggestedNextAction
        {
            get
            {
                var final = Months[Months.Count - 1];
                if (string.Equals(final.TopBlockReason, "NoPowerCapacity", StringComparison.Ordinal))
                    return "PlacePowerProducer";
                if (string.Equals(final.TopBlockReason, "RoadIneligible", StringComparison.Ordinal))
                    return "ReconnectExternalRoad";
                return "AdjustTaxRates";
            }
        }
        public bool HasFirstFeedbackLoop
        {
            get
            {
                for (var index = 0; index < Months.Count; index++)
                {
                    var row = Months[index];
                    if (row.Population > 0 && row.EmployedWorkers > 0 && row.TaxIncome > 0)
                        return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Fixed-seed, command-log-driven 30-minute P3 gate. The default clock contract is
    /// 0.1 seconds/tick and 300 ticks/month, so 60 monthly records represent 30 minutes.
    /// It advances explicit ticks without waiting on wall-clock time.
    /// </summary>
    public static class MvpSoakFixture
    {
        public const int Seed = 0x307;
        public const int Width = 32;
        public const int Height = 24;
        public const int DefaultSimulatedMinutes = 30;
        public const int SecondsPerMonth = 30;
        public const int PowerCutMonthIndex = 0;
        public const int PowerRecoveryMonthIndex = 1;
        public const int ExternalCutMonthIndex = 1;
        public const int ExternalRecoveryMonthIndex = 2;

        private static readonly CellCoord PowerPlantCell = new CellCoord(24, Height / 2 - 1);
        private static readonly CellCoord ExternalGateCell = new CellCoord(0, Height / 2);

        public static MvpSoakReport Run(
            MvpRoadLayout layout,
            bool observePresentationReadModels,
            int simulatedMinutes = DefaultSimulatedMinutes)
        {
            if (simulatedMinutes <= 0 || simulatedMinutes * 60 % SecondsPerMonth != 0)
                throw new ArgumentOutOfRangeException(nameof(simulatedMinutes));

            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = Width,
                Height = Height,
                Seed = Seed,
                TicksPerMonth = 300,
                FirstSimulationMonth = new SimulationDate(1, 1),
                InitialFunds = 1_000_000
            });
            var log = CreateCommandLog(layout);
            ApplyEntries(runtime, log, applyBeforeMonth: 0);
            SeedFeedbackLoop(runtime);

            var monthCount = simulatedMinutes * 60 / SecondsPerMonth;
            var records = new List<MvpMonthlyRecord>(monthCount);
            long tick = 0;
            for (var monthIndex = 0; monthIndex < monthCount; monthIndex++)
            {
                if (monthIndex > 0)
                    ApplyEntries(runtime, log, monthIndex);

                for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                    runtime.ProcessSimulationTick(new SimulationTick(tick++));

                if (observePresentationReadModels)
                {
                    _ = runtime.BuildingViews.Views.Count;
                    _ = runtime.RoadAccessibility.Cells.Count;
                    _ = runtime.PowerModel.ProducerViews.Count;
                }

                records.Add(Capture(runtime));
            }

            return new MvpSoakReport(layout, simulatedMinutes, records.AsReadOnly());
        }

        public static IReadOnlyList<MvpCommandLogEntry> CreateCommandLog(MvpRoadLayout layout)
        {
            var entries = new List<MvpCommandLogEntry>
            {
                Road(0, 0, Height / 2, 24, Height / 2),
                Zone(0, ZoneType.ResidentialLow, 1, 10, 3, 11),
                Zone(0, ZoneType.ResidentialLow, 1, 13, 3, 14),
                Zone(0, ZoneType.CommercialLow, 5, 10, 9, 11),
                Zone(0, ZoneType.CommercialLow, 5, 13, 9, 14),
                Zone(0, ZoneType.IndustrialLow, 11, 10, 15, 11),
                Zone(0, ZoneType.IndustrialLow, 11, 13, 15, 14),
                Power(0, MvpCommandKind.PlacePowerProducer),
                Tax(0, 10, 10, 10)
            };

            if (layout == MvpRoadLayout.SimpleGrid)
            {
                AddSplitVertical(entries, 4, 6, 18);
                AddSplitVertical(entries, 10, 6, 18);
                AddSplitVertical(entries, 16, 6, 18);
            }
            else if (layout == MvpRoadLayout.EdgeBranch)
            {
                entries.Add(Road(0, 4, 6, 4, 11));
                entries.Add(Road(0, 10, 8, 10, 11));
                entries.Add(Road(0, 16, 4, 16, 11));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(layout));
            }

            entries.Add(Power(PowerCutMonthIndex, MvpCommandKind.RemovePowerProducer));
            entries.Add(Power(PowerRecoveryMonthIndex, MvpCommandKind.PlacePowerProducer));
            entries.Add(new MvpCommandLogEntry
            {
                ApplyBeforeMonth = ExternalCutMonthIndex,
                Kind = MvpCommandKind.DemolishRoad,
                StartX = ExternalGateCell.X,
                StartY = ExternalGateCell.Y,
                EndX = ExternalGateCell.X,
                EndY = ExternalGateCell.Y
            });
            entries.Add(Road(
                ExternalRecoveryMonthIndex,
                ExternalGateCell.X,
                ExternalGateCell.Y,
                ExternalGateCell.X,
                ExternalGateCell.Y));
            return entries.AsReadOnly();
        }

        private static void AddSplitVertical(ICollection<MvpCommandLogEntry> entries, int x, int minY, int maxY)
        {
            entries.Add(Road(0, x, minY, x, Height / 2 - 1));
            entries.Add(Road(0, x, Height / 2 + 1, x, maxY));
        }

        private static void ApplyEntries(
            CityRuntime runtime,
            IReadOnlyList<MvpCommandLogEntry> entries,
            int applyBeforeMonth)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.ApplyBeforeMonth != applyBeforeMonth)
                    continue;

                var command = ToCommand(runtime, entry);
                var result = runtime.Execute(command);
                if (!result.IsApplied)
                {
                    throw new InvalidOperationException(
                        $"MVP command {entry.Kind} for month {applyBeforeMonth} failed: {result.Message}");
                }
            }
        }

        private static ICityCommand ToCommand(CityRuntime runtime, MvpCommandLogEntry entry)
        {
            var start = new CellCoord(entry.StartX, entry.StartY);
            var end = new CellCoord(entry.EndX, entry.EndY);
            switch (entry.Kind)
            {
                case MvpCommandKind.PlaceRoad:
                    return new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, start, end);
                case MvpCommandKind.DemolishRoad:
                    return new DemolishRoadCommand(start, end);
                case MvpCommandKind.PaintZone:
                    return new ZoneAreaCommand((ZoneType)entry.ZoneType, start, end);
                case MvpCommandKind.PlacePowerProducer:
                    return new PlacePowerProducerCommand(
                        runtime.PowerProducerIdFor(PowerPlantCell),
                        runtime.DefaultPowerPlantDefinitionId,
                        PowerPlantCell);
                case MvpCommandKind.RemovePowerProducer:
                    return new RemovePowerProducerCommand(runtime.PowerProducerIdFor(PowerPlantCell));
                case MvpCommandKind.SetTaxRates:
                    return new SetMunicipalTaxRatesCommand(
                        entry.ResidentialTaxPermille,
                        entry.CommercialTaxPermille,
                        entry.IndustrialTaxPermille);
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Kind));
            }
        }

        private static void SeedFeedbackLoop(CityRuntime runtime)
        {
            runtime.Parcels.RefreshIfNeeded();
            var definitions = runtime.BuildingDefinitions;
            AddSeed(runtime, 0x6001UL, definitions.Definitions[0].Id, new CellCoord(1, 10), 4, 0);
            AddSeed(runtime, 0x6002UL, definitions.Definitions[3].Id, new CellCoord(1, 13), 8, 0);
            AddSeed(runtime, 0x6003UL, definitions.Definitions[2].Id, new CellCoord(11, 10), 0, 8);
            AddSeed(runtime, 0x6004UL, definitions.Definitions[1].Id, new CellCoord(5, 10), 0, 4);
            runtime.RefreshReadModels();
        }

        private static void AddSeed(
            CityRuntime runtime,
            ulong id,
            StableId definitionId,
            CellCoord anchor,
            int occupancy,
            int filledJobs)
        {
            ParcelState parcel = null;
            var parcels = runtime.Parcels.Parcels;
            for (var index = 0; index < parcels.Count; index++)
            {
                if (parcels[index].Anchor == anchor)
                {
                    parcel = parcels[index];
                    break;
                }
            }
            if (parcel == null)
                throw new InvalidOperationException($"MVP seed parcel at {anchor} was not created.");

            runtime.Buildings.Create(
                new StableId(id),
                definitionId,
                parcel.Id,
                anchor,
                occupancy,
                filledJobs);
        }

        private static MvpMonthlyRecord Capture(CityRuntime runtime)
        {
            runtime.RefreshReadModels();
            var hud = runtime.HudModel.Snapshot;
            var ledger = runtime.Finance.LatestLedgerEntry;
            var topReason = hud.TopBlockReasons != null && hud.TopBlockReasons.Length > 0
                ? hud.TopBlockReasons[0].Code
                : string.Empty;
            return new MvpMonthlyRecord(
                ledger.Month,
                MvpStateChecksum.Compute(runtime),
                runtime.PopulationSystem.Population.TotalPopulation,
                runtime.PopulationSystem.Employment.EmployedWorkers,
                ledger.TaxIncome,
                runtime.Buildings.Count,
                hud.TotalFeasibleParcelCount,
                topReason,
                runtime.PopulationSystem.Migration.HasExternalConnection,
                hud.HasConnectedPowerProducer);
        }

        private static MvpCommandLogEntry Road(int month, int startX, int startY, int endX, int endY)
        {
            return new MvpCommandLogEntry
            {
                ApplyBeforeMonth = month,
                Kind = MvpCommandKind.PlaceRoad,
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY
            };
        }

        private static MvpCommandLogEntry Zone(
            int month,
            ZoneType zone,
            int startX,
            int startY,
            int endX,
            int endY)
        {
            return new MvpCommandLogEntry
            {
                ApplyBeforeMonth = month,
                Kind = MvpCommandKind.PaintZone,
                ZoneType = (int)zone,
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY
            };
        }

        private static MvpCommandLogEntry Power(int month, MvpCommandKind kind)
        {
            return new MvpCommandLogEntry { ApplyBeforeMonth = month, Kind = kind };
        }

        private static MvpCommandLogEntry Tax(int month, int residential, int commercial, int industrial)
        {
            return new MvpCommandLogEntry
            {
                ApplyBeforeMonth = month,
                Kind = MvpCommandKind.SetTaxRates,
                ResidentialTaxPermille = residential,
                CommercialTaxPermille = commercial,
                IndustrialTaxPermille = industrial
            };
        }
    }

    /// <summary>Cross-platform FNV-1a checksum over P3's authoritative runtime state.</summary>
    public static class MvpStateChecksum
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(CityRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            runtime.RefreshReadModels();
            var hash = Offset;

            var roads = runtime.Roads.DefinitionIds;
            for (var index = 0; index < roads.Count; index++)
                Add(ref hash, roads[index].Value);
            var zones = runtime.Zones.Cells;
            for (var index = 0; index < zones.Count; index++)
            {
                Add(ref hash, (int)zones[index].ZoneType);
                Add(ref hash, zones[index].DefinitionId.Value);
            }
            var buildings = runtime.Buildings.Buildings;
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                Add(ref hash, building.Id.Value);
                Add(ref hash, building.DefinitionId.Value);
                Add(ref hash, building.ParcelId.Value);
                Add(ref hash, building.Anchor.X);
                Add(ref hash, building.Anchor.Y);
                Add(ref hash, building.Occupancy);
                Add(ref hash, building.FilledJobs);
            }
            var sites = runtime.GrowthSystem.State.Sites;
            for (var index = 0; index < sites.Count; index++)
            {
                Add(ref hash, sites[index].BuildingId.Value);
                Add(ref hash, sites[index].DefinitionId.Value);
                Add(ref hash, sites[index].MonthsRemaining);
            }
            var producers = runtime.PowerProducers.Producers;
            for (var index = 0; index < producers.Count; index++)
            {
                Add(ref hash, producers[index].Id.Value);
                Add(ref hash, producers[index].DefinitionId.Value);
                Add(ref hash, producers[index].Anchor.X);
                Add(ref hash, producers[index].Anchor.Y);
                Add(ref hash, producers[index].CurrentProduction);
            }

            Add(ref hash, runtime.PopulationSystem.Population.TotalPopulation);
            Add(ref hash, runtime.PopulationSystem.Population.HousingCapacity);
            Add(ref hash, runtime.PopulationSystem.Employment.EmployedWorkers);
            Add(ref hash, runtime.PopulationSystem.Employment.AvailableJobs);
            Add(ref hash, runtime.PopulationSystem.Migration.Arrivals);
            Add(ref hash, runtime.PopulationSystem.Migration.Departures);
            Add(ref hash, runtime.PopulationSystem.Migration.HasExternalConnection ? 1 : 0);
            Add(ref hash, runtime.UrbanEconomySystem.Rci.Residential.SmoothedDemand);
            Add(ref hash, runtime.UrbanEconomySystem.Rci.Commercial.SmoothedDemand);
            Add(ref hash, runtime.UrbanEconomySystem.Rci.Industrial.SmoothedDemand);
            Add(ref hash, runtime.Finance.Balance);
            Add(ref hash, runtime.Finance.ActiveTaxRates.ResidentialPermille);
            Add(ref hash, runtime.Finance.ActiveTaxRates.CommercialPermille);
            Add(ref hash, runtime.Finance.ActiveTaxRates.IndustrialPermille);
            Add(ref hash, runtime.Power.ComputeChecksum());
            Add(ref hash, runtime.RoadGraph.ComputeChecksum());
            return hash;
        }

        private static void Add(ref ulong hash, int value) => Add(ref hash, unchecked((ulong)(long)value));

        private static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= Prime;
            }
        }
    }
}
