using System;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
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

    /// <summary>
    /// Creates the deterministic city source state used by P8 measurements. The
    /// builder never claims that the requested scale was reached; callers record
    /// the actual building count and the evidence gate rejects undersized runs.
    /// </summary>
    public static class P8BenchmarkFixture
    {
        private const int BlockStride = 6;

        public static P8BenchmarkFixtureState Build(P8BenchmarkScenarioSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Width < 16 || spec.Height < 16) throw new ArgumentOutOfRangeException(nameof(spec));
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
            PlacePower(runtime);
            PaintZones(runtime);
            for (var month = 0; month < spec.GrowthMonths; month++)
                runtime.ProcessSimulationTick(runtime.MonthlyScheduler.NextRunTick);
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
            var centerX = runtime.Width / 2;
            var centerY = runtime.Height / 2;
            Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                new CellCoord(0, centerY), new CellCoord(runtime.Width - 1, centerY)));
            Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                new CellCoord(centerX, 0), new CellCoord(centerX, runtime.Height - 1)));

            for (var y = BlockStride; y < runtime.Height - 1; y += BlockStride)
                Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                    new CellCoord(0, y), new CellCoord(runtime.Width - 1, y)));
            for (var x = BlockStride; x < runtime.Width - 1; x += BlockStride)
                Apply(runtime, new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                    new CellCoord(x, 0), new CellCoord(x, runtime.Height - 1)));
        }

        private static void PaintZones(CityRuntime runtime)
        {
            var blockIndex = 0;
            for (var y = 0; y + BlockStride < runtime.Height; y += BlockStride)
            {
                for (var x = 0; x + BlockStride < runtime.Width; x += BlockStride)
                {
                    var start = new CellCoord(x + 1, y + 1);
                    var end = new CellCoord(Math.Min(runtime.Width - 2, x + BlockStride - 1),
                        Math.Min(runtime.Height - 2, y + BlockStride - 1));
                    if (start.X > end.X || start.Y > end.Y) continue;
                    var zone = blockIndex % 5 < 2 ? ZoneType.ResidentialLow
                        : blockIndex % 5 < 4 ? ZoneType.CommercialLow
                        : ZoneType.IndustrialLow;
                    var result = runtime.Execute(new ZoneAreaCommand(zone, start, end));
                    if (!result.IsApplied && !string.IsNullOrEmpty(result.Message))
                        throw new InvalidOperationException($"Benchmark zone setup failed at {start}: {result.Message}");
                    blockIndex++;
                }
            }
        }

        private static void PlacePower(CityRuntime runtime)
        {
            var cell = new CellCoord(runtime.Width / 2 + 1, runtime.Height / 2 + 1);
            Apply(runtime, new PlacePowerProducerCommand(
                runtime.PowerProducerIdFor(cell), runtime.DefaultPowerPlantDefinitionId, cell));
        }

        private static void Apply(CityRuntime runtime, ICityCommand command)
        {
            var result = runtime.Execute(command);
            if (!result.IsApplied)
                throw new InvalidOperationException($"Benchmark setup command failed: {result.Message}");
        }
    }
}
