using System.Collections;
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
        }
    }
}
