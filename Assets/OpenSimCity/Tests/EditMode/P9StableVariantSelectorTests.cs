using NUnit.Framework;
using OpenSimCity.Application.Verification;
using OpenSimCity.Core;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class P9StableVariantSelectorTests
    {
        [Test]
        public void SameKeyAndStableIdSelectSameVariant()
        {
            var entry = P9ProductionCatalog.Get("building.residential.low.a");
            var id = new StableId(101UL);
            var first = P9StablePresentationVariantSelector.SelectVariant(entry, id);
            var second = P9StablePresentationVariantSelector.SelectVariant(entry, id);
            Assert.That(second, Is.EqualTo(first));
        }
    }
}
