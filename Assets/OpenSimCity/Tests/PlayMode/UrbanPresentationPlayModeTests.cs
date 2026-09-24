using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class UrbanPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityLoadsHdrReflectionsAndDetailedStreetAssets()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var dayNight = Object.FindAnyObjectByType<DayNightPresentationController>();
            Assert.That(dayNight, Is.Not.Null, "Scene transitions must reinstall the lighting controller.");
            Assert.That(dayNight.Snapshot, Is.Not.Null);
            Assert.That(dayNight.Snapshot.IsNight, Is.False);
            Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));
            Assert.That(RenderSettings.customReflectionTexture, Is.Not.Null);
            Assert.That(RenderSettings.customReflectionTexture.mipmapCount, Is.GreaterThan(1));
            foreach (var name in new[] { "StreetTree", "StreetTreeB", "StreetBench", "StreetWasteBin", "DensityMediumOffice" })
            {
                var prefab = Resources.Load<GameObject>("OpenSimCity/Environment/" + name);
                Assert.That(prefab, Is.Not.Null, name);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Decorative streetscape must not intercept construction picking.");
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, name);
                foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, name);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                }
            }
        }

        [UnityTest]
        public IEnumerator RoadsideFurnitureStaysAlignedWhenRoadGraphChanges()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var runtime = CityRuntimeContext.Current.Runtime;
            var y = runtime.Height / 2;
            Assert.That(runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                new CellCoord(0, y), new CellCoord(24, y))).IsApplied, Is.True);
            Assert.That(runtime.Execute(new PlaceRoadCommand(runtime.DefaultRoadDefinitionId,
                new CellCoord(12, y - 4), new CellCoord(12, y - 1))).IsApplied, Is.True);
            yield return null;
            yield return null;
            var coordinator = Object.FindAnyObjectByType<UrbanBackdropDetailCoordinator>();
            Assert.That(coordinator, Is.Not.Null);
            var signals = 0;
            foreach (Transform child in coordinator.transform)
            {
                if (!child.gameObject.activeSelf || child.name != "TrafficSignal") continue;
                signals++;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(child.eulerAngles.y,
                    Mathf.Round(child.eulerAngles.y / 90f) * 90f)), Is.LessThan(0.01f),
                    "Traffic furniture must face the connected street, not a random landscape angle.");
            }
            Assert.That(signals, Is.GreaterThanOrEqualTo(2));
        }
    }
}
