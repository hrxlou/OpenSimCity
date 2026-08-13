using System.Collections;
using NUnit.Framework;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Districts;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class DistrictPolicyPlayModeTests
    {
        [UnityTest]
        public IEnumerator PublicPanelShowsPendingPeriodWithoutMutatingCityDuringObservation()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null; yield return null;
            var context = CityRuntimeContext.Current;
            Assert.That(context, Is.Not.Null);
            var districts = DistrictRuntimeRegistry.GetOrCreate(context.Runtime);
            var id = districts.NextDistrictId();
            Assert.That(districts.Execute(new CreateDistrictCommand(id, "정책 지구", 0x4C9AFFCCu)).IsApplied, Is.True);
            Assert.That(districts.Execute(new PaintDistrictCommand(id, new[] { new CellCoord(2, 2) })).IsApplied, Is.True);
            DistrictSelection.Current.SelectDistrict(id);
            var rules = DistrictPolicyRuntimeRegistry.GetOrCreate(context.Runtime);
            Assert.That(rules.Execute(new SetDistrictPolicyCommand(id, DistrictPolicyKind.TransitSubsidy, true)).IsApplied, Is.True);
            var funds = context.Runtime.Funds;
            var sequence = context.Runtime.NextCommandSequence;
            yield return null; yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var panel = document.rootVisualElement.Q<VisualElement>("osc-district-policy-panel");
            Assert.That(panel, Is.Not.Null);
            var rows = panel.Q<Label>("osc-district-policy-rows");
            Assert.That(rows, Is.Not.Null);
            Assert.That(rows.text, Does.Contain("다음 적용"));
            Assert.That(rows.text, Does.Contain("bus generalized cost"));
            Assert.That(context.Runtime.Funds, Is.EqualTo(funds));
            Assert.That(context.Runtime.NextCommandSequence, Is.EqualTo(sequence));
        }
    }
}
