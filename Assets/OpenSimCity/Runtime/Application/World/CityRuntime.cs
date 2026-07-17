using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core;
using OpenSimCity.Core.Determinism;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.ExternalConnections;
using OpenSimCity.Simulation.Parcels;
using OpenSimCity.Simulation.Roads;
using OpenSimCity.Simulation.World;
using OpenSimCity.Simulation.Zones;

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

            RoadDefinitions = new RoadDefinitionCatalog(options.CreateRoadDefinitions());
            ZoneDefinitions = ZoneDefinitionCatalog.CreateDefault();
            Budget = new RoadConstructionBudget(options.InitialFunds);
            DefaultRoadDefinitionId = options.DefaultRoadDefinitionId;

            RoadCommands = new RoadCommandHandler(Roads, RoadDefinitions, Budget);
            ZoneCommands = new ZoneCommandHandler(Zones, Roads, ZoneDefinitions);
            _dispatcher = new CommandDispatcher(new ICityCommandHandler[]
            {
                RoadCommands,
                ZoneCommands
            });

            RoadAccessibility = new RoadAccessibilityReadModel(RoadGraph);
            ZoneModel = new ZoneReadModel(Zones);
            ParcelModel = new ParcelReadModel(Parcels);
        }

        public WorldState World { get; }

        public RoadNetworkState Roads { get; }

        public ZoneNetworkState Zones { get; }

        public RoadConnectivityGraph RoadGraph { get; }

        public ParcelNetworkState Parcels { get; }

        public RoadDefinitionCatalog RoadDefinitions { get; }

        public ZoneDefinitionCatalog ZoneDefinitions { get; }

        public RoadConstructionBudget Budget { get; }

        public RoadCommandHandler RoadCommands { get; }

        public ZoneCommandHandler ZoneCommands { get; }

        public RoadAccessibilityReadModel RoadAccessibility { get; }

        public ZoneReadModel ZoneModel { get; }

        public ParcelReadModel ParcelModel { get; }

        public StableId DefaultRoadDefinitionId { get; }

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

        public static readonly StableId StreetDefinitionId =
            new StableId(RoadDefinitionNamespace | 0x01UL);

        public int Width { get; set; } = WorldState.DefaultWidth;

        public int Height { get; set; } = WorldState.DefaultHeight;

        public int Seed { get; set; }

        public int InitialFunds { get; set; } = DefaultInitialFunds;

        public int RoadBuildCost { get; set; } = DefaultRoadBuildCost;

        public StableId DefaultRoadDefinitionId { get; set; } = StreetDefinitionId;

        public static CityRuntimeOptions Default => new CityRuntimeOptions();

        public IReadOnlyList<RoadDefinition> CreateRoadDefinitions()
        {
            return new[]
            {
                new RoadDefinition(DefaultRoadDefinitionId, RoadBuildCost, "road.street")
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
