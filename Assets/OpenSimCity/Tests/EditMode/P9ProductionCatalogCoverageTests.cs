using NUnit.Framework;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9ProductionCatalogCoverageTests
    {
        [Test]
        public void CatalogCoversCurrentGameplayKeys()
        {
            var inventory = P9GameplayPresentationInventory.Create();
            var catalog = P9ProductionCatalog.Create();
            var result = P9ProductionCatalog.Validate(catalog);
            Assert.That(inventory.Count, Is.EqualTo(19));
            Assert.That(catalog.Count, Is.EqualTo(inventory.Count));
            Assert.That(result.Passed, Is.True, result.Message);
        }
    }
}
