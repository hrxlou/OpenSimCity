using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ReleaseBuildContractTests
    {
        [Test]
        public void ProductScenesExcludeBenchmarkAndAreSharedAcrossPlatforms()
        {
            var windows = ReleaseBuildProfile.Create(ReleaseBuildPlatform.Windows);
            var macOS = ReleaseBuildProfile.Create(ReleaseBuildPlatform.MacOS);

            Assert.That(windows.ProductScenes, Is.EqualTo(macOS.ProductScenes));
            Assert.That(windows.ProductScenes, Is.EqualTo(new[]
            {
                "Assets/OpenSimCity/Scenes/Bootstrap.unity",
                "Assets/OpenSimCity/Scenes/MainMenu.unity",
                "Assets/OpenSimCity/Scenes/CityPrototype.unity"
            }));
        }

        [Test]
        public void SameCommitCleanBuildPairCanPass()
        {
            var windows = Passing(ReleaseBuildPlatform.Windows);
            var macOS = Passing(ReleaseBuildPlatform.MacOS);

            var result = ReleaseBuildPairGate.Evaluate(windows, macOS);

            Assert.That(result.Passed, Is.True);
            Assert.That(result.Reason, Is.EqualTo(ReleaseBuildPairGateReason.Passed));
        }

        [Test]
        public void DifferentSourceCommitIsRejected()
        {
            var windows = Passing(ReleaseBuildPlatform.Windows);
            var macOS = Passing(ReleaseBuildPlatform.MacOS);
            macOS.SourceCommit = "other";

            var result = ReleaseBuildPairGate.Evaluate(windows, macOS);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseBuildPairGateReason.CommitMismatch));
        }

        [Test]
        public void DirtyCheckoutIsRejectedEvenWhenBuildSucceeded()
        {
            var windows = Passing(ReleaseBuildPlatform.Windows);
            var macOS = Passing(ReleaseBuildPlatform.MacOS);
            windows.CleanCheckout = false;

            var result = ReleaseBuildPairGate.Evaluate(windows, macOS);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo(ReleaseBuildPairGateReason.DirtyCheckout));
        }

        private static ReleaseBuildExecutionEvidence Passing(ReleaseBuildPlatform platform)
        {
            var profile = ReleaseBuildProfile.Create(platform);
            return new ReleaseBuildExecutionEvidence
            {
                Platform = platform,
                BuildSucceeded = true,
                CleanCheckout = true,
                SourceCommit = "0123456789abcdef",
                UnityVersion = "6000.5.3f1",
                ProductName = profile.ProductName,
                ApplicationVersion = profile.ApplicationVersion,
                ContentCatalogVersion = profile.ContentCatalogVersion,
                OutputPath = profile.DefaultRelativeOutputPath,
                OutputBytes = 1,
                ProductScenes = profile.ProductScenes
            };
        }
    }
}
