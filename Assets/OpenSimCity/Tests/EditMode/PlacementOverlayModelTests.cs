using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Zones;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PlacementOverlayModelTests
    {
        /// <summary>
        /// Builds a fixture with one externally connected road, one isolated road,
        /// and three parcels: eligible, no-frontage, and reachable-but-disconnected.
        /// </summary>
        private static CityRuntime BuildFixture()
        {
            var runtime = new CityRuntime(CityRuntimeOptions.Default);

            // Connected road from the west gate inward.
            runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId, new CellCoord(0, 32), new CellCoord(6, 32)));

            // Isolated road, not touching any edge gate.
            runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId, new CellCoord(10, 10), new CellCoord(12, 10)));

            // Eligible parcel: fronts the connected road at row 32.
            runtime.Execute(new ZoneAreaCommand(
                ZoneType.ResidentialLow, new CellCoord(1, 31), new CellCoord(2, 31)));

            // No-frontage parcel: isolated single cell with no adjacent road.
            runtime.Execute(new ZoneAreaCommand(
                ZoneType.CommercialLow, new CellCoord(20, 20), new CellCoord(20, 20)));

            // Fronts the isolated road but has no external connection.
            runtime.Execute(new ZoneAreaCommand(
                ZoneType.IndustrialLow, new CellCoord(10, 9), new CellCoord(10, 9)));

            return runtime;
        }

        private static PlacementOverlayCategory CategoryAt(PlacementOverlaySnapshot snapshot, CellCoord cell)
        {
            var match = snapshot.Cells.Where(c => c.Cell == cell).ToList();
            Assert.That(match, Is.Not.Empty, $"No overlay cell at {cell}.");
            return match[0].Category;
        }

        [Test]
        public void RoadConnectivityDistinguishesIsolatedNetworks()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildRoadConnectivity();

            Assert.That(CategoryAt(snapshot, new CellCoord(3, 32)),
                Is.EqualTo(PlacementOverlayCategory.RoadConnected));
            Assert.That(CategoryAt(snapshot, new CellCoord(11, 10)),
                Is.EqualTo(PlacementOverlayCategory.RoadIsolated));
        }

        [Test]
        public void AccessibilityOverlayReportsBandsAndGate()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildAccessibility();

            Assert.That(CategoryAt(snapshot, new CellCoord(0, 32)),
                Is.EqualTo(PlacementOverlayCategory.AccessibilityGate));
            Assert.That(CategoryAt(snapshot, new CellCoord(2, 32)),
                Is.EqualTo(PlacementOverlayCategory.AccessibilityNear));
            Assert.That(CategoryAt(snapshot, new CellCoord(11, 10)),
                Is.EqualTo(PlacementOverlayCategory.AccessibilityUnreachable));
        }

        [Test]
        public void AccessibilityLabelCarriesTheIntegerDistance()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildAccessibility();

            var gate = snapshot.Cells.First(c => c.Cell == new CellCoord(0, 32));
            var near = snapshot.Cells.First(c => c.Cell == new CellCoord(3, 32));

            Assert.That(gate.Label, Is.EqualTo("0"));
            Assert.That(near.Label, Is.EqualTo("3"));
        }

        [Test]
        public void ZoneOverlayCategoriesMatchZoneTypes()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildZone();

            Assert.That(CategoryAt(snapshot, new CellCoord(1, 31)),
                Is.EqualTo(PlacementOverlayCategory.ZoneResidential));
            Assert.That(CategoryAt(snapshot, new CellCoord(20, 20)),
                Is.EqualTo(PlacementOverlayCategory.ZoneCommercial));
            Assert.That(CategoryAt(snapshot, new CellCoord(10, 9)),
                Is.EqualTo(PlacementOverlayCategory.ZoneIndustrial));
        }

        [Test]
        public void ParcelOverlayDistinguishesTheThreeFailureModes()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildParcel();

            Assert.That(CategoryAt(snapshot, new CellCoord(1, 31)),
                Is.EqualTo(PlacementOverlayCategory.ParcelEligible));
            Assert.That(CategoryAt(snapshot, new CellCoord(20, 20)),
                Is.EqualTo(PlacementOverlayCategory.ParcelNoFrontage));
            Assert.That(CategoryAt(snapshot, new CellCoord(10, 9)),
                Is.EqualTo(PlacementOverlayCategory.ParcelNoExternalConnection));
        }

        [Test]
        public void ParcelOverlayAgreesWithTheParcelQuery()
        {
            var runtime = BuildFixture();
            var snapshot = new PlacementOverlayModel(runtime).BuildParcel();

            foreach (var overlayCell in snapshot.Cells)
            {
                Assert.That(runtime.ParcelModel.TryGetAtCell(overlayCell.Cell, out var parcel), Is.True);
                var expected = PlacementOverlayModel.CategoryForParcel(parcel);
                Assert.That(overlayCell.Category, Is.EqualTo(expected),
                    $"Overlay and query disagree at {overlayCell.Cell}.");
            }
        }

        [Test]
        public void EveryOverlayHasALegendCoveringItsCategories()
        {
            var runtime = BuildFixture();
            var model = new PlacementOverlayModel(runtime);

            foreach (PlacementOverlayKind kind in System.Enum.GetValues(typeof(PlacementOverlayKind)))
            {
                var snapshot = model.Build(kind);
                Assert.That(snapshot.Legend, Is.Not.Empty, $"{kind} overlay has no legend.");

                var legendCategories = snapshot.Legend.Select(entry => entry.Category).ToHashSet();
                foreach (var cell in snapshot.Cells)
                {
                    Assert.That(legendCategories, Does.Contain(cell.Category),
                        $"{kind} overlay cell category {cell.Category} is missing from the legend.");
                }
            }
        }
    }
}
