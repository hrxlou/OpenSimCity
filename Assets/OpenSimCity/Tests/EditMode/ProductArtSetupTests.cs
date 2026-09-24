using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ProductArtSetupTests
    {
        [Test]
        public void ProductCatalogUsesNormalizedHybridLodBuildings()
        {
            Assert.That(ProductArtSetup.Validate(), Is.Empty);
        }
    }
}
