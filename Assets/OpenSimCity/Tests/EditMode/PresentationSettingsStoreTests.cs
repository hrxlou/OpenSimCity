using System;
using System.IO;
using NUnit.Framework;
using OpenSimCity.Application.Settings;
using OpenSimCity.Platform.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class PresentationSettingsStoreTests
    {
        [Test]
        public void PresentationSettingsRoundTripOutsideCitySave()
        {
            var root = Path.Combine(Path.GetTempPath(), "osc-presentation-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "Profile", "presentation-settings.json");
            try
            {
                var store = new JsonFilePresentationSettingsStore(path);
                store.Save(new PresentationSettingsDto
                {
                    UiScalePercent = 125,
                    VisualMode = (int)AccessibilityVisualMode.ColorIndependent,
                    BindingOverrides = new[] { new PresentationBindingOverrideDto { Action = (int)PresentationInputAction.Cancel, ControlPath = "<Keyboard>/x" } }
                });
                var loaded = store.Load();
                Assert.That(loaded.UiScalePercent, Is.EqualTo(125));
                Assert.That(loaded.VisualMode, Is.EqualTo((int)AccessibilityVisualMode.ColorIndependent));
                Assert.That(loaded.BindingOverrides.Length, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
