using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Applies the approved CC0 asphalt PBR maps to the shared road surface.
    /// Road topology remains authored by RoadViewRenderer; this setup changes
    /// presentation assets only.
    /// </summary>
    public static class RoadSurfaceSetup
    {
        private const string SourceFolder = "Assets/ThirdParty/PolyHaven/asphalt_01";
        private const string MaterialPath =
            PlaceholderArtSetup.MaterialFolder + "/MAT_Placeholder_Road.mat";
        private const string DiffusePath = SourceFolder + "/asphalt_01_Diffuse.jpg";
        private const string NormalPath = SourceFolder + "/asphalt_01_nor_gl.jpg";
        private const string OcclusionPath = SourceFolder + "/asphalt_01_AO.jpg";

        public static void Configure()
        {
            ConfigureNormalImporter();

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath);
            if (material == null || diffuse == null || normal == null || occlusion == null)
            {
                Debug.LogWarning(
                    "OpenSimCity road surface setup skipped because its shared road material or " +
                    "Poly Haven asphalt maps are missing.");
                return;
            }

            SetTexture(material, "_BaseMap", diffuse);
            SetTexture(material, "_MainTex", diffuse);
            SetTexture(material, "_BumpMap", normal);
            SetTexture(material, "_OcclusionMap", occlusion);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.69f, 1f));
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(0.72f, 0.72f, 0.69f, 1f));
            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", 0.45f);
            if (material.HasProperty("_OcclusionStrength"))
                material.SetFloat("_OcclusionStrength", 0.65f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.06f);
            material.SetTextureScale("_BaseMap", new Vector2(0.65f, 0.65f));
            EditorUtility.SetDirty(material);
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath);
            if (material == null)
                issues.Add($"Road material is missing at '{MaterialPath}'.");
            if (diffuse == null || normal == null || occlusion == null)
                issues.Add($"Poly Haven asphalt PBR maps are incomplete under '{SourceFolder}'.");
            if (material != null && diffuse != null && material.GetTexture("_BaseMap") != diffuse)
                issues.Add("Road material does not reference the approved asphalt diffuse map.");
            if (material != null && normal != null && material.GetTexture("_BumpMap") != normal)
                issues.Add("Road material does not reference the approved OpenGL asphalt normal map.");
            return issues.AsReadOnly();
        }

        private static void ConfigureNormalImporter()
        {
            var importer = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap)
                return;

            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property))
                material.SetTexture(property, texture);
        }
    }
}
