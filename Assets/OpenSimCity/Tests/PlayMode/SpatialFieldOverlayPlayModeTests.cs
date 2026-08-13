using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Spatial;
using OpenSimCity.Presentation.Spatial;
using OpenSimCity.Simulation.Spatial;
using OpenSimCity.UI.Spatial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class SpatialFieldOverlayPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPrototypeBuildsSingleMeshOverlayAndTextLegendWithoutChangingFields()
        {
            SpatialFieldOverlaySelection.Current.Reset();
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var host = Object.FindFirstObjectByType<SpatialFieldRuntimeHost>();
            var coordinator = Object.FindFirstObjectByType<SpatialFieldOverlayCoordinator>();
            var ui = Object.FindFirstObjectByType<SpatialFieldUiBinder>();
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.Panel, Is.Not.Null);
            Assert.That(ui.Panel.Q<Label>("osc-spatial-legend"), Is.Not.Null);

            var module = host.Module;
            var sourceId = new StableId(0xF630000000000100UL);
            var result = module.Execute(new UpsertSpatialSourceCommand(
                sourceId,
                SpatialSourceDefinitionIds.IndustrialEmitter,
                new CellCoord(context.Runtime.Width / 2, context.Runtime.Height / 2)));
            Assert.That(result.IsApplied, Is.True, result.Message);
            var checksum = module.Fields.ComputeChecksum();

            SpatialFieldOverlaySelection.Current.Show(SpatialFieldViewKind.Pollution);
            yield return null;
            yield return null;

            Assert.That(coordinator.IsVisible, Is.True);
            Assert.That(coordinator.ActiveKind, Is.EqualTo(SpatialFieldViewKind.Pollution));
            Assert.That(coordinator.RenderedCellCount,
                Is.EqualTo(context.Runtime.Width * context.Runtime.Height));
            Assert.That(coordinator.MeshVersion, Is.GreaterThan(0));
            Assert.That(coordinator.transform.childCount, Is.Zero,
                "The datamap must not create one GameObject per cell.");
            Assert.That(coordinator.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(coordinator.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(module.Fields.ComputeChecksum(), Is.EqualTo(checksum));

            SpatialFieldOverlaySelection.Current.Show(SpatialFieldViewKind.LandValue);
            yield return null;
            Assert.That(coordinator.ActiveKind, Is.EqualTo(SpatialFieldViewKind.LandValue));
            Assert.That(module.Fields.ComputeChecksum(), Is.EqualTo(checksum));
            SpatialFieldOverlaySelection.Current.Hide();
            yield return null;
            Assert.That(coordinator.IsVisible, Is.False);
            Assert.That(module.Fields.ComputeChecksum(), Is.EqualTo(checksum));

            var oldCoordinator = coordinator;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldCoordinator == null, Is.True);
            SpatialFieldOverlaySelection.Current.Reset();
        }
    }
}
