using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Presentation.Districts;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class DistrictPlayModeTests
    {
        [UnityTest]
        public IEnumerator DistrictPanelAndBoundaryAreDerivedFromApplicationState()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null; yield return null;
            var context = CityRuntimeContext.Current;
            Assert.That(context, Is.Not.Null);
            var module = DistrictRuntimeRegistry.GetOrCreate(context.Runtime);
            var id = module.NextDistrictId();
            Assert.That(module.Execute(new CreateDistrictCommand(id, "테스트 지구", 0x4C9AFFCCu)).IsApplied, Is.True);
            Assert.That(module.Execute(new PaintDistrictCommand(id, new[]
            {
                new CellCoord(1, 1), new CellCoord(2, 1), new CellCoord(1, 2), new CellCoord(2, 2)
            })).IsApplied, Is.True);
            yield return null; yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            var view = Object.FindFirstObjectByType<DistrictBoundaryView>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.rootVisualElement.Q<VisualElement>("osc-district-panel"), Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.RenderedSegmentCount, Is.EqualTo(8));

            var version = module.Districts.Version;
            DistrictSelection.Current.SetOverlayVisible(false);
            yield return null;
            Assert.That(view.RenderedSegmentCount, Is.Zero);
            Assert.That(module.Districts.Version, Is.EqualTo(version));
            DistrictSelection.Current.SetOverlayVisible(true);
        }
    }
}
