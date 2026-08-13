using NUnit.Framework;
using OpenSimCity.Application.Settings;
using OpenSimCity.Application.Verification;
using OpenSimCity.Application.World;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PresentationSettingsControllerTests
    {
        [Test]
        public void RemapRejectsConflictsAndResetRestoresDefaults()
        {
            var store = new MemoryStore();
            var controller = new PresentationSettingsController(store);
            Assert.That(controller.TryRemap(PresentationInputAction.ToolRoad, "<Keyboard>/r", out _), Is.True);
            Assert.That(controller.TryRemap(PresentationInputAction.ToolResidential, "<Keyboard>/r", out var error), Is.False);
            Assert.That(error, Does.Contain("already assigned"));
            Assert.That(controller.GetControlPath(PresentationInputAction.ToolRoad), Is.EqualTo("<Keyboard>/r"));
            controller.ResetToDefaults();
            Assert.That(controller.GetControlPath(PresentationInputAction.ToolRoad), Is.EqualTo("<Keyboard>/digit1"));
        }

        [Test]
        public void PresentationVariantsDoNotChangeSimulationChecksum()
        {
            var runtime = new CityRuntime(CityRuntimeOptions.Default);
            var before = MvpStateChecksum.Compute(runtime);
            var controller = new PresentationSettingsController(new MemoryStore());
            controller.SetUiScale(UiScalePercent.Percent150);
            controller.SetVisualMode(AccessibilityVisualMode.HighContrast);
            controller.SetStrongFocusVisible(true);
            Assert.That(controller.TryRemap(PresentationInputAction.RotateLeft, "<Keyboard>/z", out _), Is.True);
            Assert.That(MvpStateChecksum.Compute(runtime), Is.EqualTo(before));
        }

        [Test]
        public void InvalidSchemaFallsBackToSafeDefaults()
        {
            var controller = new PresentationSettingsController(new MemoryStore
            {
                Value = new PresentationSettingsDto { SchemaVersion = 999, UiScalePercent = 150, VisualMode = 99 }
            });
            Assert.That(controller.UiScale, Is.EqualTo(UiScalePercent.Percent100));
            Assert.That(controller.VisualMode, Is.EqualTo(AccessibilityVisualMode.Standard));
            Assert.That(controller.GetControlPath(PresentationInputAction.MoveUp), Is.EqualTo("<Keyboard>/w"));
        }

        private sealed class MemoryStore : IPresentationSettingsStore
        {
            public PresentationSettingsDto Value = new PresentationSettingsDto();
            public PresentationSettingsDto Load() => Value;
            public void Save(PresentationSettingsDto settings) => Value = settings;
        }
    }
}
