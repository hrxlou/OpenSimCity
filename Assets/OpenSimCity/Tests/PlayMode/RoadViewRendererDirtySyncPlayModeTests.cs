using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Presentation.Roads;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class RoadViewRendererDirtySyncPlayModeTests
    {
        [UnityTest]
        public IEnumerator LocalRoadEditVisitsOnlyPublishedDirtyCellsAfterInitialSync()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var renderer = Object.FindAnyObjectByType<RoadViewRenderer>();
            Assert.That(context, Is.Not.Null);
            Assert.That(renderer, Is.Not.Null);

            var runtime = context.Runtime;
            var roadY = runtime.Height / 2;
            var execution = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(5, roadY)));
            Assert.That(execution.IsApplied, Is.True, execution.Message);

            var dirtyCellCount = runtime.RoadAccessibility.DirtyCells.Count;
            Assert.That(dirtyCellCount, Is.GreaterThan(0));
            Assert.That(dirtyCellCount, Is.LessThan(runtime.Width * runtime.Height));

            yield return null;

            Assert.That(renderer.ActiveViewCount, Is.EqualTo(6));
            Assert.That(renderer.LastSynchronizationCellCount, Is.EqualTo(dirtyCellCount));

            var middleX = context.WorldBounds.MinX + 2.5f * context.WorldBounds.CellSize;
            var middle = renderer.transform.Cast<Transform>().FirstOrDefault(candidate =>
                candidate.gameObject.activeSelf &&
                Mathf.Approximately(candidate.localPosition.x, middleX));
            Assert.That(middle, Is.Not.Null);
            AssertRoadPart(middle, "KerbNorth", true);
            AssertRoadPart(middle, "KerbEast", false);
            AssertRoadPart(middle, "KerbSouth", true);
            AssertRoadPart(middle, "KerbWest", false);
            AssertRoadPart(middle, "LaneNorth", false);
            AssertRoadPart(middle, "LaneEast", true);
            AssertRoadPart(middle, "LaneSouth", false);
            AssertRoadPart(middle, "LaneWest", true);

            var southArm = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(2, roadY - 2),
                new CellCoord(2, roadY - 1)));
            Assert.That(southArm.IsApplied, Is.True, southArm.Message);
            var northArm = runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(2, roadY + 1),
                new CellCoord(2, roadY + 2)));
            Assert.That(northArm.IsApplied, Is.True, northArm.Message);
            yield return null;

            AssertRoadPart(middle, "KerbNorth", false);
            AssertRoadPart(middle, "KerbEast", false);
            AssertRoadPart(middle, "KerbSouth", false);
            AssertRoadPart(middle, "KerbWest", false);
            AssertRoadPart(middle, "LaneNorth", true);
            AssertRoadPart(middle, "LaneEast", true);
            AssertRoadPart(middle, "LaneSouth", true);
            AssertRoadPart(middle, "LaneWest", true);
            AssertRoadPart(middle, "Crosswalk", true);
            AssertRoadPart(middle, "Crosswalk/North", true);
            AssertRoadPart(middle, "Crosswalk/East", true);
            AssertRoadPart(middle, "Crosswalk/South", true);
            AssertRoadPart(middle, "Crosswalk/West", true);
        }

        private static void AssertRoadPart(Transform road, string name, bool expectedActive)
        {
            var part = road.Find(name);
            Assert.That(part, Is.Not.Null, $"Road view is missing '{name}'.");
            Assert.That(part.gameObject.activeSelf, Is.EqualTo(expectedActive), name);
        }
    }
}
