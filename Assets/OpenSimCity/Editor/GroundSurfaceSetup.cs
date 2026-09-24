using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>Applies the approved CC0 terrain PBR maps to the playable backdrop.</summary>
    public static class GroundSurfaceSetup
    {
        private const string SourceFolder = "Assets/ThirdParty/PolyHaven/aerial_grass_rock";
        private const string DiffusePath = SourceFolder + "/aerial_grass_rock_Diffuse.png";
        private const string NormalPath = SourceFolder + "/aerial_grass_rock_nor_gl.png";
        private const string OcclusionPath = SourceFolder + "/aerial_grass_rock_AO.png";
        private const string SoilDetailPath =
            "Assets/OpenSimCity/Content/Art/Yeouido/Textures/Soil/BaseColor.jpg";

        public static void Configure()
        {
            ConfigureNormalImporter();
            var material = AssetDatabase.LoadAssetAtPath<Material>(IsometricCameraRigSetup.GroundMaterialPath);
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath);
            var soilDetail = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilDetailPath);
            if (material == null || diffuse == null || normal == null || occlusion == null) return;

            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetTextureScale("_BaseMap", new Vector2(18f, 18f));
            material.SetTextureScale("_BumpMap", new Vector2(18f, 18f));
            material.SetTextureScale("_OcclusionMap", new Vector2(18f, 18f));
            // Keep the undeveloped terrain subordinate to the city. The source
            // grass map is intentionally vivid, so this neutral olive tint
            // prevents it reading as a different asset pack beside the muted
            // Seoul asphalt, concrete and facade palette.
            material.SetColor("_BaseColor", new Color(0.56f, 0.64f, 0.56f, 1f));
            if (soilDetail != null)
            {
                // The first-party Yeouido soil map breaks up the single mossy
                // grass read at a larger scale, giving undeveloped land a mix of
                // maintained grass and exposed urban soil without a bespoke
                // terrain shader.
                material.SetTexture("_DetailAlbedoMap", soilDetail);
                material.SetTextureScale("_DetailAlbedoMap", new Vector2(4.5f, 4.5f));
                material.SetFloat("_DetailAlbedoMapScale", 0.32f);
                material.EnableKeyword("_DETAIL_SCALED");
            }
            material.SetFloat("_BumpScale", 0.28f);
            material.SetFloat("_OcclusionStrength", 0.50f);
            material.SetFloat("_Smoothness", 0.04f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(IsometricCameraRigSetup.GroundMaterialPath);
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var soilDetail = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilDetailPath);
            if (diffuse == null || normal == null || AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath) == null)
                issues.Add($"Poly Haven terrain maps are incomplete under '{SourceFolder}'.");
            if (material != null && diffuse != null && material.GetTexture("_BaseMap") != diffuse)
                issues.Add("Ground material does not reference the approved terrain diffuse map.");
            if (material != null && soilDetail != null &&
                material.GetTexture("_DetailAlbedoMap") != soilDetail)
                issues.Add("Ground material does not reference the first-party Yeouido soil detail map.");
            return issues.AsReadOnly();
        }

        private static void ConfigureNormalImporter()
        {
            var importer = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
    }
}
