using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Persistence;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Platform.Persistence;
using OpenSimCity.Presentation.Roads;
using OpenSimCity.UI.Foundation;
using OpenSimCity.UI.HUD;
using OpenSimCity.UI.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class SaveRoundTripPlayModeTests
    {
        private string _storageRoot;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_storageRoot) && Directory.Exists(_storageRoot))
                Directory.Delete(_storageRoot, recursive: true);
        }

        [UnityTest]
        public IEnumerator SaveUiPausesInputAndLoadRebuildsRuntimeContextAndPooledViews()
        {
            yield return LoadCityPrototype();

            var host = UnityEngine.Object.FindAnyObjectByType<CityPersistenceHost>();
            var runtimeHost = UnityEngine.Object.FindAnyObjectByType<CityRuntimeHost>();
            var input = UnityEngine.Object.FindAnyObjectByType<ConstructionInputDriver>();
            var binder = UnityEngine.Object.FindAnyObjectByType<SaveUiBinder>();
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<UiFoundationBootstrap>();
            var roadViews = UnityEngine.Object.FindAnyObjectByType<RoadViewRenderer>();
            var municipalHud = UnityEngine.Object.FindAnyObjectByType<MunicipalHudBinder>();
            Assert.That(host, Is.Not.Null);
            Assert.That(runtimeHost, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(binder, Is.Not.Null);
            Assert.That(bootstrap.SavePanel, Is.Not.Null);

            _storageRoot = NewStorageRoot();
            host.ReinitializeForTests(_storageRoot);
            yield return null;

            binder.TogglePanel();
            Assert.That(binder.IsOpen, Is.True);
            Assert.That(bootstrap.SavePanel.Q<Button>("osc-save-write-manual-1"), Is.Not.Null);
            Assert.That(bootstrap.SavePanel.Q<Button>("osc-save-load-manual-1"), Is.Not.Null);
            Assert.That(bootstrap.SavePanel.Q<Button>("osc-save-load-autosave"), Is.Not.Null);
            Assert.That(bootstrap.SavePanel.Q<Button>("osc-save-retry"), Is.Not.Null);
            Assert.That(bootstrap.SavePanel.Q<Button>("osc-save-dismiss"), Is.Not.Null);

            var runtime = runtimeHost.Runtime;
            var roadY = runtime.Height / 2;
            Assert.That(runtime.Execute(new PlaceRoadCommand(
                runtime.DefaultRoadDefinitionId,
                new CellCoord(0, roadY),
                new CellCoord(3, roadY))).IsApplied, Is.True);
            yield return null;
            Assert.That(roadViews.ActiveViewCount, Is.EqualTo(4));

            runtime.SetSimulationSpeed(SimulationSpeed.Two);
            binder.RequestManualSave();
            Assert.That(host.Controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saving));
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(input.InteractionEnabled, Is.False);
            host.Controller.ProcessPendingOperation();
            yield return null;

            Assert.That(host.Controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Saved));
            Assert.That(runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Two));
            Assert.That(input.InteractionEnabled, Is.True);
            Assert.That(File.Exists(SavePath(CityPersistenceController.ManualSaveKey)), Is.True);

            Assert.That(runtime.Execute(new DemolishRoadCommand(
                new CellCoord(0, roadY),
                new CellCoord(3, roadY))).IsApplied, Is.True);
            yield return null;
            Assert.That(roadViews.ActiveViewCount, Is.Zero);

            var beforeLoad = runtimeHost.Runtime;
            binder.RequestLoad(CityPersistenceController.ManualSaveKey);
            Assert.That(host.Controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Loading));
            Assert.That(runtimeHost.Runtime, Is.SameAs(beforeLoad));
            Assert.That(input.InteractionEnabled, Is.False);
            municipalHud.RequestSimulationSpeed(SimulationSpeed.Four);
            municipalHud.RequestResidentialTaxIncrease();
            Assert.That(beforeLoad.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(beforeLoad.Finance.HasPendingTaxRateChange, Is.False);
            host.Controller.ProcessPendingOperation();
            Assert.That(runtimeHost.Runtime, Is.Not.SameAs(beforeLoad));
            Assert.That(CityRuntimeContext.Current.Runtime, Is.SameAs(runtimeHost.Runtime));
            Assert.That(input.InteractionEnabled, Is.True);
            yield return null;
            yield return null;

            Assert.That(roadViews.ActiveViewCount, Is.EqualTo(4));
            Assert.That(runtimeHost.Runtime.RoadGraph.Cells[roadY * runtimeHost.Runtime.Width].HasRoad, Is.True);
            Assert.That(runtimeHost.Runtime.Clock.Speed, Is.EqualTo(SimulationSpeed.Two));
            Assert.That(
                bootstrap.SavePanel.Q<Label>("osc-save-feedback").text,
                Does.Contain("복원"));
        }

        [UnityTest]
        public IEnumerator CorruptSaveShowsRecoveryChoicesWithoutReplacingCurrentCity()
        {
            yield return LoadCityPrototype();

            var host = UnityEngine.Object.FindAnyObjectByType<CityPersistenceHost>();
            var runtimeHost = UnityEngine.Object.FindAnyObjectByType<CityRuntimeHost>();
            var binder = UnityEngine.Object.FindAnyObjectByType<SaveUiBinder>();
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<UiFoundationBootstrap>();
            _storageRoot = NewStorageRoot();
            host.ReinitializeForTests(_storageRoot);
            yield return null;

            binder.RequestManualSave();
            host.Controller.ProcessPendingOperation();
            var current = runtimeHost.Runtime;
            File.WriteAllBytes(
                SavePath(CityPersistenceController.ManualSaveKey),
                new byte[] { 0xFF, 0x00, 0x81 });

            binder.RequestLoad(CityPersistenceController.ManualSaveKey);
            host.Controller.ProcessPendingOperation();
            yield return null;

            Assert.That(runtimeHost.Runtime, Is.SameAs(current));
            Assert.That(host.Controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Error));
            Assert.That(
                bootstrap.SavePanel.Q<Label>("osc-save-feedback").text,
                Does.Contain("손상"));
            Assert.That(
                bootstrap.SavePanel.Q<Button>("osc-save-retry").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(
                bootstrap.SavePanel.Q<Button>("osc-save-dismiss").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));

            host.Controller.DismissFeedback();
            Assert.That(host.Controller.ViewModel.State, Is.EqualTo(CityPersistenceOperationState.Idle));
        }

        private static IEnumerator LoadCityPrototype()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private string NewStorageRoot()
        {
            return Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "OpenSimCity",
                "P4-03-PlayMode",
                Guid.NewGuid().ToString("N"));
        }

        private string SavePath(string saveKey)
        {
            return Path.Combine(
                _storageRoot,
                saveKey + AtomicFileCitySaveStorage.SaveFileExtension);
        }
    }
}
