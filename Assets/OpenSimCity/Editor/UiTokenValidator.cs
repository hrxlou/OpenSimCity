using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenSimCity.UI.Foundation;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Checks the USS design tokens without changing anything.
    /// <para>
    /// Guards three failures that are cheap to make and expensive to find later: a
    /// token named in <see cref="RequiredTokens"/> going missing, the same token
    /// being defined twice (last definition silently wins), and component USS
    /// referencing a variable that is never defined.
    /// </para>
    /// </summary>
    public static class UiTokenValidator
    {
        /// <summary>
        /// Tokens the runtime and its shells rely on. The color entries mirror
        /// Docs/DESIGN.md section 3.2; the rest back the shared component classes.
        /// </summary>
        public static readonly string[] RequiredTokens =
        {
            "--osc-color-canvas",
            "--osc-color-panel",
            "--osc-color-elevated",
            "--osc-color-border",
            "--osc-color-text-primary",
            "--osc-color-text-secondary",
            "--osc-color-accent",
            "--osc-color-info",
            "--osc-color-success",
            "--osc-color-warning",
            "--osc-color-danger",
            "--osc-color-special",
            "--osc-state-hover",
            "--osc-state-selected",
            "--osc-state-disabled-opacity",
            "--osc-state-focus-ring",
            "--osc-state-focus-ring-width",
            "--osc-space-0",
            "--osc-space-1",
            "--osc-space-2",
            "--osc-space-3",
            "--osc-space-4",
            "--osc-space-5",
            "--osc-space-6",
            "--osc-border-width",
            "--osc-radius-sm",
            "--osc-radius-md",
            "--osc-radius-lg",
            "--osc-font-size-caption",
            "--osc-font-size-body",
            "--osc-font-size-body-strong",
            "--osc-font-size-title",
            "--osc-font-size-display",
            "--osc-tool-rail-width",
            "--osc-build-palette-height",
            "--osc-context-inspector-width"
        };

        private static readonly Regex DefinitionPattern =
            new Regex(@"^\s*(--[a-z0-9-]+)\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex UsagePattern =
            new Regex(@"var\(\s*(--[a-z0-9-]+)\s*\)", RegexOptions.IgnoreCase);

        private static readonly Regex CommentPattern =
            new Regex(@"/\*.*?\*/", RegexOptions.Singleline);

        public static void Validate(ICollection<string> issues)
        {
            var tokensPath = UiFoundationNames.TokensStyleSheetPath;
            var componentsPath = UiFoundationNames.ComponentsStyleSheetPath;

            if (!File.Exists(tokensPath))
            {
                issues.Add($"UI token stylesheet is missing at '{tokensPath}'. Run Configure Project.");
                return;
            }

            if (!File.Exists(componentsPath))
            {
                issues.Add($"UI component stylesheet is missing at '{componentsPath}'. Run Configure Project.");
                return;
            }

            ValidateSources(
                File.ReadAllText(tokensPath),
                File.ReadAllText(componentsPath),
                tokensPath,
                componentsPath,
                issues);
        }

        /// <summary>
        /// Validates stylesheet text directly. Separate from <see cref="Validate"/>
        /// so tests can prove the rules fire without writing USS files to disk.
        /// </summary>
        public static void ValidateSources(
            string tokensSource,
            string componentsSource,
            string tokensPath,
            string componentsPath,
            ICollection<string> issues)
        {
            var tokens = StripComments(tokensSource);
            var components = StripComments(componentsSource);

            var defined = ValidateDefinitions(tokens, tokensPath, issues);
            ValidateUsages(components, componentsPath, defined, issues);
            ValidateUsages(tokens, tokensPath, defined, issues);
        }

        private static HashSet<string> ValidateDefinitions(
            string tokensSource, string tokensPath, ICollection<string> issues)
        {
            var defined = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();

            foreach (Match match in DefinitionPattern.Matches(tokensSource))
            {
                var token = match.Groups[1].Value;
                if (!defined.Add(token))
                    duplicates.Add(token);
            }

            foreach (var duplicate in duplicates.Distinct())
            {
                issues.Add(
                    $"UI token '{duplicate}' is defined more than once in '{tokensPath}'. " +
                    "The last definition silently wins, so remove the duplicate.");
            }

            foreach (var required in RequiredTokens.Where(token => !defined.Contains(token)))
                issues.Add($"Required UI token '{required}' is not defined in '{tokensPath}'.");

            return defined;
        }

        private static void ValidateUsages(
            string source, string path, HashSet<string> defined, ICollection<string> issues)
        {
            var undefined = UsagePattern.Matches(source)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Where(token => !defined.Contains(token))
                .Distinct();

            foreach (var token in undefined)
            {
                issues.Add(
                    $"'{path}' uses undefined UI token '{token}'. " +
                    $"Define it in '{UiFoundationNames.TokensStyleSheetPath}' or fix the reference.");
            }
        }

        private static string StripComments(string source) => CommentPattern.Replace(source, string.Empty);
    }
}
