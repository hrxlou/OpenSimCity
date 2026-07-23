using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Application.Queries;
using OpenSimCity.Presentation;
using OpenSimCity.Presentation.Roads;
using OpenSimCity.Presentation.Utilities;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Reproducibly creates the first-party primitive P3 placeholder set. These
    /// prefabs establish fixture coverage, not the production art direction.
    /// </summary>
    public static class PlaceholderArtSetup
    {
        public const string RootFolder = "Assets/OpenSimCity/Content/Art/Placeholder";
        public const string MaterialFolder = RootFolder + "/Materials";
        public const string PrefabFolder = "Assets/OpenSimCity/Content/Prefabs/Placeholder";
        public const string CatalogPath = RootFolder + "/PlaceholderArtCatalog.asset";

        private static readonly PlaceholderSeed[] Seeds =
        {
            new PlaceholderSeed("building.residential.low.a", "BLD_R_LOW_A", BuildResidentialA),
            new PlaceholderSeed("building.commercial.low.a", "BLD_C_LOW_A", BuildCommercialA),
            new PlaceholderSeed("building.industrial.low.a", "BLD_I_LOW_A", BuildIndustrialA),
            new PlaceholderSeed("building.residential.low.b", "BLD_R_LOW_B", BuildResidentialB),
            new PlaceholderSeed("building.commercial.low.b", "BLD_C_LOW_B", BuildCommercialB),
            new PlaceholderSeed("building.industrial.low.b", "BLD_I_LOW_B", BuildIndustrialB),
            new PlaceholderSeed("building.residential.low.c", "BLD_R_LOW_C", BuildResidentialC),
            new PlaceholderSeed("building.commercial.low.c", "BLD_C_LOW_C", BuildCommercialC),
            new PlaceholderSeed("building.industrial.low.c", "BLD_I_LOW_C", BuildIndustrialC),
            new PlaceholderSeed(
                BuildingViewReadModel.ConstructionPresentationKey,
                "BLD_CONSTRUCTION",
                BuildConstruction),
            new PlaceholderSeed(
                PowerProducerViewRenderer.DefaultPresentationKey,
                "UTL_POWER_PLANT",
                BuildPowerPlant),
            new PlaceholderSeed(RoadViewRenderer.RoadPresentationKey, "ROAD_STREET", BuildRoad)
        };

        [MenuItem("OpenSimCity/Setup/Configure P3 Placeholder Art")]
        public static void ConfigureAll()
        {
            BuildingDefinitionSetup.ConfigureDefinitions();
            UtilityDefinitionSetup.ConfigureDefinitions();
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            var materials = CreateMaterials();
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (var index = 0; index < Seeds.Length; index++)
            {
                var seed = Seeds[index];
                prefabs.Add(seed.PresentationKey, CreatePrefab(seed, materials));
            }

            var catalog = CreateCatalog(prefabs);
            PlacementSetup.ConfigureCityPrototype(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity P3 primitive placeholder art configured.");
        }

        [MenuItem("OpenSimCity/Setup/Validate P3 Placeholder Art")]
        public static void ValidateMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("OpenSimCity P3 placeholder art validation passed.");
                return;
            }

            for (var index = 0; index < issues.Count; index++)
                Debug.LogError($"OpenSimCity P3 placeholder art: {issues[index]}");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var definitionIssues = BuildingDefinitionSetup.Validate();
            for (var index = 0; index < definitionIssues.Count; index++)
                issues.Add(definitionIssues[index]);
            var utilityIssues = UtilityDefinitionSetup.Validate();
            for (var index = 0; index < utilityIssues.Count; index++)
                issues.Add(utilityIssues[index]);

            var catalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                issues.Add($"Placeholder art catalog is missing at '{CatalogPath}'.");
                return issues;
            }

            var expectedNames = Seeds.ToDictionary(
                seed => seed.PresentationKey,
                seed => seed.PrefabName,
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var bindings = catalog.Bindings;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.PresentationKey))
                {
                    issues.Add("Placeholder catalog contains an empty presentation key.");
                    continue;
                }
                if (!seen.Add(binding.PresentationKey))
                    issues.Add($"Placeholder presentation key '{binding.PresentationKey}' is duplicated.");
                if (!expectedNames.TryGetValue(binding.PresentationKey, out var expectedName))
                {
                    issues.Add($"Unexpected placeholder presentation key '{binding.PresentationKey}'.");
                    continue;
                }
                if (binding.Prefab == null)
                {
                    issues.Add($"Placeholder key '{binding.PresentationKey}' has no prefab.");
                    continue;
                }
                if (!string.Equals(binding.Prefab.name, expectedName, StringComparison.Ordinal))
                    issues.Add($"Placeholder key '{binding.PresentationKey}' must use prefab '{expectedName}'.");
                ValidatePrefab(binding.Prefab, issues);
            }

            foreach (var expected in expectedNames.Keys)
            {
                if (!seen.Contains(expected))
                    issues.Add($"Placeholder presentation key '{expected}' is not mapped.");
            }

            var definitions = BuildingDefinitionSetup.LoadDefinitions();
            for (var index = 0; index < definitions.Count; index++)
            {
                if (!seen.Contains(definitions[index].PresentationKey))
                {
                    issues.Add(
                        $"Building definition {definitions[index].DefinitionId} key " +
                        $"'{definitions[index].PresentationKey}' has no placeholder prefab.");
                }
            }

            return issues.AsReadOnly();
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["residential"] = EnsureMaterial("MAT_Placeholder_Residential", new Color(0.42f, 0.72f, 0.50f)),
                ["commercial"] = EnsureMaterial("MAT_Placeholder_Commercial", new Color(0.32f, 0.61f, 0.85f)),
                ["industrial"] = EnsureMaterial("MAT_Placeholder_Industrial", new Color(0.78f, 0.62f, 0.31f)),
                ["roof"] = EnsureMaterial("MAT_Placeholder_Roof", new Color(0.24f, 0.27f, 0.31f)),
                ["accent"] = EnsureMaterial("MAT_Placeholder_Accent", new Color(0.90f, 0.31f, 0.38f)),
                ["construction"] = EnsureMaterial("MAT_Placeholder_Construction", new Color(0.94f, 0.75f, 0.22f)),
                ["utility"] = EnsureMaterial("MAT_Placeholder_Utility", new Color(0.62f, 0.48f, 0.82f)),
                ["road"] = EnsureMaterial("MAT_Placeholder_Road", new Color(0.18f, 0.20f, 0.23f))
            };
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefab(
            PlaceholderSeed seed,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject(seed.PrefabName);
            try
            {
                seed.Build(root, materials);
                var path = $"{PrefabFolder}/{seed.PrefabName}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static PlaceholderArtCatalog CreateCatalog(
            IReadOnlyDictionary<string, GameObject> prefabs)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlaceholderArtCatalog>();
                catalog.name = "PlaceholderArtCatalog";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = Seeds.Length;
            for (var index = 0; index < Seeds.Length; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("presentationKey").stringValue = Seeds[index].PresentationKey;
                binding.FindPropertyRelative("prefab").objectReferenceValue =
                    prefabs[Seeds[index].PresentationKey];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ValidatePrefab(GameObject prefab, ICollection<string> issues)
        {
            if (prefab.transform.localPosition != Vector3.zero ||
                prefab.transform.localRotation != Quaternion.identity ||
                prefab.transform.localScale != Vector3.one)
                issues.Add($"Prefab '{prefab.name}' root must use zero position/rotation and scale (1,1,1).");

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                issues.Add($"Prefab '{prefab.name}' has no renderer.");
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].sharedMaterial == null)
                    issues.Add($"Prefab '{prefab.name}' renderer '{renderers[index].name}' has no material.");
            }

            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                issues.Add($"Prefab '{prefab.name}' must not contain per-view MonoBehaviours.");
        }

        private static void BuildResidentialA(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "House", new Vector3(0f, 0.22f, 0f), new Vector3(0.72f, 0.44f, 0.72f), materials["residential"]);
            AddBox(root, "Roof", new Vector3(0f, 0.49f, 0f), new Vector3(0.82f, 0.10f, 0.82f), materials["roof"]);
        }

        private static void BuildResidentialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Duplex", new Vector3(0f, 0.30f, 0f), new Vector3(0.86f, 0.60f, 0.68f), materials["residential"]);
            AddBox(root, "Roof", new Vector3(0f, 0.64f, 0f), new Vector3(0.92f, 0.08f, 0.74f), materials["roof"]);
            AddBox(root, "Entry", new Vector3(0f, 0.15f, -0.37f), new Vector3(0.18f, 0.30f, 0.06f), materials["accent"]);
        }

        private static void BuildResidentialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Apartments", new Vector3(0f, 0.48f, 0f), new Vector3(0.66f, 0.96f, 0.66f), materials["residential"]);
            AddBox(root, "Rooftop", new Vector3(0f, 1.00f, 0f), new Vector3(0.32f, 0.08f, 0.32f), materials["roof"]);
        }

        private static void BuildCommercialA(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Shop", new Vector3(0f, 0.24f, 0f), new Vector3(0.82f, 0.48f, 0.72f), materials["commercial"]);
            AddBox(root, "Awning", new Vector3(0f, 0.31f, -0.39f), new Vector3(0.70f, 0.10f, 0.08f), materials["accent"]);
        }

        private static void BuildCommercialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Office", new Vector3(0f, 0.38f, 0f), new Vector3(0.76f, 0.76f, 0.68f), materials["commercial"]);
            AddBox(root, "Sign", new Vector3(0f, 0.58f, -0.37f), new Vector3(0.50f, 0.16f, 0.06f), materials["accent"]);
        }

        private static void BuildCommercialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "CornerStore", new Vector3(-0.12f, 0.30f, 0f), new Vector3(0.58f, 0.60f, 0.76f), materials["commercial"]);
            AddBox(root, "Tower", new Vector3(0.22f, 0.48f, 0.10f), new Vector3(0.28f, 0.96f, 0.46f), materials["commercial"]);
        }

        private static void BuildIndustrialA(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Warehouse", new Vector3(0f, 0.24f, 0f), new Vector3(0.90f, 0.48f, 0.78f), materials["industrial"]);
            AddBox(root, "Roof", new Vector3(0f, 0.51f, 0f), new Vector3(0.94f, 0.06f, 0.82f), materials["roof"]);
        }

        private static void BuildIndustrialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Workshop", new Vector3(-0.08f, 0.28f, 0f), new Vector3(0.72f, 0.56f, 0.74f), materials["industrial"]);
            AddBox(root, "Chimney", new Vector3(0.30f, 0.58f, 0.18f), new Vector3(0.14f, 0.72f, 0.14f), materials["roof"]);
        }

        private static void BuildIndustrialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Depot", new Vector3(0f, 0.20f, 0f), new Vector3(0.90f, 0.40f, 0.84f), materials["industrial"]);
            AddBox(root, "Tank", new Vector3(0.24f, 0.55f, 0.18f), new Vector3(0.28f, 0.70f, 0.28f), materials["roof"]);
        }

        private static void BuildConstruction(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Foundation", new Vector3(0f, 0.05f, 0f), new Vector3(0.88f, 0.10f, 0.88f), materials["construction"]);
            AddBox(root, "FrameA", new Vector3(-0.34f, 0.40f, 0f), new Vector3(0.08f, 0.70f, 0.08f), materials["construction"]);
            AddBox(root, "FrameB", new Vector3(0.34f, 0.40f, 0f), new Vector3(0.08f, 0.70f, 0.08f), materials["construction"]);
        }

        private static void BuildPowerPlant(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Plant", new Vector3(0f, 0.28f, 0f), new Vector3(0.82f, 0.56f, 0.78f), materials["utility"]);
            AddBox(root, "StackA", new Vector3(-0.23f, 0.72f, 0.12f), new Vector3(0.16f, 0.88f, 0.16f), materials["roof"]);
            AddBox(root, "StackB", new Vector3(0.23f, 0.62f, 0.12f), new Vector3(0.16f, 0.68f, 0.16f), materials["roof"]);
        }

        private static void BuildRoad(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Surface", new Vector3(0f, 0.03f, 0f), new Vector3(0.96f, 0.06f, 0.96f), materials["road"]);
            AddBox(root, "Marking", new Vector3(0f, 0.065f, 0f), new Vector3(0.08f, 0.01f, 0.55f), materials["construction"]);
        }

        private static void AddBox(
            GameObject root,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = position;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = scale;
            var collider = child.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            child.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private readonly struct PlaceholderSeed
        {
            public PlaceholderSeed(
                string presentationKey,
                string prefabName,
                Action<GameObject, IReadOnlyDictionary<string, Material>> build)
            {
                PresentationKey = presentationKey;
                PrefabName = prefabName;
                Build = build;
            }

            public string PresentationKey { get; }
            public string PrefabName { get; }
            public Action<GameObject, IReadOnlyDictionary<string, Material>> Build { get; }
        }
    }
}
