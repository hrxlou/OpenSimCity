using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Builds a muted, lived-in urban product catalog from normalized first-party
    /// structures. Vendor textures remain optional surface inputs; gameplay IDs,
    /// footprints and the lightweight distance LOD stay unchanged.
    /// </summary>
    public static class ProductArtSetup
    {
        public const string ProductPrefabFolder =
            "Assets/OpenSimCity/Content/Prefabs/ProductionArt";
        public const string ProductCatalogPath =
            "Assets/OpenSimCity/Content/Art/Production/ProductArtCatalog.asset";
        public const string ProductMaterialFolder =
            "Assets/OpenSimCity/Content/Art/Production/Materials";

        private static readonly Dictionary<string, string> ProductPrefabSources =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "building.residential.low.a", "BLD_R_LOW_A" },
                { "building.residential.low.b", "BLD_R_LOW_B" },
                { "building.residential.low.c", "BLD_R_LOW_C" },
                { "building.commercial.low.a", "BLD_C_LOW_A" },
                { "building.commercial.low.b", "BLD_C_LOW_B" },
                { "building.commercial.low.c", "BLD_C_LOW_C" },
                { "building.industrial.low.a", "BLD_I_LOW_A" },
                { "building.industrial.low.b", "BLD_I_LOW_B" },
                { "building.industrial.low.c", "BLD_I_LOW_C" },
                { "utility.power.plant.placeholder", "UTL_POWER_PLANT" }
            };

        [MenuItem("OpenSimCity/Setup/Configure Product Art Catalog")]
        public static void ConfigureProductCatalog()
        {
            EnsureFolder(ProductPrefabFolder);
            EnsureFolder("Assets/OpenSimCity/Content/Art/Production");
            EnsureFolder(ProductMaterialFolder);
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(
                PlaceholderArtSetup.CatalogPath);
            if (sourceCatalog == null)
                throw new InvalidOperationException(
                    "The placeholder catalog must exist before product art integration.");

            var fallbacks = sourceCatalog.Bindings.ToDictionary(
                binding => binding.PresentationKey,
                binding => binding.Prefab,
                StringComparer.Ordinal);
            foreach (var pair in ProductPrefabSources)
            {
                var authoredFallback = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{PlaceholderArtSetup.PrefabFolder}/{pair.Value}.prefab");
                if (authoredFallback != null)
                    fallbacks[pair.Key] = authoredFallback;
            }
            var materials = CreateUrbanMaterials();
            var replacements = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var variant = 0;
            foreach (var pair in ProductPrefabSources)
            {
                if (!fallbacks.TryGetValue(pair.Key, out var fallback) || fallback == null)
                    continue;
                var blenderPrefab = SeoulAssetImportSetup.LoadBuildingPrefabForKey(pair.Key) ??
                                    BlenderUrbanImportSetup.LoadPrefabForKey(pair.Key);
                replacements[pair.Key] = blenderPrefab != null
                    ? CreateBlenderHybridPrefab(pair.Key, blenderPrefab, fallback, materials, variant++)
                    : CreateUrbanHybridPrefab(pair.Key, fallback, materials, variant++);
            }

            var productCatalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(ProductCatalogPath);
            if (productCatalog == null)
            {
                productCatalog = ScriptableObject.CreateInstance<PlaceholderArtCatalog>();
                productCatalog.name = "ProductArtCatalog";
                AssetDatabase.CreateAsset(productCatalog, ProductCatalogPath);
            }

            var serialized = new SerializedObject(productCatalog);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = sourceCatalog.Bindings.Count;
            for (var index = 0; index < sourceCatalog.Bindings.Count; index++)
            {
                var element = bindings.GetArrayElementAtIndex(index);
                var source = sourceCatalog.Bindings[index];
                var key = source.PresentationKey;
                element.FindPropertyRelative("presentationKey").stringValue = key;
                element.FindPropertyRelative("prefab").objectReferenceValue =
                    replacements.TryGetValue(key, out var replacement)
                        ? replacement
                        : source.Prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(productCatalog);
            PlacementSetup.ConfigureCityPrototype(productCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"OpenSimCity lived-in urban catalog configured with {replacements.Count} hybrid LOD buildings.");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(ProductCatalogPath);
            if (catalog == null)
            {
                issues.Add($"Product art catalog is missing at '{ProductCatalogPath}'.");
                return issues;
            }

            foreach (var pair in ProductPrefabSources)
            {
                var expectedPath = $"{ProductPrefabFolder}/{ProductPrefabName(pair.Key)}.prefab";
                var expected = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPath);
                var binding = catalog.Bindings.FirstOrDefault(candidate =>
                    string.Equals(candidate.PresentationKey, pair.Key, StringComparison.Ordinal));
                if (expected == null || binding?.Prefab != expected)
                {
                    issues.Add($"'{pair.Key}' is not bound to '{expectedPath}'.");
                    continue;
                }

                var lodGroup = expected.GetComponent<LODGroup>();
                if (lodGroup == null || lodGroup.GetLODs().Length != 2)
                    issues.Add($"Product prefab '{expected.name}' must contain two LOD levels.");
                if (expected.transform.Find("Detail") == null || expected.transform.Find("Distance") == null)
                    issues.Add($"Product prefab '{expected.name}' is missing Detail or Distance geometry.");
                if (expected.transform.Find("Detail/Building") == null)
                    issues.Add($"Product prefab '{expected.name}' is missing its Blender-authored detail building.");
                if (expected.transform.localPosition != Vector3.zero ||
                    expected.transform.localRotation != Quaternion.identity ||
                    expected.transform.localScale != Vector3.one)
                    issues.Add($"Product prefab '{expected.name}' root transform is not normalized.");
            }
            return issues;
        }

        private static Dictionary<string, Material> CreateUrbanMaterials()
        {
            const string textureRoot =
                "Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard/Textures";
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["concrete"] = EnsureUrbanMaterial(
                    "Concrete", new Color(0.92f, 0.90f, 0.84f), 0.10f,
                    textureRoot + "/T_Concrete_BaseColor.png",
                    textureRoot + "/T_Concrete_Normal.png"),
                ["brick"] = EnsureUrbanMaterial(
                    "Brick", new Color(0.88f, 0.82f, 0.76f), 0.08f,
                    textureRoot + "/T_RedBrick_BaseColor.png",
                    textureRoot + "/T_RedBrick_Normal.png"),
                ["metal"] = EnsureUrbanMaterial(
                    "Metal", new Color(0.72f, 0.75f, 0.73f), 0.28f,
                    textureRoot + "/T_MetalConcrete_BaseColor.png",
                    textureRoot + "/T_MetalConcrete_Normal.png"),
                ["glass"] = EnsureUrbanMaterial(
                    "Glass", new Color(0.075f, 0.16f, 0.18f), 0.68f, null, null),
                ["roof"] = EnsureUrbanMaterial(
                    "Roof", new Color(0.20f, 0.23f, 0.23f), 0.08f, null, null),
                ["sign"] = EnsureUrbanMaterial(
                    "Sign", new Color(0.52f, 0.20f, 0.15f), 0.16f, null, null),
                ["utility"] = EnsureUrbanMaterial(
                    "Utility", new Color(0.34f, 0.38f, 0.38f), 0.20f, null, null),
                ["safety"] = EnsureUrbanMaterial(
                    "Safety", new Color(0.72f, 0.53f, 0.17f), 0.12f, null, null)
            };
        }

        private static Material EnsureUrbanMaterial(
            string name,
            Color color,
            float smoothness,
            string baseTexturePath,
            string normalTexturePath)
        {
            var path = $"{ProductMaterialFolder}/MAT_Urban_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("No supported lit shader is available for urban materials.");
                material = new Material(shader) { name = $"MAT_Urban_{name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name == "Metal" ? 0.12f : 0f);
            var baseTexture = string.IsNullOrWhiteSpace(baseTexturePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath);
            var normalTexture = string.IsNullOrWhiteSpace(normalTexturePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseTexture);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normalTexture);
            if (normalTexture != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateBlenderHybridPrefab(
            string key,
            GameObject blenderPrefab,
            GameObject fallbackPrefab,
            IReadOnlyDictionary<string, Material> materials,
            int variant)
        {
            var root = new GameObject(ProductPrefabName(key));
            try
            {
                var detail = new GameObject("Detail");
                detail.transform.SetParent(root.transform, false);
                var building = PrefabUtility.InstantiatePrefab(blenderPrefab) as GameObject;
                var distance = PrefabUtility.InstantiatePrefab(fallbackPrefab) as GameObject;
                if (building == null || distance == null)
                    throw new InvalidOperationException($"Could not create Blender product wrapper for '{key}'.");
                building.name = "Building";
                building.transform.SetParent(detail.transform, false);
                distance.name = "Distance";
                distance.transform.SetParent(root.transform, false);
                RetoneStructure(distance, key, materials, variant);

                var detailRenderers = detail.GetComponentsInChildren<Renderer>(true);
                var distanceRenderers = distance.GetComponentsInChildren<Renderer>(true);
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;
                group.SetLODs(new[]
                {
                    new LOD(0.003f, detailRenderers),
                    new LOD(0f, distanceRenderers)
                });
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    $"{ProductPrefabFolder}/{root.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUrbanHybridPrefab(
            string key,
            GameObject fallbackPrefab,
            IReadOnlyDictionary<string, Material> materials,
            int variant)
        {
            var root = new GameObject(ProductPrefabName(key));
            try
            {
                var detail = new GameObject("Detail");
                detail.transform.SetParent(root.transform, false);
                var structure = PrefabUtility.InstantiatePrefab(fallbackPrefab) as GameObject;
                var distance = PrefabUtility.InstantiatePrefab(fallbackPrefab) as GameObject;
                if (structure == null || distance == null)
                    throw new InvalidOperationException($"Could not create urban wrapper for '{key}'.");

                structure.name = "Structure";
                structure.transform.SetParent(detail.transform, false);
                distance.name = "Distance";
                distance.transform.SetParent(root.transform, false);
                RetoneStructure(structure, key, materials, variant);
                RetoneStructure(distance, key, materials, variant);

                var urbanDetail = new GameObject("UrbanDetail");
                urbanDetail.transform.SetParent(detail.transform, false);
                AddSupportedDetails(urbanDetail, structure, materials);

                var detailRenderers = detail.GetComponentsInChildren<Renderer>(true);
                var distanceRenderers = distance.GetComponentsInChildren<Renderer>(true);
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;
                group.SetLODs(new[]
                {
                    new LOD(0.003f, detailRenderers),
                    new LOD(0f, distanceRenderers)
                });
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    $"{ProductPrefabFolder}/{root.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddSupportedDetails(
            GameObject parent, GameObject structure, IReadOnlyDictionary<string, Material> materials)
        {
            // Pick an actual broad roof surface, excluding tanks, chimneys and trim.
            var surfaces = structure.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.bounds.size.x > 0.28f && r.bounds.size.z > 0.28f)
                .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).ToArray();
            if (surfaces.Length == 0)
                throw new InvalidOperationException("Urban structure has no supporting surface.");
            var roof = surfaces.FirstOrDefault(r =>
                r.name.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.name.IndexOf("cornice", StringComparison.OrdinalIgnoreCase) >= 0) ?? surfaces[0];
            var b = roof.bounds;
            const float thickness = 0.018f;
            const float height = 0.045f;
            var y = b.max.y + height * 0.5f;
            AddBox(parent, "SupportedParapetNorth", new Vector3(b.center.x, y, b.max.z - thickness),
                new Vector3(b.size.x, height, thickness), materials["concrete"]);
            AddBox(parent, "SupportedParapetSouth", new Vector3(b.center.x, y, b.min.z + thickness),
                new Vector3(b.size.x, height, thickness), materials["concrete"]);
            AddBox(parent, "SupportedParapetEast", new Vector3(b.max.x - thickness, y, b.center.z),
                new Vector3(thickness, height, b.size.z), materials["concrete"]);
            AddBox(parent, "SupportedParapetWest", new Vector3(b.min.x + thickness, y, b.center.z),
                new Vector3(thickness, height, b.size.z), materials["concrete"]);
            var unit = new Vector3(b.center.x - b.size.x * 0.20f, b.max.y + 0.035f, b.center.z);
            AddBox(parent, "SupportedHVAC", unit, new Vector3(0.12f, 0.07f, 0.10f), materials["metal"]);
            for (var i = 0; i < 5; i++)
                AddBox(parent, "HVACGrille" + i, unit + new Vector3(-0.04f + i * 0.02f, 0.037f, 0),
                    new Vector3(0.006f, 0.004f, 0.075f), materials["roof"]);
        }

        private static void RetoneStructure(
            GameObject structure,
            string key,
            IReadOnlyDictionary<string, Material> materials,
            int variant)
        {
            var primary = key.Contains("residential", StringComparison.Ordinal)
                ? (variant % 2 == 0 ? materials["brick"] : materials["concrete"])
                : key.Contains("commercial", StringComparison.Ordinal)
                    ? materials["concrete"]
                    : key.Contains("industrial", StringComparison.Ordinal)
                        ? materials["metal"]
                        : materials["utility"];
            var renderers = structure.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var sourceName = renderers[index].sharedMaterial != null
                    ? renderers[index].sharedMaterial.name.ToLowerInvariant()
                    : string.Empty;
                renderers[index].sharedMaterial = sourceName.Contains("glass")
                    ? materials["glass"]
                    : sourceName.Contains("roof") || sourceName.Contains("loading") || sourceName.Contains("chimney")
                        ? materials["roof"]
                        : sourceName.Contains("accent") || sourceName.Contains("construction")
                            ? materials["sign"]
                            : sourceName.Contains("concrete") || sourceName.Contains("trim") || sourceName.Contains("wall")
                                ? materials["concrete"]
                                : primary;
            }
        }

        private static void AddBox(
            GameObject parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent.transform, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            Object.DestroyImmediate(value.GetComponent<Collider>());
            value.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AddCylinder(
            GameObject parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            value.name = name;
            value.transform.SetParent(parent.transform, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            Object.DestroyImmediate(value.GetComponent<Collider>());
            value.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static string ProductPrefabName(string presentationKey)
        {
            return "PRD_" + presentationKey.Replace('.', '_').ToUpperInvariant();
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
