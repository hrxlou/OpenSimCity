using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UiIconSetupTests
    {
        [Test]
        public void ToolIconFamilyIsGeneratedAtTheCommittedResolution()
        {
            Assert.That(UiIconSetup.Validate(), Is.Empty);
        }
    }
}
