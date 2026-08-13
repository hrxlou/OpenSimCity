using NUnit.Framework;
using OpenSimCity.Application.Onboarding;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class OnboardingMenuContractTests
    {
        [Test]
        public void ReleaseMenuHasOnlyOfflineActions()
        {
            CollectionAssert.AreEqual(
                new[] { ReleaseMenuAction.Continue, ReleaseMenuAction.NewCity, ReleaseMenuAction.Load, ReleaseMenuAction.Settings },
                ReleaseMenuContract.SupportedActions);
        }
    }
}
