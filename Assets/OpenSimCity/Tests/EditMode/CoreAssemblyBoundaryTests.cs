using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class CoreAssemblyBoundaryTests
    {
        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string[] references;
            public bool noEngineReferences;
        }

        [Test]
        public void CoreAssemblyHasNoRuntimeOrUnityEngineReferences()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            var coreFolder = Path.Combine(projectRoot, "Assets", "OpenSimCity", "Runtime", "Core");
            var asmdefPath = Path.Combine(coreFolder, "OpenSimCity.Core.asmdef");
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(asmdefPath));

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.noEngineReferences, Is.True);
            Assert.That(definition.references, Is.Not.Null);
            Assert.That(definition.references, Has.Length.EqualTo(0));

            foreach (var sourcePath in Directory.GetFiles(coreFolder, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(
                    source,
                    Does.Not.Contain("UnityEngine"),
                    $"Core source must remain independent of UnityEngine: {sourcePath}");
            }
        }
    }
}
