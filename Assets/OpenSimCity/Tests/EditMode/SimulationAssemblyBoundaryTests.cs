using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SimulationAssemblyBoundaryTests
    {
        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
            public bool noEngineReferences;
        }

        [Test]
        public void SimulationAssemblyAndWorldSourcesRemainUnityIndependent()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            var simulationFolder = Path.Combine(projectRoot, "Assets", "OpenSimCity", "Runtime", "Simulation");
            var asmdefPath = Path.Combine(simulationFolder, "OpenSimCity.Simulation.asmdef");
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(asmdefPath));

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.noEngineReferences, Is.True);
            Assert.That(definition.references, Is.EqualTo(new[] { "OpenSimCity.Core" }));

            foreach (var sourcePath in Directory.GetFiles(simulationFolder, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(
                    source,
                    Does.Not.Contain("UnityEngine"),
                    $"Simulation source must remain independent of UnityEngine: {sourcePath}");
            }
        }
    }
}
