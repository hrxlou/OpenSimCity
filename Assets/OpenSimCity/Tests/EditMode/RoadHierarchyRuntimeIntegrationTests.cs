using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Roads;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RoadHierarchyRuntimeIntegrationTests
    {
        [Test]
        public void StableDefinitionValuesAndDefaultRuntimeCatalogRemainExplicit()
        {
            Assert.That(RoadHierarchyDefaults.StreetDefinitionId.Value,
                Is.EqualTo(0x0100_0000_0000_0001UL));
            Assert.That(RoadHierarchyDefaults.SmallRoadDefinitionId.Value,
                Is.EqualTo(0x0100_0000_0000_0002UL));
            Assert.That(RoadHierarchyDefaults.AvenueDefinitionId.Value,
                Is.EqualTo(0x0100_0000_0000_0003UL));
            Assert.That(RoadHierarchyDefaults.BoulevardDefinitionId.Value,
                Is.EqualTo(0x0100_0000_0000_0004UL));

            var runtime = new CityRuntime();
            Assert.That(runtime.DefaultRoadDefinitionId,
                Is.EqualTo(RoadHierarchyDefaults.StreetDefinitionId));
            Assert.That(runtime.RoadDefinitions.Count, Is.EqualTo(4));

            AssertDefinition(runtime, RoadHierarchyDefaults.SmallRoadDefinitionId,
                6, 0, 500, 20, 15, "road.small");
            AssertDefinition(runtime, RoadHierarchyDefaults.StreetDefinitionId,
                10, 1, 1000, 30, 30, "road.street");
            AssertDefinition(runtime, RoadHierarchyDefaults.AvenueDefinitionId,
                25, 5, 2400, 45, 70, "road.avenue");
            AssertDefinition(runtime, RoadHierarchyDefaults.BoulevardDefinitionId,
                38, 8, 3800, 50, 95, "road.boulevard");
        }

        [Test]
        public void RuntimeUpgradePublishesCentralHierarchyReadModelWithoutDemolition()
        {
            var runtime = new CityRuntime(new CityRuntimeOptions
            {
                Width = 8,
                Height = 4,
                InitialFunds = 10_000
            });
            var start = new CellCoord(0, 1);
            var end = new CellCoord(3, 1);

            var placement = runtime.Execute(new PlaceRoadCommand(
                RoadHierarchyDefaults.SmallRoadDefinitionId,
                start,
                end));
            var upgrade = runtime.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Forward,
                start,
                end));

            Assert.That(placement.IsApplied, Is.True, placement.Message);
            Assert.That(upgrade.IsApplied, Is.True, upgrade.Message);
            Assert.That(runtime.RoadHierarchyModel.Cells, Has.Count.EqualTo(4));
            Assert.That(runtime.RoadHierarchyModel.Cells.All(row =>
                row.DefinitionId == RoadHierarchyDefaults.AvenueDefinitionId &&
                row.TravelDirection == RoadTravelDirection.East &&
                row.CapacityPerMonth == 2400 &&
                row.FreeFlowSpeedCellsPerHour == 45), Is.True);
            Assert.That(runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(4));
        }

        private static void AssertDefinition(
            CityRuntime runtime,
            OpenSimCity.Core.StableId id,
            int buildCost,
            int maintenance,
            int capacity,
            int speed,
            int noise,
            string key)
        {
            var definition = runtime.RoadDefinitions.Get(id);
            Assert.That(definition.BuildCost, Is.EqualTo(buildCost));
            Assert.That(definition.MonthlyMaintenanceCost, Is.EqualTo(maintenance));
            Assert.That(definition.CapacityPerMonth, Is.EqualTo(capacity));
            Assert.That(definition.FreeFlowSpeedCellsPerHour, Is.EqualTo(speed));
            Assert.That(definition.NoiseEmission, Is.EqualTo(noise));
            Assert.That(definition.PresentationKey, Is.EqualTo(key));
        }
    }
}
