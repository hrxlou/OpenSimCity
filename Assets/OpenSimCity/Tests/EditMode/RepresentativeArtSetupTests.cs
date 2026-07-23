using System.IO;
using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class RepresentativeArtSetupTests
    {
        [Test]
        public void RepresentativeVendorAndWrapperBoundaryPassesValidation()
        {
            var issues = RepresentativeArtSetup.Validate();
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void AcquisitionAndBlenderValidationReportsArePinned()
        {
            var manifest = File.ReadAllText(
                RepresentativeArtSetup.ProvenanceManifestPath);
            var report = File.ReadAllText(
                RepresentativeArtSetup.VendorRoot +
                "/Provenance/validation-report.json");

            StringAssert.Contains(
                "\"archiveSha256\": " +
                "\"5b1a945576d54cdbb4ccc9c3d52711e6d530da74c74c586407ecc28b165335da\"",
                manifest);
            StringAssert.Contains("\"spdx\": \"CC0-1.0\"", manifest);
            StringAssert.Contains("\"status\": \"Passed\"", report);
            StringAssert.Contains("\"blenderVersion\": \"5.1.2\"", report);
        }

        [Test]
        public void BenchmarkRemainsOutsideReleaseSceneList()
        {
            CollectionAssert.DoesNotContain(
                BuildScript.ReleaseScenePaths(),
                RepresentativeArtSetup.BenchmarkScenePath);
            CollectionAssert.Contains(
                BuildScript.ReleaseScenePaths(),
                RepresentativeArtSetup.CityPrototypeScenePath);
        }

    }
}
