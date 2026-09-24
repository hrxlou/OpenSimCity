using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.Verification;
using OpenSimCity.Application.World;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class AudioDayNightSettingsTests
    {
        [Test]
        public void V1ProfileMigratesWithoutLosingAccessibilitySettings()
        {
            var store = new MemoryStore
            {
                Value = new PresentationSettingsDto
                {
                    SchemaVersion = 1,
                    UiScalePercent = 150,
                    VisualMode = (int)AccessibilityVisualMode.HighContrast,
                    StrongFocusVisible = true,
                    BindingOverrides = new[] { new PresentationBindingOverrideDto { Action = (int)PresentationInputAction.ToolRoad, ControlPath = "<Keyboard>/r" } }
                }
            };
            var controller = new PresentationSettingsController(store);
            Assert.That(controller.UiScale, Is.EqualTo(UiScalePercent.Percent150));
            Assert.That(controller.VisualMode, Is.EqualTo(AccessibilityVisualMode.HighContrast));
            Assert.That(controller.GetControlPath(PresentationInputAction.ToolRoad), Is.EqualTo("<Keyboard>/r"));
            Assert.That(controller.MasterVolumePermille, Is.EqualTo(1000));
            Assert.That(controller.MusicVolumePermille, Is.EqualTo(700));
            Assert.That(controller.DayNightEnabled, Is.True);
        }

        [Test]
        public void AudioAndDayNightSettingsDoNotChangeSimulationChecksum()
        {
            var runtime = new CityRuntime(CityRuntimeOptions.Default);
            var before = MvpStateChecksum.Compute(runtime);
            var controller = new PresentationSettingsController(new MemoryStore());
            controller.SetMasterVolumePermille(420);
            controller.SetMusicVolumePermille(310);
            controller.SetSfxVolumePermille(250);
            controller.SetAmbientVolumePermille(180);
            controller.SetAudioMuted(true);
            controller.SetDayNightEnabled(false);
            Assert.That(MvpStateChecksum.Compute(runtime), Is.EqualTo(before));
        }

        [Test]
        public void DayNightProjectionFreezesWhileSimulationIsPaused()
        {
            var runtime = new CityRuntime(CityRuntimeOptions.Default);
            var model = new DayNightPresentationReadModel(runtime);
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);
            model.RefreshIfNeeded();
            var beforeTick = model.Snapshot.SourceTick;
            var beforePhase = model.Snapshot.DayPhasePermille;
            runtime.AdvanceSimulation(runtime.Clock.FixedDeltaSeconds * 20d);
            model.RefreshIfNeeded();
            Assert.That(model.Snapshot.SourceTick, Is.EqualTo(beforeTick));
            Assert.That(model.Snapshot.DayPhasePermille, Is.EqualTo(beforePhase));
        }

        [Test]
        public void NewCityStartsInDaylightWithoutChangingSimulationState()
        {
            var runtime = new CityRuntime(CityRuntimeOptions.Default);
            var before = MvpStateChecksum.Compute(runtime);
            var model = new DayNightPresentationReadModel(runtime);
            Assert.That(model.Snapshot.DayPhasePermille, Is.EqualTo(500));
            Assert.That(model.Snapshot.IsNight, Is.False);
            Assert.That(MvpStateChecksum.Compute(runtime), Is.EqualTo(before));
        }

        private sealed class MemoryStore : IPresentationSettingsStore
        {
            public PresentationSettingsDto Value = new PresentationSettingsDto();
            public PresentationSettingsDto Load() => Value;
            public void Save(PresentationSettingsDto settings) => Value = settings;
        }
    }
}
