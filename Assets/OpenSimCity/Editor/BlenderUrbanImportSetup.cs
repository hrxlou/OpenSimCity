using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    public static class BlenderUrbanImportSetup
    {
        public const string ModelFolder = "Assets/OpenSimCity/Content/Art/UrbanBuildings";
        public const string PrefabFolder = "Assets/OpenSimCity/Content/Prefabs/ProductionArt/Blender";

        private static readonly IReadOnlyDictionary<string, string> KeyToModel =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["building.residential.low.a"] = "UrbanResidentialA",
                ["building.residential.low.b"] = "UrbanResidentialB",
                ["building.residential.low.c"] = "UrbanResidentialC",
                ["building.commercial.low.a"] = "UrbanCommercialA",
                ["building.commercial.low.b"] = "UrbanCommercialB",
                ["building.commercial.low.c"] = "UrbanCommercialC",
                ["building.industrial.low.a"] = "UrbanIndustrialA",
                ["building.industrial.low.b"] = "UrbanIndustrialB",
                ["building.industrial.low.c"] = "UrbanIndustrialC",
                ["utility.power.plant.placeholder"] = "UrbanPowerPlant"
            };

        [MenuItem("OpenSimCity/Setup/Import Blender Urban Set")]
        public static void Configure()
        {
            EnsureFolder(PrefabFolder);
            ConfigureTexture("brick_wall_001", "brick_wall_001_nor_gl.jpg", true);
            ConfigureTexture("concrete_wall_003", "concrete_wall_003_nor_gl.jpg", true);
            ConfigureTexture("factory_wall", "factory_wall_nor_gl.jpg", true);
            ConfigureTexture("brick_wall_001", "brick_wall_001_AO.jpg", false);
            ConfigureTexture("concrete_wall_003", "concrete_wall_003_AO.jpg", false);
            ConfigureTexture("factory_wall", "factory_wall_AO.jpg", false);

            foreach (var modelName in KeyToModel.Values)
                ConfigureModel(modelName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Imported {KeyToModel.Count} Blender MCP urban buildings with explicit URP materials.");
        }

        public static GameObject LoadPrefabForKey(string presentationKey)
        {
            return KeyToModel.TryGetValue(presentationKey, out var modelName)
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{modelName}.prefab")
                : null;
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            foreach (var pair in KeyToModel)
            {
                var prefab = LoadPrefabForKey(pair.Key);
                if (prefab == null)
                {
                    issues.Add($"Blender urban prefab for '{pair.Key}' is missing.");
                    continue;
                }
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length != 1)
                    issues.Add($"'{prefab.name}' must use one combined renderer; found {renderers.Length}.");
                if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    issues.Add($"'{prefab.name}' must remain behaviour-free.");
                if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                    issues.Add($"'{prefab.name}' must not contain colliders.");
                if (renderers.Length == 1)
                {
                    var bounds = renderers[0].bounds;
                    if (Mathf.Max(bounds.size.x, bounds.size.z) > 0.91f || bounds.min.y < -0.001f)
                        issues.Add($"'{prefab.name}' is not footprint-normalized: {bounds}.");
                    if (Mathf.Max(bounds.size.x, bounds.size.z) < 0.40f || bounds.size.y < 0.30f)
                        issues.Add($"'{prefab.name}' is implausibly small after FBX import: {bounds}.");
                    if (bounds.size.y <= Mathf.Max(bounds.size.x, bounds.size.z) * 0.25f)
                        issues.Add($"'{prefab.name}' appears flattened or axis-converted incorrectly: {bounds}.");
                }
            }
            return issues.AsReadOnly();
        }

        private static void ConfigureModel(string modelName)
        {
            var modelPath = $"{ModelFolder}/{modelName}.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Blender FBX export is missing at '{modelPath}'.");
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = new GameObject(modelName);
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform);
                model.name = "Building";
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(MapMaterial).ToArray();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);

                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    throw new InvalidOperationException($"Imported model '{modelName}' has no renderer.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var scale = 0.88f / Mathf.Max(bounds.size.x, bounds.size.z);
                // Blender FBX imports carry a model-root unit scale (typically
                // 100 for metre conversion). Replacing it made the saved prefab
                // another 100x smaller in standalone builds. Apply the footprint
                // normalization relative to the imported transform instead.
                model.transform.localScale = Vector3.Scale(
                    model.transform.localScale,
                    Vector3.one * scale);
                model.transform.localPosition = new Vector3(
                    -bounds.center.x * scale,
                    -bounds.min.y * scale,
                    -bounds.center.z * scale);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{modelName}.prefab");
                Debug.Log($"Blender city import '{modelName}': bounds={bounds.size}, normalization={scale:0.0000}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material MapMaterial(Material source)
        {
            if (source == null) throw new InvalidOperationException("FBX has an empty material slot.");
            var name = source.name;
            var path = $"{ModelFolder}/MAT_{name}.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                result = new Material(shader) { name = $"MAT_{name}" };
                AssetDatabase.CreateAsset(result, path);
            }

            var textureId = name.Contains("brick_wall", StringComparison.OrdinalIgnoreCase)
                ? "brick_wall_001"
                : name.Contains("concrete_wall", StringComparison.OrdinalIgnoreCase)
                    ? "concrete_wall_003"
                    : name.Contains("factory_wall", StringComparison.OrdinalIgnoreCase)
                        ? "factory_wall"
                        : null;
            var glass = name.Contains("Glass", StringComparison.OrdinalIgnoreCase);
            var metal = name.Contains("Metal", StringComparison.OrdinalIgnoreCase);
            result.SetColor("_BaseColor", textureId != null ? Color.white : source.color);
            result.SetFloat("_Smoothness", glass ? 0.72f : metal ? 0.48f : 0.16f);
            result.SetFloat("_Metallic", glass ? 0.18f : metal ? 0.62f : 0f);
            if (textureId != null)
            {
                var root = $"Assets/ThirdParty/PolyHaven/{textureId}/";
                result.SetTexture("_BaseMap", LoadTexture(root, textureId + "_Diffuse.jpg"));
                result.SetTexture("_BumpMap", LoadTexture(root, textureId + "_nor_gl.jpg"));
                result.SetTexture("_OcclusionMap", LoadTexture(root, textureId + "_AO.jpg"));
                result.SetFloat("_BumpScale", 0.65f);
                result.SetFloat("_OcclusionStrength", 0.75f);
                result.EnableKeyword("_NORMALMAP");
                result.EnableKeyword("_OCCLUSIONMAP");
            }
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Texture2D LoadTexture(string root, string fileName)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + fileName);
            if (texture == null)
                throw new InvalidOperationException($"Poly Haven texture is missing: '{root + fileName}'.");
            return texture;
        }

        private static void ConfigureTexture(string assetId, string fileName, bool normal)
        {
            var path = $"Assets/ThirdParty/PolyHaven/{assetId}/{fileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Poly Haven texture is missing at '{path}'.");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !fileName.Contains("_AO", StringComparison.Ordinal);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
