using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Presentation.Overlays;
using OpenSimCity.Presentation.World;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class RoadUpgradeUxPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CityRuntimeContext.ResetForTests();
            var operation = SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            while (!operation.isDone)
                yield return null;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CityRuntimeContext.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpgradeControlsPreviewCommitAndHierarchyOverlayStayInSync()
        {
            var context = CityRuntimeContext.Current;
            var bootstrap = Object.FindFirstObjectByType<UiFoundationBootstrap>();
            var overlayRenderer = Object.FindFirstObjectByType<PlacementOverlayRenderer>();
            var roadRenderer = Object.FindFirstObjectByType<RoadViewRenderer>();
            Assert.That(context, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(overlayRenderer, Is.Not.Null);
            Assert.That(roadRenderer, Is.Not.Null);

            yield return null;
            var buttons = bootstrap.Root.Query<Button>().ToList();
            Assert.That(buttons.Any(button => button.text.Contains("도로 업그레이드")), Is.True);
            Assert.That(buttons.Any(button => button.text.Contains("등급·방향")), Is.True);

            var start = new CellCoord(0, context.Runtime.Height / 2);
            var end = new CellCoord(3, context.Runtime.Height / 2);
            var placement = context.Runtime.Execute(new PlaceRoadCommand(
                RoadUpgradeDefinitionIds.Small,
                start,
                end));
            Assert.That(placement.IsApplied, Is.True, placement.Message);
            yield return null;

            Assert.That(roadRenderer.ActiveViewCount, Is.EqualTo(4));
            var activeBefore = roadRenderer.ActiveViewCount;
            var pooledBefore = roadRenderer.PooledViewCount;
            var roadChecksum = context.Runtime.Roads.ComputeChecksum();
            var trafficChecksum = context.Runtime.TrafficRoutingSystem.State.ComputeChecksum();

            context.SelectTool(ConstructionToolMode.RoadUpgrade);
            context.Tool.ConfigureRoadUpgrade(
                RoadUpgradeDefinitionIds.Avenue,
                RoadDirectionMode.Forward);
            context.Tool.BeginDrag(start);
            context.Tool.UpdateDrag(end);
            yield return null;

            var panel = bootstrap.Root.Q<VisualElement>("osc-road-upgrade-panel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            var previewLabels = panel.Query<Label>().ToList();
            Assert.That(previewLabels.Any(label => label.text.Contains("건설값")), Is.True);
            Assert.That(previewLabels.Any(label => label.text.Contains("용량")), Is.True);
            Assert.That(previewLabels.Any(label => label.text.Contains("최대 V/C")), Is.True);
            Assert.That(context.Runtime.Roads.ComputeChecksum(), Is.EqualTo(roadChecksum));
            Assert.That(context.Runtime.TrafficRoutingSystem.State.ComputeChecksum(), Is.EqualTo(trafficChecksum));

            context.SetOverlay(PlacementOverlayKind.RoadHierarchy);
            yield return null;
            Assert.That(context.ActiveOverlaySnapshot.Kind,
                Is.EqualTo(PlacementOverlayKind.RoadHierarchy));
            Assert.That(overlayRenderer.ActiveCellCount, Is.EqualTo(4));
            Assert.That(context.ActiveOverlaySnapshot.Cells.All(cell => cell.Label == "↔"), Is.True);

            var committed = context.Tool.CommitDrag();
            Assert.That(committed.Committed, Is.True, committed.Message);
            yield return null;
            yield return null;

            Assert.That(roadRenderer.ActiveViewCount, Is.EqualTo(activeBefore));
            Assert.That(roadRenderer.PooledViewCount, Is.EqualTo(pooledBefore));
            Assert.That(context.Runtime.Roads.Cells.Count(cell => cell.IsOccupied), Is.EqualTo(4));
            Assert.That(context.ActiveOverlaySnapshot.Cells.All(cell =>
                cell.Category == PlacementOverlayCategory.RoadAvenue && cell.Label == "→"), Is.True);
            Assert.That(overlayRenderer.ActiveCellCount, Is.EqualTo(4));
            Assert.That(panel.Query<Label>().ToList().Any(label =>
                label.text.Contains("건설값") && label.text.Contains("차액")), Is.True);

            var checksumAfterCommit = context.Runtime.Roads.ComputeChecksum();
            context.ToggleOverlay(PlacementOverlayKind.RoadHierarchy);
            yield return null;
            Assert.That(context.HasActiveOverlay, Is.False);
            Assert.That(context.Runtime.Roads.ComputeChecksum(), Is.EqualTo(checksumAfterCommit));
        }
    }
}
