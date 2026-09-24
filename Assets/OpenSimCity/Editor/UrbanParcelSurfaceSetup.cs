using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>Builds reusable PBR block surfaces from the user's packed v05 maps.</summary>
    public static class UrbanParcelSurfaceSetup
    {
        public const string ResourceFolder =
            "Assets/OpenSimCity/Resources/OpenSimCity/Environment/Materials";
        private const string TextureRoot =
            "Assets/OpenSimCity/Content/Art/Yeouido/Textures";

        public static void Configure()
        {
            EnsureFolder(ResourceFolder);
            Create("UrbanParcelConcrete", "Concrete", new Color(0.58f, 0.60f, 0.57f, 1f), 3.2f, 0.13f);
            Create("UrbanParcelAsphalt", "Asphalt", new Color(0.58f, 0.59f, 0.57f, 1f), 3.8f, 0.08f);
            Create("UrbanParcelGranite", "Granite", new Color(0.56f, 0.55f, 0.51f, 1f), 4.5f, 0.18f);
            AssetDatabase.SaveAssets();
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            foreach (var name in new[] { "UrbanParcelConcrete", "UrbanParcelAsphalt", "UrbanParcelGranite" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{ResourceFolder}/{name}.mat");
                if (material == null || material.GetTexture("_BaseMap") == null ||
                    material.GetTexture("_BumpMap") == null)
                    issues.Add($"PBR city-block material '{name}' is missing or incomplete.");
            }
            return issues.AsReadOnly();
        }

        private static void Create(
            string name,
            string surface,
            Color tint,
            float tiling,
            float smoothness)
        {
            var basePath = $"{TextureRoot}/{surface}/BaseColor.jpg";
            var normalPath = $"{TextureRoot}/{surface}/NormalGL.png";
            ConfigureNormal(normalPath);
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (baseMap == null || normal == null)
                return;

            var path = $"{ResourceFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTextureScale("_BumpMap", Vector2.one * tiling);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", 0.42f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            UrbanRoughnessSetup.Apply(material, surface);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureNormal(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap)
                return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
