using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class BuildingAssemblyBoundaryTests
    {
        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
            public bool noEngineReferences;
        }

        [Test]
        public void SimulationAndApplicationRemainIndependentFromUnityDefinitionAssets()
        {
            var runtime = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "Assets", "OpenSimCity", "Runtime");
            var simulation = Read(Path.Combine(runtime, "Simulation", "OpenSimCity.Simulation.asmdef"));
            var application = Read(Path.Combine(runtime, "Application", "OpenSimCity.Application.asmdef"));
            var definitions = Read(Path.Combine(runtime, "Definitions", "OpenSimCity.Definitions.asmdef"));

            Assert.That(simulation.noEngineReferences, Is.True);
            Assert.That(simulation.references, Does.Not.Contain("OpenSimCity.Definitions"));
            Assert.That(application.noEngineReferences, Is.True);
            Assert.That(application.references, Does.Not.Contain("OpenSimCity.Definitions"));
            Assert.That(definitions.noEngineReferences, Is.False);
            Assert.That(definitions.references, Does.Contain("OpenSimCity.Application"));

            foreach (var folder in new[] { "Simulation", "Application" })
            {
                var sources = Directory.GetFiles(Path.Combine(runtime, folder), "*.cs", SearchOption.AllDirectories);
                Assert.That(sources.Any(path => File.ReadAllText(path).Contains("using UnityEngine")), Is.False,
                    $"{folder} must remain independent from UnityEngine.");
                Assert.That(sources.Any(path => File.ReadAllText(path).Contains(": ScriptableObject")), Is.False,
                    $"{folder} must not own ScriptableObject definitions.");
            }
        }

        private static AssemblyDefinition Read(string path)
        {
            return JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
        }
    }
}
