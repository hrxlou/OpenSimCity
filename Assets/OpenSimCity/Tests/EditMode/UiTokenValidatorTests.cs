using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UiTokenValidatorTests
    {
        private const string TokensPath = "Tokens.uss";
        private const string ComponentsPath = "Components.uss";

        private static string TokensSourceWithAllRequired(string extra = "")
        {
            var body = string.Join(
                "\n",
                UiTokenValidator.RequiredTokens.Select(token => $"    {token}: #FFFFFF;"));
            return ":root {\n" + body + "\n" + extra + "\n}";
        }

        [Test]
        public void CommittedStyleSheetsPassValidation()
        {
            var issues = new List<string>();
            UiTokenValidator.Validate(issues);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void MissingRequiredTokenIsReported()
        {
            var missing = UiTokenValidator.RequiredTokens[0];
            var source = TokensSourceWithAllRequired()
                .Replace($"    {missing}: #FFFFFF;\n", string.Empty);

            var issues = new List<string>();
            UiTokenValidator.ValidateSources(source, ".osc-x { }", TokensPath, ComponentsPath, issues);

            Assert.That(issues, Has.Exactly(1).Contains(missing));
        }

        [Test]
        public void DuplicateTokenIsReportedOnce()
        {
            var duplicated = UiTokenValidator.RequiredTokens[0];
            var source = TokensSourceWithAllRequired($"    {duplicated}: #000000;");

            var issues = new List<string>();
            UiTokenValidator.ValidateSources(source, ".osc-x { }", TokensPath, ComponentsPath, issues);

            Assert.That(issues, Has.Exactly(1).Contains(duplicated));
            Assert.That(issues.Single(), Does.Contain("defined more than once"));
        }

        [Test]
        public void ComponentUsingUndefinedTokenIsReported()
        {
            var components = ".osc-x { color: var(--osc-color-nonexistent); }";

            var issues = new List<string>();
            UiTokenValidator.ValidateSources(
                TokensSourceWithAllRequired(), components, TokensPath, ComponentsPath, issues);

            Assert.That(issues, Has.Exactly(1).Contains("--osc-color-nonexistent"));
        }

        [Test]
        public void CommentedOutTokenDoesNotCountAsDefined()
        {
            var required = UiTokenValidator.RequiredTokens[0];
            var source = TokensSourceWithAllRequired()
                .Replace($"    {required}: #FFFFFF;", $"    /* {required}: #FFFFFF; */");

            var issues = new List<string>();
            UiTokenValidator.ValidateSources(source, ".osc-x { }", TokensPath, ComponentsPath, issues);

            Assert.That(issues, Has.Exactly(1).Contains(required));
        }
    }
}
