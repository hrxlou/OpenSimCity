using System;
using System.Collections.Generic;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Buildings;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Application.Verification
{
    public sealed class P8BenchmarkFixtureState
    {
        public P8BenchmarkScenarioSpec Spec { get; set; }
        public CityRuntime Runtime { get; set; }
        public int BuildingCount { get; set; }
        public ulong Checksum { get; set; }
        public bool ReachedRequiredBuildingCount => BuildingCount >= Spec.RequiredBuildingCount;
    }

    public static class P8BenchmarkFixture
    {
        private const int BlockStride = 2;
        private const ulong BuildingIdPrefix = 0xB800_0000_0000_0000UL;

        public static P8BenchmarkFixtureState Build(P8BenchmarkScenarioSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Width < 16 || spec.Height < 16) throw new ArgumentOutOfRangeException(nameof(spec));
            if (spec.Width % BlockStride != 0 || spec.Height % BlockStride != 0)
                throw new ArgumentException("P8 benchmark dimensions must align to the fixed two-cell grid.", nameof(spec));
            var runtime = NewCityRuntimeFactory.Create(new CityRuntimeOptions
            {
                Width = spec.Width,
                Height = spec.Height,
                Seed = spec.Seed,
                InitialFunds = 100_000_000,
                PowerPlantCapacity = 1_000_000,
                TicksPerMonth = 300,
                FirstSimulationMonth = new SimulationDate(1, 1)
            });

            BuildRoadGrid(runtime);
            PaintZones(runtime);
            PlacePower(runtime);
            SeedBuildings(runtime, spec.RequiredBuildingCount);
            for (var month = 0; month < spec.SettlementMonths; month++)
                for (var tick = 0; tick < runtime.MonthlyScheduler.TicksPerMonth; tick++)
                    runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds);
            runtime.RefreshReadModels();
            return new P8BenchmarkFixtureState
            {
                Spec = spec,
                Runtime = runtime,
                BuildingCount = runtime.Buildings.Count,
                Checksum = MvpStateChecksum.Compute(runtime)
            };
        }

        private static void BuildRoadGrid(CityRuntime runtime)
        {
            var rows = new List<int>();
            for (var y = BlockStride; y < runtime.Height; y += BlockStride) rows.Add(y);
            for (var index = 0; index < rows.Count; index++)
                Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                    new CellCoord(0, rows[index]), new CellCoord(runtime.Width - 1, rows[index])));

            for (var x = BlockStride; x < runtime.Width; x += BlockStride)
            {
                var startY = 0;
                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var intersectionY = rows[rowIndex];
                    if (startY <= intersectionY - 1)
                        Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                            new CellCoord(x, startY), new CellCoord(x, intersectionY - 1)));
                    startY = intersectionY + 1;
                }
                if (startY < runtime.Height)
                    Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                        new CellCoord(x, startY), new CellCoord(x, runtime.Height - 1)));
            }
        }

        private static void PaintZones(CityRuntime runtime)
        {
            var blockIndex = 0;
            var power = PowerCell(runtime);
            for (var y = 0; y < runtime.Height; y += BlockStride)
            {
                for (var x = 0; x < runtime.Width; x += BlockStride)
                {
                    var cell = new CellCoord(x + 1, y + 1);
                    if (!runtime.World.IsInBounds(cell) || cell == power)
                    {
                        blockIndex++;
                        continue;
                    }
                    var zone = blockIndex % 5 < 2 ? ZoneType.ResidentialLow
                        : blockIndex % 5 < 4 ? ZoneType.CommercialLow
                        : ZoneType.IndustrialLow;
                    Apply(runtime, new ZoneAreaCommand(zone, cell, cell));
                    blockIndex++;
                }
            }
        }

        private static void PlacePower(CityRuntime runtime)
        {
            var cell = PowerCell(runtime);
            Apply(runtime, new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(cell), runtime.DefaultPowerPlantDefinitionId, cell));
        }

        private static void SeedBuildings(CityRuntime runtime, int requested)
        {
            runtime.Parcels.RefreshIfNeeded();
            var definitions = new Dictionary<ZoneType, BuildingDefinitionData>();
            var catalog = runtime.BuildingDefinitions.Definitions;
            for (var index = 0; index < catalog.Count; index++)
            {
                var definition = catalog[index];
                if (definition.Footprint.Width != 1 || definition.Footprint.Height != 1 || definitions.ContainsKey(definition.ZoneType)) continue;
                definitions.Add(definition.ZoneType, definition);
            }

            var parcels = runtime.Parcels.Parcels;
            var created = 0;
            for (var index = 0; index < parcels.Count && created < requested; index++)
            {
                var parcel = parcels[index];
                if (!parcel.IsDevelopmentEligible || parcel.Width != 1 || parcel.Height != 1 || !definitions.TryGetValue(parcel.ZoneType, out var definition)) continue;
                var id = new StableId(BuildingIdPrefix | (ulong)(uint)(created + 1));
                if (!runtime.Buildings.Create(id, definition.Id, parcel.Id, parcel.Anchor,
                    definition.OccupancyCapacity, definition.JobCapacity))
                    throw new InvalidOperationException($"Benchmark building seed failed at parcel {parcel.Id}.");
                created++;
            }
        }

        private static CellCoord PowerCell(CityRuntime runtime) =>
            new CellCoord(runtime.Width / 2 + 1, runtime.Height / 2 + 1);

        private static void Apply(CityRuntime runtime, ICityCommand command)
        {
            var result = runtime.Execute(command);
            if (!result.IsApplied)
                throw new InvalidOperationException($"Benchmark setup command failed: {result.Message}");
        }
    }
}
