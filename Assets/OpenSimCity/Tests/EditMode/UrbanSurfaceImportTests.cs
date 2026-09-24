using NUnit.Framework;
using OpenSimCity.Editor;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UrbanSurfaceImportTests
    {
        [Test]
        public void GeneratedCoordinateOfficeImportsWithUsableTextureCoordinates()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SeoulAssetImportSetup.ModelFolder + "/V05_Office_00.fbx");
            Assert.That(model, Is.Not.Null);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.subMeshCount, Is.GreaterThan(0));
                Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
                Assert.That(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0), Is.True,
                    "Blender Generated coordinates must be converted before rendering PBR textures in Unity.");
                Assert.That(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent), Is.True,
                    "Normal mapping requires a tangent frame.");
            }
        }

        [Test]
        public void RoughnessIsImportedAsLinearSmoothnessWithRetainedAlpha()
        {
            const string root = "Assets/OpenSimCity/Content/Art/Yeouido/Textures/";
            foreach (var surface in new[] { "Asphalt", "Concrete", "Granite", "Plaster", "Bark" })
            {
                var importer = AssetImporter.GetAtPath(root + surface + "/UrpMetallicSmoothness.png") as TextureImporter;
                Assert.That(importer, Is.Not.Null, surface);
                Assert.That(importer.sRGBTexture, Is.False, surface);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), surface);
                Assert.That(importer.alphaIsTransparency, Is.False, surface);
            }
        }

        [Test]
        public void ShadowRangeCoversCityBeyondTheCameraOffset()
        {
            var pipeline = AssetDatabase.LoadMainAssetAtPath("Assets/OpenSimCity/Settings/OpenSimCityURP.asset");
            var serialized = new SerializedObject(pipeline);
            Assert.That(serialized.FindProperty("m_ShadowDistance").floatValue, Is.GreaterThan(150f),
                "The 100-unit camera offset must not place the city inside the shadow fade boundary.");
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(UrbanEnvironmentSetup.ReflectionPath);
            Assert.That(reflection, Is.Not.Null);
            Assert.That(reflection.mipmapCount, Is.GreaterThan(1));
        }
    }
}
