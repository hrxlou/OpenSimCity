using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Development;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Population;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.World
{
    /// <summary>
    /// Deterministic, Unity-independent assembly of the whole P3 city: world grid,
    /// road/zone/parcel simulation, external-connection graph, definition catalogs,
    /// municipal construction funds, command handlers, dispatcher and the versioned read
    /// models overlays and UI consume.
    /// <para>
    /// It is the single object the Gameplay host constructs and drives. Preview
    /// methods never mutate simulation state; only <see cref="Execute"/> applies a
    /// command through the application dispatcher.
    /// </para>
    /// </summary>
    public sealed class CityRuntime
    {
        private readonly CommandDispatcher _dispatcher;
        private CommandSequence _sequence = CommandSequence.Zero;

        public CityRuntime()
            : this(CityRuntimeOptions.Default)
        {
        }

        public CityRuntime(CityRuntimeOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            World = new WorldState(options.Width, options.Height, options.Seed);
            Roads = new RoadNetworkState(World);
            Zones = new ZoneNetworkState(World);

            var gates = options.CreateExternalConnections(World.Width, World.Height);
            ExternalConnections = gates;
            RoadGraph = new RoadConnectivityGraph(Roads, gates);
            Parcels = new ParcelNetworkState(Zones, RoadGraph);

            Buildings = new BuildingStateCollection();
            BuildingDefinitions = new BuildingDefinitionCatalog(options.CreateBuildingDefinitions());
            PowerProducers = new PowerProducerCollection();
            UtilityDefinitions = new UtilityDefinitionCatalog(options.CreateUtilityDefinitions());
            Power = new PowerNetwork(RoadGraph, Buildings, PowerProducers, BuildingDefinitions, UtilityDefinitions);

            RoadDefinitions = new RoadDefinitionCatalog(options.CreateRoadDefinitions());
            ZoneDefinitions = ZoneDefinitionCatalog.CreateDefault();
            Finance = new MunicipalFinanceState(options.InitialFunds, options.InitialTaxRates);
            DefaultRoadDefinitionId = options.DefaultRoadDefinitionId;
            DefaultPowerPlantDefinitionId = options.PowerPlantDefinitionId;

            RoadCommands = new RoadCommandHandler(Roads, RoadDefinitions, Finance);
            ZoneCommands = new ZoneCommandHandler(Zones, Roads, ZoneDefinitions);
            PowerCommands = new PowerCommandHandler(PowerProducers, UtilityDefinitions, Roads, Finance);

            RoadAccessibility = new RoadAccessibilityReadModel(RoadGraph);
            ZoneModel = new ZoneReadModel(Zones);
            ParcelModel = new ParcelReadModel(Parcels);
            PowerModel = new PowerReadModel(Power, UtilityDefinitions, PowerProducers);
            DevelopmentPowerEligibility = new DevelopmentPowerEligibilityQuery(ParcelModel, BuildingDefinitions, Power);
            PopulationSystem = new PopulationEmploymentMigrationSystem(
                Buildings,
                BuildingDefinitions,
                ExternalConnections,
                HasReachableExternalRoadConnection);
            UrbanEconomySystem = new UrbanEconomySystem(
                Buildings,
                BuildingDefinitions,
                options.UrbanEconomyParameters);
            GrowthSystem = new GrowthDevelopmentSystem(
                Buildings,
                Parcels,
                Power,
                UrbanEconomySystem.Rci,
                BuildingDefinitions,
                options.GrowthDevelopmentParameters,
                (ulong)options.Seed);
            MunicipalFinanceSystem = new MunicipalFinanceMonthlySystem(
                Finance,
                UrbanEconomySystem,
                GrowthSystem,
                Buildings,
                BuildingDefinitions,
                Roads,
                RoadDefinitions,
                PowerProducers,
                UtilityDefinitions,
                options.MunicipalFinanceParameters);
            MonthlyScheduler = new MonthlySimulationScheduler(
                new IMonthlySimulationSystem[] { PopulationSystem, MunicipalFinanceSystem },
                options.TicksPerMonth,
                options.FirstSimulationMonth);
            Clock = new SimulationClock();
            FinanceCommands = new MunicipalFinanceCommandHandler(Finance, () => MonthlyScheduler.NextMonth);
            _dispatcher = new CommandDispatcher(new ICityCommandHandler[]
            {
                RoadCommands,
                ZoneCommands,
                PowerCommands,
                FinanceCommands
            });
            PopulationEmploymentModel = new PopulationEmploymentReadModel(PopulationSystem);
            UrbanEconomyModel = new UrbanEconomyReadModel(UrbanEconomySystem);
            GrowthModel = new GrowthDevelopmentReadModel(GrowthSystem);
            BuildingViews = new BuildingViewReadModel(Buildings, BuildingDefinitions, GrowthSystem);
            FinanceModel = new MunicipalFinanceReadModel(Finance);
            DevelopmentStatus = new DevelopmentStatusViewModel(
                GrowthModel,
                ParcelModel,
                RoadAccessibility,
                PowerModel);
            HudModel = new MunicipalHudViewModel(
                FinanceModel,
                PopulationEmploymentModel,
                UrbanEconomyModel,
                DevelopmentStatus,
                () => CurrentSimulationMonth);
        }

        public WorldState World { get; }

        public RoadNetworkState Roads { get; }

        public ZoneNetworkState Zones { get; }

        public RoadConnectivityGraph RoadGraph { get; }

        public ParcelNetworkState Parcels { get; }
        public IReadOnlyList<ExternalConnectionState> ExternalConnections { get; }
        public BuildingStateCollection Buildings { get; }
        public BuildingDefinitionCatalog BuildingDefinitions { get; }
        public PowerProducerCollection PowerProducers { get; }
        public UtilityDefinitionCatalog UtilityDefinitions { get; }
        public PowerNetwork Power { get; }

        public RoadDefinitionCatalog RoadDefinitions { get; }

        public ZoneDefinitionCatalog ZoneDefinitions { get; }

        /// <summary>Public municipal balance replacing the temporary P2 road-only budget.</summary>
        public MunicipalFinanceState Finance { get; }

        [Obsolete("Use Finance; P3 municipal finance owns the city balance.")]
        public MunicipalFinanceState Budget => Finance;

        public RoadCommandHandler RoadCommands { get; }

        public ZoneCommandHandler ZoneCommands { get; }
        public PowerCommandHandler PowerCommands { get; }
        public MunicipalFinanceCommandHandler FinanceCommands { get; }

        public RoadAccessibilityReadModel RoadAccessibility { get; }

        public ZoneReadModel ZoneModel { get; }

        public ParcelReadModel ParcelModel { get; }
        public PowerReadModel PowerModel { get; }
        public DevelopmentPowerEligibilityQuery DevelopmentPowerEligibility { get; }
        public PopulationEmploymentMigrationSystem PopulationSystem { get; }
        public UrbanEconomySystem UrbanEconomySystem { get; }
        public GrowthDevelopmentSystem GrowthSystem { get; }
        public MunicipalFinanceMonthlySystem MunicipalFinanceSystem { get; }
        public MonthlySimulationScheduler MonthlyScheduler { get; }
        public SimulationClock Clock { get; }
        public PopulationEmploymentReadModel PopulationEmploymentModel { get; }
        public UrbanEconomyReadModel UrbanEconomyModel { get; }
        public GrowthDevelopmentReadModel GrowthModel { get; }
        public BuildingViewReadModel BuildingViews { get; }
        public MunicipalFinanceReadModel FinanceModel { get; }
        public DevelopmentStatusViewModel DevelopmentStatus { get; }
        public MunicipalHudViewModel HudModel { get; }

        public StableId DefaultRoadDefinitionId { get; }
        public StableId DefaultPowerPlantDefinitionId { get; }
        public CommandSequence NextCommandSequence => _sequence;

        public StableId PowerProducerIdFor(CellCoord anchor)
        {
            if (!World.IsInBounds(anchor))
                throw new ArgumentOutOfRangeException(nameof(anchor));
            return new StableId(0xD100_0000_0000_0000UL | (ulong)(uint)(Roads.GetIndex(anchor) + 1));
        }

        public int Funds => Finance.Funds;

        public int Width => World.Width;

        public int Height => World.Height;

        /// <summary>The currently in-progress monthly period shown by the HUD.</summary>
        public SimulationDate CurrentSimulationMonth => MonthlyScheduler.NextMonth;

        /// <summary>
        /// Advances the application clock from a presentation host. Rendering speed
        /// cannot change the number or order of monthly systems that run for a
        /// given sequence of ticks.
        /// </summary>
        public int AdvanceSimulation(double realDeltaSeconds)
        {
            return Clock.Advance(realDeltaSeconds, ProcessSimulationTick);
        }

        /// <summary>Explicit tick seam used by deterministic fixtures and the runtime host.</summary>
        public void ProcessSimulationTick(SimulationTick tick)
        {
            MonthlyScheduler.ProcessTick(tick);
            RefreshReadModels();
        }

        public void SetSimulationSpeed(SimulationSpeed speed)
        {
            Clock.SetSpeed(speed);
        }

        internal void RestoreApplicationState(
            CommandSequence nextSequence,
            SimulationTick currentTick,
            SimulationSpeed speed,
            double accumulatedSeconds,
            bool wasCatchUpClamped,
            int completedMonths,
            SimulationDate nextMonth,
            SimulationTick nextSchedulerRunTick)
        {
            _sequence = nextSequence;
            Clock.RestoreState(currentTick, speed, accumulatedSeconds, wasCatchUpClamped);
            MonthlyScheduler.RestoreState(completedMonths, nextMonth, nextSchedulerRunTick);
        }

        // --- Preview seams (never mutate simulation state) ---

        public RoadCommandResult Preview(PlaceRoadCommand command)
        {
            return RoadCommands.Preview(command);
        }

        public RoadCommandResult Preview(DemolishRoadCommand command)
        {
            return RoadCommands.Preview(command);
        }

        public ZoneCommandResult Preview(ZoneAreaCommand command)
        {
            return ZoneCommands.Preview(command);
        }

        public ZoneCommandResult Preview(ClearZoneCommand command)
        {
            return ZoneCommands.Preview(command);
        }

        public PowerCommandResult Preview(PlacePowerProducerCommand command) => PowerCommands.Preview(command);
        public PowerCommandResult Preview(RemovePowerProducerCommand command) => PowerCommands.Preview(command);

        // --- Commit through the application dispatcher ---

        /// <summary>
        /// Validates and applies a command through the shared dispatcher, stamping
        /// it with the next deterministic sequence so an ordered command stream is
        /// reproducible. Preview and commit share the handler validation rules.
        /// </summary>
        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var stamp = new CommandStamp(SimulationTick.Zero, _sequence);
            _sequence = _sequence.Next();
            var result = _dispatcher.Dispatch(new CommandEnvelope(stamp, command));
            RefreshReadModels();
            return result;
        }

        /// <summary>
        /// Forces the derived caches and read models to observe the latest
        /// simulation versions. The getters already refresh lazily; this is a
        /// convenience for callers that snapshot several models at once.
        /// </summary>
        public void RefreshReadModels()
        {
            RoadGraph.RefreshIfNeeded();
            Parcels.RefreshIfNeeded();
            RoadAccessibility.RefreshIfNeeded();
            ZoneModel.RefreshIfNeeded();
            ParcelModel.RefreshIfNeeded();
            Power.RefreshIfNeeded();
            PowerModel.RefreshIfNeeded();
            BuildingViews?.RefreshIfNeeded();
            FinanceModel?.RefreshIfNeeded();
            DevelopmentStatus?.RefreshIfNeeded();
            HudModel?.RefreshIfNeeded();
        }

        private bool HasReachableExternalRoadConnection()
        {
            RoadGraph.RefreshIfNeeded();
            var cells = RoadGraph.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index].HasRoad && cells[index].IsReachable)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Static configuration for a <see cref="CityRuntime"/>. Values here are the
    /// deterministic fixture defaults; nothing is read from Unity or disk.
    /// </summary>
    public sealed class CityRuntimeOptions
    {
        public const int DefaultInitialFunds = 1_000_000;
        public const int DefaultRoadBuildCost = 10;
        public const int DefaultRoadMonthlyMaintenanceCost = 1;
        public const int DefaultPowerPlantConstructionCost = 100;
        public const int DefaultPowerPlantMonthlyMaintenanceCost = 5;

        private const ulong RoadDefinitionNamespace = 0x0100_0000_0000_0000UL;
        private const ulong ExternalGateNamespace = 0x0200_0000_0000_0000UL;
        private const ulong BuildingDefinitionNamespace = 0xB100_0000_0000_0000UL;
        private const ulong UtilityDefinitionNamespace = 0xC100_0000_0000_0000UL;

        public static readonly StableId StreetDefinitionId =
            new StableId(RoadDefinitionNamespace | 0x01UL);

        public static readonly StableId DefaultPowerPlantDefinitionId =
            new StableId(UtilityDefinitionNamespace | 0x01UL);

        public int Width { get; set; } = WorldState.DefaultWidth;

        public int Height { get; set; } = WorldState.DefaultHeight;

        public ulong Seed { get; set; }

        public int InitialFunds { get; set; } = DefaultInitialFunds;

        public int RoadBuildCost { get; set; } = DefaultRoadBuildCost;
        public int RoadMonthlyMaintenanceCost { get; set; } = DefaultRoadMonthlyMaintenanceCost;

        public StableId DefaultRoadDefinitionId { get; set; } = StreetDefinitionId;
        public StableId PowerPlantDefinitionId { get; set; } = DefaultPowerPlantDefinitionId;
        public int PowerPlantCapacity { get; set; } = 100;
        public int TicksPerMonth { get; set; } = MonthlySimulationScheduler.DefaultTicksPerMonth;
        public SimulationDate FirstSimulationMonth { get; set; } = new SimulationDate(1, 1);
        public UrbanEconomyParameters UrbanEconomyParameters { get; set; } = UrbanEconomyParameters.Default;
        public GrowthDevelopmentParameters GrowthDevelopmentParameters { get; set; } = GrowthDevelopmentParameters.Default;
        public MunicipalFinanceParameters MunicipalFinanceParameters { get; set; } = MunicipalFinanceParameters.Default;
        public UrbanEconomyTaxRates InitialTaxRates { get; set; } = UrbanEconomyTaxRates.Zero;

        public static CityRuntimeOptions Default => new CityRuntimeOptions();

        public IReadOnlyList<RoadDefinition> CreateRoadDefinitions()
        {
            return new[]
            {
                new RoadDefinition(
                    DefaultRoadDefinitionId,
                    RoadBuildCost,
                    "road.street",
                    RoadMonthlyMaintenanceCost)
            };
        }

        public IReadOnlyList<BuildingDefinitionData> CreateBuildingDefinitions()
        {
            return new[]
            {
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 1UL), ZoneType.ResidentialLow,
                    new BuildingFootprint(2, 2), 4, 0, "building.residential.low.a", 4),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 2UL), ZoneType.CommercialLow,
                    new BuildingFootprint(2, 2), 0, 6, "building.commercial.low.a", 6),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 3UL), ZoneType.IndustrialLow,
                    new BuildingFootprint(2, 2), 0, 8, "building.industrial.low.a", 8),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 4UL), ZoneType.ResidentialLow,
                    new BuildingFootprint(2, 1), 8, 0, "building.residential.low.b", 8),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 5UL), ZoneType.CommercialLow,
                    new BuildingFootprint(2, 1), 0, 4, "building.commercial.low.b", 4),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 6UL), ZoneType.IndustrialLow,
                    new BuildingFootprint(2, 1), 0, 6, "building.industrial.low.b", 6),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 7UL), ZoneType.ResidentialLow,
                    new BuildingFootprint(1, 1), 12, 0, "building.residential.low.c", 12),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 8UL), ZoneType.CommercialLow,
                    new BuildingFootprint(1, 1), 0, 10, "building.commercial.low.c", 10),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 9UL), ZoneType.IndustrialLow,
                    new BuildingFootprint(1, 1), 0, 12, "building.industrial.low.c", 12)
            };
        }

        public IReadOnlyList<UtilityDefinitionData> CreateUtilityDefinitions()
        {
            return new[]
            {
                new UtilityDefinitionData(
                    PowerPlantDefinitionId,
                    PowerPlantCapacity,
                    "utility.power.plant.placeholder",
                    DefaultPowerPlantConstructionCost,
                    DefaultPowerPlantMonthlyMaintenanceCost)
            };
        }

        /// <summary>
        /// One enabled road gate at the middle of each of the four map edges. A
        /// road that reaches an edge midpoint becomes externally connected; roads
        /// that stop short stay isolated. IDs are stable and edge-derived.
        /// </summary>
        public IReadOnlyList<ExternalConnectionState> CreateExternalConnections(int width, int height)
        {
            var midX = width / 2;
            var midY = height / 2;
            return new[]
            {
                new ExternalConnectionState(new StableId(ExternalGateNamespace | 0x01UL),
                    ExternalConnectionKind.Road, new CellCoord(0, midY), true),
                new ExternalConnectionState(new StableId(ExternalGateNamespace | 0x02UL),
                    ExternalConnectionKind.Road, new CellCoord(width - 1, midY), true),
                new ExternalConnectionState(new StableId(ExternalGateNamespace | 0x03UL),
                    ExternalConnectionKind.Road, new CellCoord(midX, 0), true),
                new ExternalConnectionState(new StableId(ExternalGateNamespace | 0x04UL),
                    ExternalConnectionKind.Road, new CellCoord(midX, height - 1), true)
            };
        }
    }
}
