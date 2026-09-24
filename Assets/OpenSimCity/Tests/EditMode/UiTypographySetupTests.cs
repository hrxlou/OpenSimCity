using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UiTypographySetupTests
    {
        [Test]
        public void KoreanRuntimeFontHasLicenseAndPinnedProvenance()
        {
            Assert.That(UiTypographySetup.Validate(), Is.Empty);
        }
    }
}
