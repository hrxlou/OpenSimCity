using NUnit.Framework;
using OpenSimCity.Editor;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class VehicleArtSetupTests
    {
        [Test]
        public void VehicleArtIsLoadableCompactAndBehaviourFree()
        {
            Assert.That(VehicleArtSetup.Validate(), Is.Empty);

            foreach (var path in new[]
                     {
                         VehicleArtSetup.TrafficResourcePath,
                         VehicleArtSetup.BusResourcePath
                     })
            {
                var prefab = Resources.Load<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(1));
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                var meshFilter = renderers[0].GetComponent<MeshFilter>();
                Assert.That(meshFilter, Is.Not.Null);
                Assert.That(meshFilter.sharedMesh.subMeshCount, Is.GreaterThanOrEqualTo(3));
            }
        }
    }
}
