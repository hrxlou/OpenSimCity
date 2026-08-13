using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Services;
using OpenSimCity.Presentation.Services;
using OpenSimCity.UI.Services;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class PublicServicePlayModeTests
    {
        [UnityTest]
        public IEnumerator PublicPlacementBudgetCoverageAndResponseViewsStayReadOnly()
        {
            PublicServiceSelection.Current.SetCoverageVisible(false);
            ServiceResponseSettings.Current.SetMaximumSamples(ServiceResponseProjection.DefaultMaximumSamples);
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var context = CityRuntimeContext.Current;
            var host = Object.FindFirstObjectByType<PublicServiceRuntimeHost>();
            var ui = Object.FindFirstObjectByType<PublicServiceUiBinder>();
            var views = Object.FindFirstObjectByType<PublicServiceViewCoordinator>();
            var responseView = Object.FindFirstObjectByType<ServiceResponseView>();
            var responsePanel = Object.FindFirstObjectByType<ServiceResponsePanel>();
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(ui, Is.Not.Null);
            Assert.That(views, Is.Not.Null);
            Assert.That(responseView, Is.Not.Null);
            Assert.That(responsePanel, Is.Not.Null);

            var runtime = context.Runtime;
            var roadY = runtime.Height / 2;
            Commit(context, ConstructionToolMode.Road,
                new CellCoord(0, roadY), new CellCoord(15, roadY));
            Commit(context, ConstructionToolMode.ZoneResidential,
                new CellCoord(2, roadY - 2), new CellCoord(3, roadY - 1));
            Commit(context, ConstructionToolMode.ZoneCommercial,
                new CellCoord(6, roadY - 2), new CellCoord(7, roadY - 1));
            Commit(context, ConstructionToolMode.ZoneIndustrial,
                new CellCoord(10, roadY - 2), new CellCoord(11, roadY - 1));
            Commit(context, ConstructionToolMode.PowerPlant,
                new CellCoord(15, roadY - 1), new CellCoord(15, roadY - 1));
            RunMonths(runtime, 6);

            var serviceCell = new CellCoord(4, roadY - 1);
            host.SelectCellForTests(serviceCell);
            var placement = host.Place(ServiceKindView.Police, serviceCell);
            Assert.That(placement.IsApplied, Is.True, placement.Message);
            RunMonths(runtime, 1);
            yield return null;
            host.Module.ReadModel.RefreshIfNeeded();
            var facility = host.Module.ReadModel.Snapshot.Facilities.Single();
            Assert.That(facility.Kind, Is.EqualTo(ServiceKindView.Police));
            Assert.That(facility.ActiveBudgetPermille, Is.EqualTo(1000));
            Assert.That(host.Module.ReadModel.Snapshot.Coverage.Count, Is.GreaterThan(0));

            var checksum = host.Module.System.State.ComputeChecksum();
            PublicServiceSelection.Current.SetKind(ServiceKindView.Police);
            PublicServiceSelection.Current.SetCoverageVisible(true);
            yield return null;
            yield return null;
            Assert.That(views.ActiveFacilityViewCount, Is.EqualTo(1));
            Assert.That(views.IsCoverageVisible, Is.True);
            Assert.That(views.CoverageCellCount, Is.GreaterThan(0));
            Assert.That(responseView.Projection.SourceSimulationChecksum, Is.EqualTo(checksum));
            Assert.That(responseView.ActiveCount, Is.LessThanOrEqualTo(responseView.Projection.Samples.Count));
            Assert.That(host.Module.System.State.ComputeChecksum(), Is.EqualTo(checksum));
            Assert.That(views.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)
                .Where(component => !ReferenceEquals(component, views)), Is.Empty);

            ServiceResponseSettings.Current.SetMaximumSamples(0);
            yield return null;
            yield return null;
            Assert.That(responseView.ActiveCount, Is.Zero);
            Assert.That(host.Module.System.State.ComputeChecksum(), Is.EqualTo(checksum));
            ServiceResponseSettings.Current.SetMaximumSamples(ServiceResponseProjection.DefaultMaximumSamples);

            var budget = host.SetBudget(facility.FacilityId, 500);
            Assert.That(budget.IsApplied, Is.True, budget.Message);
            RunMonths(runtime, 1);
            host.Module.ReadModel.RefreshIfNeeded();
            var halfBudget = host.Module.ReadModel.Snapshot.Facilities.Single();
            Assert.That(halfBudget.ActiveBudgetPermille, Is.EqualTo(500));
            Assert.That(halfBudget.EffectiveBudgetPermille, Is.LessThanOrEqualTo(500));
            var summary = ui.Panel.Q<Label>("osc-service-summary");
            var responseStatus = ui.Panel.Q<Label>("osc-service-response-status");
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.text, Does.Contain("적용 기간"));
            Assert.That(responseStatus, Is.Not.Null);
            Assert.That(responseStatus.text, Does.Contain("미서비스 사건"));

            var oldViews = views;
            var oldResponseView = responseView;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(oldViews == null, Is.True);
            Assert.That(oldResponseView == null, Is.True);
            PublicServiceSelection.Current.SetCoverageVisible(false);
            ServiceResponseSettings.Current.SetMaximumSamples(ServiceResponseProjection.DefaultMaximumSamples);
        }

        private static void RunMonths(CityRuntime runtime, int count)
        {
            for (var index = 0; index < count; index++)
                runtime.ProcessSimulationTick(runtime.MonthlyScheduler.NextRunTick);
        }

        private static void Commit(
            CityRuntimeContext context,
            ConstructionToolMode mode,
            CellCoord start,
            CellCoord end)
        {
            context.SelectTool(mode);
            context.Tool.BeginDrag(start);
            context.Tool.UpdateDrag(end);
            var result = context.Tool.CommitDrag();
            Assert.That(result.Committed, Is.True, result.Message);
        }
    }
}
