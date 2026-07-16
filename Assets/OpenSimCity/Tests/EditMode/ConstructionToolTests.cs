using NUnit.Framework;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ConstructionToolTests
    {
        private static ConstructionTool NewTool(out CityRuntime runtime)
        {
            runtime = new CityRuntime(CityRuntimeOptions.Default);
            return new ConstructionTool(runtime);
        }

        [Test]
        public void RoadPlacementCommitAppliesAndDeductsFunds()
        {
            var tool = NewTool(out var runtime);
            var fundsBefore = runtime.Funds;

            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));

            Assert.That(tool.Preview.IsValid, Is.True);
            var previewCost = tool.Preview.Cost;
            var previewCells = tool.Preview.ValidCells;

            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(runtime.Roads.IsOccupied(new CellCoord(2, 32)), Is.True);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore - previewCost));
            Assert.That(result.Cost, Is.EqualTo(previewCost));
            Assert.That(result.ChangedCells, Is.EquivalentTo(previewCells));
            Assert.That(tool.IsDragging, Is.False);
        }

        [Test]
        public void PreviewAndCommitReportTheSameCostAndCells()
        {
            var tool = NewTool(out _);

            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(5, 5));
            tool.UpdateDrag(new CellCoord(5, 9));
            var preview = tool.Preview;

            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(result.Cost, Is.EqualTo(preview.Cost));
            Assert.That(result.ChangedCells, Is.EquivalentTo(preview.ValidCells));
        }

        [Test]
        public void CancellingADragLeavesStateAndFundsUnchanged()
        {
            var tool = NewTool(out var runtime);
            var fundsBefore = runtime.Funds;
            var roadVersion = runtime.Roads.Version;

            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(8, 32));
            Assert.That(tool.Preview.IsValid, Is.True);

            tool.CancelDrag();

            Assert.That(tool.IsDragging, Is.False);
            Assert.That(tool.Preview.HasPreview, Is.False);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.Version, Is.EqualTo(roadVersion));
        }

        [Test]
        public void CommittingAnInvalidPreviewChangesNothing()
        {
            var tool = NewTool(out var runtime);

            // Place a road first, then try to place over it.
            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));
            tool.CommitDrag();

            var fundsBefore = runtime.Funds;
            var roadVersion = runtime.Roads.Version;

            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));
            Assert.That(tool.Preview.IsValid, Is.False);

            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.False);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
            Assert.That(runtime.Roads.Version, Is.EqualTo(roadVersion));
        }

        [Test]
        public void ZonePaintCommitAppliesZoneWithoutCost()
        {
            var tool = NewTool(out var runtime);
            var fundsBefore = runtime.Funds;

            tool.SelectTool(ConstructionToolMode.ZoneResidential);
            tool.BeginDrag(new CellCoord(2, 2));
            tool.UpdateDrag(new CellCoord(4, 4));
            Assert.That(tool.Preview.IsValid, Is.True);

            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(result.Cost, Is.EqualTo(0));
            Assert.That(runtime.Zones.IsZoned(new CellCoord(3, 3)), Is.True);
            Assert.That(runtime.Funds, Is.EqualTo(fundsBefore));
        }

        [Test]
        public void ZoneClearRemovesZone()
        {
            var tool = NewTool(out var runtime);

            tool.SelectTool(ConstructionToolMode.ZoneResidential);
            tool.BeginDrag(new CellCoord(2, 2));
            tool.UpdateDrag(new CellCoord(4, 4));
            tool.CommitDrag();
            Assert.That(runtime.Zones.IsZoned(new CellCoord(3, 3)), Is.True);

            tool.SelectTool(ConstructionToolMode.ZoneClear);
            tool.BeginDrag(new CellCoord(2, 2));
            tool.UpdateDrag(new CellCoord(4, 4));
            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(runtime.Zones.IsZoned(new CellCoord(3, 3)), Is.False);
        }

        [Test]
        public void DemolishRemovesPlacedRoad()
        {
            var tool = NewTool(out var runtime);

            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));
            tool.CommitDrag();
            Assert.That(runtime.Roads.IsOccupied(new CellCoord(2, 32)), Is.True);

            tool.SelectTool(ConstructionToolMode.Demolish);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));
            var result = tool.CommitDrag();

            Assert.That(result.Committed, Is.True);
            Assert.That(runtime.Roads.IsOccupied(new CellCoord(2, 32)), Is.False);
        }

        [Test]
        public void SwitchingToolCancelsActiveDrag()
        {
            var tool = NewTool(out var runtime);
            var roadVersion = runtime.Roads.Version;

            tool.SelectTool(ConstructionToolMode.Road);
            tool.BeginDrag(new CellCoord(0, 32));
            tool.UpdateDrag(new CellCoord(4, 32));
            Assert.That(tool.IsDragging, Is.True);

            tool.SelectTool(ConstructionToolMode.ZoneResidential);

            Assert.That(tool.IsDragging, Is.False);
            Assert.That(tool.Mode, Is.EqualTo(ConstructionToolMode.ZoneResidential));
            Assert.That(runtime.Roads.Version, Is.EqualTo(roadVersion));
        }
    }
}
