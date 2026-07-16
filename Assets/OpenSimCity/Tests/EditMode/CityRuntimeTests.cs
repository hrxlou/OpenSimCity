using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CityRuntimeTests
    {
        private static CityRuntime NewRuntime()
        {
            return new CityRuntime(CityRuntimeOptions.Default);
        }

        [Test]
        public void ConstructsWithDefaultWorldAndFunds()
        {
            var runtime = NewRuntime();

            Assert.That(runtime.Width, Is.EqualTo(64));
            Assert.That(runtime.Height, Is.EqualTo(64));
            Assert.That(runtime.Funds, Is.EqualTo(CityRuntimeOptions.DefaultInitialFunds));
            Assert.That(runtime.RoadDefinitions.Count, Is.GreaterThan(0));
            Assert.That(runtime.ZoneDefinitions.Count, Is.EqualTo(3));
        }

        [Test]
        public void RoadPreviewDoesNotMutateStateOrFunds()
        {
            var runtime = NewRuntime();
            var fundsBefore = runtime.Funds;
            var version = runtime.Roads.Version;

            var preview = runtime.Preview(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(4, 32)));

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Cost, Is.GreaterThan(0));
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.Version, Is.EqualTo(version));
        }

        [Test]
        public void ExecuteRoadAppliesStateAndDeductsFunds()
        {
            var runtime = NewRuntime();
            var fundsBefore = runtime.Funds;

            var preview = runtime.Preview(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(4, 32)));
            var execution = runtime.Execute(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(4, 32)));

            Assert.That(execution.IsApplied, Is.True);
            Assert.That(runtime.Roads.IsOccupied(new CellCoord(2, 32)), Is.True);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore - preview.Cost));
        }

        [Test]
        public void ReadModelsRefreshAfterCommit()
        {
            var runtime = NewRuntime();

            runtime.Execute(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(6, 32)));
            runtime.Execute(new ZoneAreaCommand(ZoneType.ResidentialLow, new CellCoord(1, 31), new CellCoord(2, 31)));

            Assert.That(runtime.RoadAccessibility.TryGet(new CellCoord(0, 32), out var gate), Is.True);
            Assert.That(gate.HasRoad, Is.True);
            Assert.That(gate.NetworkDistanceToExternalGate, Is.EqualTo(0));

            Assert.That(runtime.ZoneModel.Get(new CellCoord(1, 31)).IsZoned, Is.True);
            Assert.That(runtime.ParcelModel.Parcels.Count, Is.GreaterThan(0));
        }

        [Test]
        public void OrderedCommandStreamIsDeterministic()
        {
            var checksumA = BuildFixtureAndChecksum();
            var checksumB = BuildFixtureAndChecksum();

            Assert.That(checksumA, Is.EqualTo(checksumB));
        }

        private static ulong BuildFixtureAndChecksum()
        {
            var runtime = NewRuntime();
            runtime.Execute(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(6, 32)));
            runtime.Execute(
                new PlaceRoadCommand(runtime.DefaultRoadDefinitionId, new CellCoord(10, 10), new CellCoord(12, 10)));
            runtime.Execute(new ZoneAreaCommand(ZoneType.ResidentialLow, new CellCoord(1, 31), new CellCoord(2, 31)));
            return runtime.RoadGraph.ComputeChecksum();
        }
    }
}
