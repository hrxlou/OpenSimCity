using System;
using System.IO;
using NUnit.Framework;
using OpenSimCity.Application.Onboarding;
using OpenSimCity.Platform.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class OnboardingProfileStoreTests
    {
        [Test]
        public void ProfileRoundTripIsSeparateAndVersioned()
        {
            var root = Path.Combine(Path.GetTempPath(), "osc-onboarding-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "Profile", "onboarding.json");
            try
            {
                var store = new JsonFileOnboardingProfileStore(path);
                store.Save(new OnboardingProfileDto { HintsEnabled = false, RoadPlaced = true, ZonePainted = true });
                var loaded = store.Load();
                Assert.That(loaded.SchemaVersion, Is.EqualTo(OnboardingProfileDto.CurrentSchemaVersion));
                Assert.That(loaded.HintsEnabled, Is.False);
                Assert.That(loaded.RoadPlaced, Is.True);
                Assert.That(loaded.ZonePainted, Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
