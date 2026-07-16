using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UiAssemblyBoundaryTests
    {
        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
        }

        private static readonly string[] AllowedUiReferences =
        {
            "OpenSimCity.Core",
            "OpenSimCity.Application"
        };

        private static string UiFolder => Path.Combine(
            Directory.GetParent(UnityEngine.Application.dataPath).FullName,
            "Assets", "OpenSimCity", "Runtime", "UI");

        [Test]
        public void UiAssemblyReferencesOnlyCoreAndApplication()
        {
            var asmdefPath = Path.Combine(UiFolder, "OpenSimCity.UI.asmdef");
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(asmdefPath));

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.references, Is.Not.Null);
            Assert.That(
                definition.references,
                Is.SubsetOf(AllowedUiReferences),
                "UI must not reference Simulation, Presentation or Gameplay. It renders Application " +
                "queries and must not reach back into implementations.");
        }

        [Test]
        public void UiSourcesDoNotReferenceSimulationPresentationOrGameplay()
        {
            var forbiddenNamespaces = new[]
            {
                "OpenSimCity.Simulation",
                "OpenSimCity.Presentation",
                "OpenSimCity.Gameplay"
            };

            foreach (var sourcePath in Directory.GetFiles(UiFolder, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var forbidden in forbiddenNamespaces)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbidden),
                        $"UI source must stay independent of {forbidden}: {sourcePath}");
                }
            }
        }

        [Test]
        public void ApplicationInputBoundaryStaysFreeOfUnityEngine()
        {
            // The boundary is shared with Gameplay through Application, which is a
            // pure assembly. A UnityEngine type here would break that guarantee.
            var inputFolder = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "Assets", "OpenSimCity", "Runtime", "Application", "Input");

            Assert.That(Directory.Exists(inputFolder), Is.True);

            foreach (var sourcePath in Directory.GetFiles(inputFolder, "*.cs", SearchOption.AllDirectories))
            {
                Assert.That(
                    File.ReadAllText(sourcePath),
                    Does.Not.Contain("UnityEngine"),
                    $"Application input boundary must remain engine independent: {sourcePath}");
            }
        }
    }
}
