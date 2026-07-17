using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Utilities;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Buildings;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;
using OpenSimCity.Simulation.Utilities;

namespace OpenSimCity.Application.World
{
    /// <summary>
    /// Deterministic, Unity-independent assembly of the whole P2 city: world grid,
    /// road/zone/parcel simulation, external-connection graph, definition catalogs,
    /// construction budget, command handlers, dispatcher and the versioned read
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
            RoadGraph = new RoadConnectivityGraph(Roads, gates);
            Parcels = new ParcelNetworkState(Zones, RoadGraph);

            Buildings = new BuildingStateCollection();
            BuildingDefinitions = new BuildingDefinitionCatalog(options.CreateBuildingDefinitions());
            PowerProducers = new PowerProducerCollection();
            UtilityDefinitions = new UtilityDefinitionCatalog(options.CreateUtilityDefinitions());
            Power = new PowerNetwork(RoadGraph, Buildings, PowerProducers, BuildingDefinitions, UtilityDefinitions);

            RoadDefinitions = new RoadDefinitionCatalog(options.CreateRoadDefinitions());
            ZoneDefinitions = ZoneDefinitionCatalog.CreateDefault();
            Budget = new RoadConstructionBudget(options.InitialFunds);
            DefaultRoadDefinitionId = options.DefaultRoadDefinitionId;
            DefaultPowerPlantDefinitionId = options.PowerPlantDefinitionId;

            RoadCommands = new RoadCommandHandler(Roads, RoadDefinitions, Budget);
            ZoneCommands = new ZoneCommandHandler(Zones, Roads, ZoneDefinitions);
            PowerCommands = new PowerCommandHandler(PowerProducers, UtilityDefinitions, Roads);
            _dispatcher = new CommandDispatcher(new ICityCommandHandler[]
            {
                RoadCommands,
                ZoneCommands,
                PowerCommands
            });

            RoadAccessibility = new RoadAccessibilityReadModel(RoadGraph);
            ZoneModel = new ZoneReadModel(Zones);
            ParcelModel = new ParcelReadModel(Parcels);
            PowerModel = new PowerReadModel(Power);
            DevelopmentPowerEligibility = new DevelopmentPowerEligibilityQuery(ParcelModel, BuildingDefinitions, Power);
        }

        public WorldState World { get; }

        public RoadNetworkState Roads { get; }

        public ZoneNetworkState Zones { get; }

        public RoadConnectivityGraph RoadGraph { get; }

        public ParcelNetworkState Parcels { get; }
        public BuildingStateCollection Buildings { get; }
        public BuildingDefinitionCatalog BuildingDefinitions { get; }
        public PowerProducerCollection PowerProducers { get; }
        public UtilityDefinitionCatalog UtilityDefinitions { get; }
        public PowerNetwork Power { get; }

        public RoadDefinitionCatalog RoadDefinitions { get; }

        public ZoneDefinitionCatalog ZoneDefinitions { get; }

        public RoadConstructionBudget Budget { get; }

        public RoadCommandHandler RoadCommands { get; }

        public ZoneCommandHandler ZoneCommands { get; }
        public PowerCommandHandler PowerCommands { get; }

        public RoadAccessibilityReadModel RoadAccessibility { get; }

        public ZoneReadModel ZoneModel { get; }

        public ParcelReadModel ParcelModel { get; }
        public PowerReadModel PowerModel { get; }
        public DevelopmentPowerEligibilityQuery DevelopmentPowerEligibility { get; }

        public StableId DefaultRoadDefinitionId { get; }
        public StableId DefaultPowerPlantDefinitionId { get; }

        public StableId PowerProducerIdFor(CellCoord anchor)
        {
            if (!World.IsInBounds(anchor))
                throw new ArgumentOutOfRangeException(nameof(anchor));
            return new StableId(0xD100_0000_0000_0000UL | (ulong)(uint)(Roads.GetIndex(anchor) + 1));
        }

        public int Funds => Budget.Funds;

        public int Width => World.Width;

        public int Height => World.Height;

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

        public int Seed { get; set; }

        public int InitialFunds { get; set; } = DefaultInitialFunds;

        public int RoadBuildCost { get; set; } = DefaultRoadBuildCost;

        public StableId DefaultRoadDefinitionId { get; set; } = StreetDefinitionId;
        public StableId PowerPlantDefinitionId { get; set; } = DefaultPowerPlantDefinitionId;
        public int PowerPlantCapacity { get; set; } = 100;

        public static CityRuntimeOptions Default => new CityRuntimeOptions();

        public IReadOnlyList<RoadDefinition> CreateRoadDefinitions()
        {
            return new[]
            {
                new RoadDefinition(DefaultRoadDefinitionId, RoadBuildCost, "road.street")
            };
        }

        public IReadOnlyList<BuildingDefinitionData> CreateBuildingDefinitions()
        {
            return new[]
            {
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 1UL), ZoneType.ResidentialLow,
                    new BuildingFootprint(2, 2), 4, 0, "building.residential.low.placeholder", 4),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 2UL), ZoneType.CommercialLow,
                    new BuildingFootprint(2, 2), 0, 6, "building.commercial.low.placeholder", 6),
                new BuildingDefinitionData(new StableId(BuildingDefinitionNamespace | 3UL), ZoneType.IndustrialLow,
                    new BuildingFootprint(2, 2), 0, 8, "building.industrial.low.placeholder", 8)
            };
        }

        public IReadOnlyList<UtilityDefinitionData> CreateUtilityDefinitions()
        {
            return new[] { new UtilityDefinitionData(PowerPlantDefinitionId, PowerPlantCapacity, "utility.power.plant.placeholder") };
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
