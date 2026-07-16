using System.Collections;
using NUnit.Framework;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class GridSelectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypeSelectsCornersAndCenterForEveryCameraRotation()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            var selectionTool = Object.FindAnyObjectByType<GridSelectionTool>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(selectionTool, Is.Not.Null);
            Assert.That(selectionTool.HighlightView, Is.Not.Null);

            rig.SetOrthographicSize(32f);
            rig.SetTarget(new Vector2(rig.WorldBounds.CenterX, rig.WorldBounds.CenterZ));
            var representativeCells = new[]
            {
                new CellCoord(0, 0),
                new CellCoord(63, 0),
                new CellCoord(0, 63),
                new CellCoord(63, 63),
                new CellCoord(32, 32)
            };

            for (var quarterTurn = 0; quarterTurn < 4; quarterTurn++)
            {
                rig.SetQuarterTurn(quarterTurn);
                foreach (var expected in representativeCells)
                {
                    var worldCenter = GridSelectionMath.CellCenter(expected, rig.WorldBounds);
                    var screenPosition = rig.ControlledCamera.WorldToScreenPoint(worldCenter);

                    Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.True);
                    Assert.That(selectionTool.SelectedCell, Is.EqualTo(expected));
                }
            }

            Assert.That(selectionTool.HighlightView.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator SelectionToolRejectsUiPointerWithoutChangingSelection()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            var selectionTool = Object.FindAnyObjectByType<GridSelectionTool>();
            Assert.That(selectionTool, Is.Not.Null);
            var screenPosition = rig.ControlledCamera.WorldToScreenPoint(
                GridSelectionMath.CellCenter(new CellCoord(10, 10), rig.WorldBounds));

            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.True);
            var selectionBeforeUiClick = selectionTool.SelectedCell;

            selectionTool.SetUiPointerBlocked(true);
            Assert.That(selectionTool.TrySelectScreenPosition(screenPosition), Is.False);
            Assert.That(selectionTool.SelectedCell, Is.EqualTo(selectionBeforeUiClick));
            Assert.That(selectionTool.State, Is.EqualTo(GridSelectionToolState.PointerOverUi));
            selectionTool.SetUiPointerBlocked(false);
        }
    }
}
