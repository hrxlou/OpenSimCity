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
                    issues.Add($"Presentation key '{binding.PresentationKey}' must use authored prefab '{expectedName}'.");
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
                ["residential"] = EnsureMaterial("MAT_Placeholder_Residential", new Color(0.50f, 0.43f, 0.36f), 0.14f),
                ["commercial"] = EnsureMaterial("MAT_Placeholder_Commercial", new Color(0.31f, 0.43f, 0.46f), 0.18f),
                ["industrial"] = EnsureMaterial("MAT_Placeholder_Industrial", new Color(0.46f, 0.39f, 0.28f), 0.12f),
                ["wall"] = EnsureMaterial("MAT_Placeholder_Wall", new Color(0.64f, 0.61f, 0.54f), 0.10f),
                ["concrete"] = EnsureMaterial("MAT_Placeholder_Concrete", new Color(0.45f, 0.47f, 0.44f), 0.07f),
                ["roof"] = EnsureMaterial("MAT_Placeholder_Roof", new Color(0.20f, 0.24f, 0.25f), 0.08f),
                ["glass"] = EnsureMaterial("MAT_Placeholder_Glass", new Color(0.075f, 0.20f, 0.23f), 0.62f),
                ["trim"] = EnsureMaterial("MAT_Placeholder_Trim", new Color(0.73f, 0.70f, 0.63f), 0.15f),
                ["accent"] = EnsureMaterial("MAT_Placeholder_Accent", new Color(0.64f, 0.27f, 0.20f), 0.18f),
                ["construction"] = EnsureMaterial("MAT_Placeholder_Construction", new Color(0.78f, 0.52f, 0.18f), 0.14f),
                ["utility"] = EnsureMaterial("MAT_Placeholder_Utility", new Color(0.35f, 0.39f, 0.43f), 0.18f),
                ["road"] = EnsureMaterial("MAT_Placeholder_Road", new Color(0.105f, 0.125f, 0.13f), 0.08f),
                ["marking"] = EnsureMaterial("MAT_Placeholder_RoadMarking", new Color(0.76f, 0.65f, 0.34f), 0.12f)
            };
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness)
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
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
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
            AddBox(root, "Plinth", new Vector3(0f, 0.04f, 0f), new Vector3(0.86f, 0.08f, 0.82f), materials["concrete"]);
            AddBox(root, "House", new Vector3(0f, 0.29f, 0.02f), new Vector3(0.76f, 0.50f, 0.70f), materials["residential"]);
            AddBox(root, "Roof", new Vector3(0f, 0.58f, 0.02f), new Vector3(0.84f, 0.08f, 0.78f), materials["roof"]);
            AddBox(root, "Entry", new Vector3(0f, 0.18f, -0.345f), new Vector3(0.16f, 0.28f, 0.035f), materials["accent"]);
            AddWindowRow(root, "FrontWindow", 0.37f, -0.365f, 3, 0.48f, materials["glass"]);
            AddBox(root, "RooftopRoom", new Vector3(0.18f, 0.68f, 0.08f), new Vector3(0.23f, 0.14f, 0.24f), materials["wall"]);
        }

        private static void BuildResidentialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "LowerHouse", new Vector3(-0.15f, 0.31f, 0f), new Vector3(0.58f, 0.62f, 0.72f), materials["wall"]);
            AddBox(root, "UpperHouse", new Vector3(0.22f, 0.39f, 0.04f), new Vector3(0.34f, 0.78f, 0.60f), materials["residential"]);
            AddBox(root, "Roof", new Vector3(-0.15f, 0.65f, 0f), new Vector3(0.64f, 0.07f, 0.78f), materials["roof"]);
            AddBox(root, "Balcony", new Vector3(0.21f, 0.42f, -0.31f), new Vector3(0.30f, 0.05f, 0.12f), materials["trim"]);
            AddBox(root, "Entry", new Vector3(-0.16f, 0.16f, -0.37f), new Vector3(0.17f, 0.30f, 0.04f), materials["accent"]);
            AddWindowRow(root, "DuplexWindow", 0.42f, -0.365f, 2, 0.30f, materials["glass"]);
        }

        private static void BuildResidentialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Podium", new Vector3(0f, 0.16f, 0f), new Vector3(0.82f, 0.32f, 0.78f), materials["wall"]);
            AddBox(root, "Apartments", new Vector3(0f, 0.66f, 0.04f), new Vector3(0.62f, 1.00f, 0.60f), materials["residential"]);
            AddBox(root, "WindowBandA", new Vector3(0f, 0.42f, -0.315f), new Vector3(0.50f, 0.08f, 0.025f), materials["glass"]);
            AddBox(root, "WindowBandB", new Vector3(0f, 0.66f, -0.315f), new Vector3(0.50f, 0.08f, 0.025f), materials["glass"]);
            AddBox(root, "WindowBandC", new Vector3(0f, 0.90f, -0.315f), new Vector3(0.50f, 0.08f, 0.025f), materials["glass"]);
            AddBox(root, "Rooftop", new Vector3(0f, 1.20f, 0.04f), new Vector3(0.30f, 0.08f, 0.30f), materials["roof"]);
            AddCylinder(root, "WaterTank", new Vector3(0.16f, 1.31f, 0.05f), new Vector3(0.12f, 0.10f, 0.12f), materials["concrete"]);
        }

        private static void BuildCommercialA(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Shop", new Vector3(0f, 0.27f, 0f), new Vector3(0.84f, 0.54f, 0.72f), materials["wall"]);
            AddBox(root, "Storefront", new Vector3(0f, 0.19f, -0.37f), new Vector3(0.66f, 0.25f, 0.035f), materials["glass"]);
            AddBox(root, "Awning", new Vector3(0f, 0.36f, -0.40f), new Vector3(0.72f, 0.08f, 0.11f), materials["accent"]);
            AddBox(root, "Sign", new Vector3(0.22f, 0.48f, -0.375f), new Vector3(0.28f, 0.10f, 0.025f), materials["commercial"]);
            AddBox(root, "RoofTrim", new Vector3(0f, 0.57f, 0f), new Vector3(0.90f, 0.06f, 0.78f), materials["roof"]);
        }

        private static void BuildCommercialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Office", new Vector3(0f, 0.46f, 0f), new Vector3(0.74f, 0.92f, 0.66f), materials["commercial"]);
            AddBox(root, "Lobby", new Vector3(0f, 0.16f, -0.345f), new Vector3(0.46f, 0.26f, 0.035f), materials["glass"]);
            AddBox(root, "WindowBandA", new Vector3(0f, 0.44f, -0.345f), new Vector3(0.60f, 0.10f, 0.025f), materials["glass"]);
            AddBox(root, "WindowBandB", new Vector3(0f, 0.67f, -0.345f), new Vector3(0.60f, 0.10f, 0.025f), materials["glass"]);
            AddBox(root, "Sign", new Vector3(0.25f, 0.84f, -0.35f), new Vector3(0.22f, 0.09f, 0.025f), materials["accent"]);
            AddBox(root, "Cornice", new Vector3(0f, 0.95f, 0f), new Vector3(0.82f, 0.06f, 0.74f), materials["trim"]);
        }

        private static void BuildCommercialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "CornerStore", new Vector3(-0.14f, 0.31f, 0f), new Vector3(0.58f, 0.62f, 0.78f), materials["wall"]);
            AddBox(root, "Storefront", new Vector3(-0.14f, 0.20f, -0.405f), new Vector3(0.46f, 0.26f, 0.03f), materials["glass"]);
            AddBox(root, "Tower", new Vector3(0.24f, 0.58f, 0.10f), new Vector3(0.30f, 1.16f, 0.50f), materials["commercial"]);
            AddBox(root, "TowerGlass", new Vector3(0.24f, 0.60f, -0.16f), new Vector3(0.20f, 0.74f, 0.025f), materials["glass"]);
            AddBox(root, "Canopy", new Vector3(-0.14f, 0.38f, -0.43f), new Vector3(0.52f, 0.06f, 0.12f), materials["accent"]);
            AddBox(root, "RoofCap", new Vector3(0.24f, 1.18f, 0.10f), new Vector3(0.36f, 0.06f, 0.56f), materials["trim"]);
        }

        private static void BuildIndustrialA(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Warehouse", new Vector3(0f, 0.24f, 0f), new Vector3(0.90f, 0.48f, 0.78f), materials["industrial"]);
            AddBox(root, "LoadingDoor", new Vector3(-0.19f, 0.20f, -0.405f), new Vector3(0.32f, 0.30f, 0.035f), materials["roof"]);
            AddBox(root, "OfficeDoor", new Vector3(0.27f, 0.16f, -0.405f), new Vector3(0.16f, 0.22f, 0.035f), materials["glass"]);
            AddBox(root, "Roof", new Vector3(0f, 0.51f, 0f), new Vector3(0.94f, 0.06f, 0.82f), materials["roof"]);
            AddBox(root, "VentA", new Vector3(-0.24f, 0.58f, 0.06f), new Vector3(0.12f, 0.12f, 0.18f), materials["concrete"]);
            AddBox(root, "VentB", new Vector3(0.16f, 0.58f, 0.06f), new Vector3(0.12f, 0.12f, 0.18f), materials["concrete"]);
        }

        private static void BuildIndustrialB(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Workshop", new Vector3(-0.08f, 0.28f, 0f), new Vector3(0.72f, 0.56f, 0.74f), materials["industrial"]);
            AddBox(root, "Annex", new Vector3(0.32f, 0.18f, -0.06f), new Vector3(0.22f, 0.36f, 0.56f), materials["concrete"]);
            AddBox(root, "LoadingDoor", new Vector3(-0.12f, 0.19f, -0.385f), new Vector3(0.34f, 0.30f, 0.03f), materials["roof"]);
            AddCylinder(root, "Chimney", new Vector3(0.22f, 0.68f, 0.16f), new Vector3(0.10f, 0.48f, 0.10f), materials["roof"]);
            AddBox(root, "ChimneyCap", new Vector3(0.22f, 1.18f, 0.16f), new Vector3(0.14f, 0.05f, 0.14f), materials["accent"]);
        }

        private static void BuildIndustrialC(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Depot", new Vector3(-0.06f, 0.22f, 0f), new Vector3(0.82f, 0.44f, 0.84f), materials["industrial"]);
            AddBox(root, "Roof", new Vector3(-0.06f, 0.47f, 0f), new Vector3(0.88f, 0.06f, 0.90f), materials["roof"]);
            AddCylinder(root, "Tank", new Vector3(0.28f, 0.48f, 0.16f), new Vector3(0.18f, 0.36f, 0.18f), materials["concrete"]);
            AddBox(root, "TankBand", new Vector3(0.28f, 0.50f, -0.03f), new Vector3(0.38f, 0.05f, 0.05f), materials["accent"]);
            AddBox(root, "ServiceDoor", new Vector3(-0.20f, 0.17f, -0.435f), new Vector3(0.22f, 0.28f, 0.03f), materials["roof"]);
        }

        private static void BuildConstruction(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Foundation", new Vector3(0f, 0.05f, 0f), new Vector3(0.88f, 0.10f, 0.88f), materials["construction"]);
            AddBox(root, "FrameA", new Vector3(-0.34f, 0.40f, 0f), new Vector3(0.08f, 0.70f, 0.08f), materials["construction"]);
            AddBox(root, "FrameB", new Vector3(0.34f, 0.40f, 0f), new Vector3(0.08f, 0.70f, 0.08f), materials["construction"]);
        }

        private static void BuildPowerPlant(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Plant", new Vector3(0f, 0.30f, 0f), new Vector3(0.84f, 0.60f, 0.78f), materials["utility"]);
            AddBox(root, "TurbineHall", new Vector3(-0.18f, 0.66f, 0.03f), new Vector3(0.36f, 0.16f, 0.50f), materials["concrete"]);
            AddCylinder(root, "StackA", new Vector3(-0.24f, 0.85f, 0.14f), new Vector3(0.09f, 0.55f, 0.09f), materials["roof"]);
            AddCylinder(root, "StackB", new Vector3(0.24f, 0.75f, 0.14f), new Vector3(0.09f, 0.45f, 0.09f), materials["roof"]);
            AddBox(root, "StackStripeA", new Vector3(-0.24f, 1.12f, 0.14f), new Vector3(0.19f, 0.05f, 0.19f), materials["accent"]);
            AddBox(root, "StackStripeB", new Vector3(0.24f, 0.98f, 0.14f), new Vector3(0.19f, 0.05f, 0.19f), materials["accent"]);
            AddBox(root, "ControlWindows", new Vector3(0f, 0.28f, -0.405f), new Vector3(0.48f, 0.16f, 0.03f), materials["glass"]);
        }

        private static void BuildRoad(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            AddBox(root, "Surface", new Vector3(0f, 0.025f, 0f), new Vector3(1.02f, 0.05f, 1.02f), materials["road"]);
            AddBox(root, "SidewalkNorth", new Vector3(0f, 0.052f, 0.425f), new Vector3(1.02f, 0.045f, 0.15f), materials["concrete"]);
            AddBox(root, "SidewalkEast", new Vector3(0.425f, 0.052f, 0f), new Vector3(0.15f, 0.045f, 1.02f), materials["concrete"]);
            AddBox(root, "SidewalkSouth", new Vector3(0f, 0.052f, -0.425f), new Vector3(1.02f, 0.045f, 0.15f), materials["concrete"]);
            AddBox(root, "SidewalkWest", new Vector3(-0.425f, 0.052f, 0f), new Vector3(0.15f, 0.045f, 1.02f), materials["concrete"]);
            AddBox(root, "KerbNorth", new Vector3(0f, 0.055f, 0.46f), new Vector3(1.02f, 0.06f, 0.045f), materials["concrete"]);
            AddBox(root, "KerbEast", new Vector3(0.46f, 0.055f, 0f), new Vector3(0.045f, 0.06f, 1.02f), materials["concrete"]);
            AddBox(root, "KerbSouth", new Vector3(0f, 0.055f, -0.46f), new Vector3(1.02f, 0.06f, 0.045f), materials["concrete"]);
            AddBox(root, "KerbWest", new Vector3(-0.46f, 0.055f, 0f), new Vector3(0.045f, 0.06f, 1.02f), materials["concrete"]);
            AddBox(root, "LaneNorth", new Vector3(0f, 0.061f, 0.25f), new Vector3(0.035f, 0.008f, 0.48f), materials["marking"]);
            AddBox(root, "LaneEast", new Vector3(0.25f, 0.061f, 0f), new Vector3(0.48f, 0.008f, 0.035f), materials["marking"]);
            AddBox(root, "LaneSouth", new Vector3(0f, 0.061f, -0.25f), new Vector3(0.035f, 0.008f, 0.48f), materials["marking"]);
            AddBox(root, "LaneWest", new Vector3(-0.25f, 0.061f, 0f), new Vector3(0.48f, 0.008f, 0.035f), materials["marking"]);
            AddBox(root, "AvenueMedianNS", new Vector3(0f, 0.070f, 0f), new Vector3(0.045f, 0.025f, 1.02f), materials["concrete"]).SetActive(false);
            AddBox(root, "AvenueMedianEW", new Vector3(0f, 0.070f, 0f), new Vector3(1.02f, 0.025f, 0.045f), materials["concrete"]).SetActive(false);
            AddBox(root, "AvenueLaneNSLeft", new Vector3(-0.19f, 0.064f, 0f), new Vector3(0.025f, 0.008f, 0.66f), materials["marking"]).SetActive(false);
            AddBox(root, "AvenueLaneNSRight", new Vector3(0.19f, 0.064f, 0f), new Vector3(0.025f, 0.008f, 0.66f), materials["marking"]).SetActive(false);
            AddBox(root, "AvenueLaneEWNorth", new Vector3(0f, 0.064f, 0.19f), new Vector3(0.66f, 0.008f, 0.025f), materials["marking"]).SetActive(false);
            AddBox(root, "AvenueLaneEWSouth", new Vector3(0f, 0.064f, -0.19f), new Vector3(0.66f, 0.008f, 0.025f), materials["marking"]).SetActive(false);
        }

        private static void AddWindowRow(
            GameObject root,
            string name,
            float y,
            float z,
            int count,
            float span,
            Material material)
        {
            var start = count <= 1 ? 0f : -span * 0.5f;
            var step = count <= 1 ? 0f : span / (count - 1);
            for (var index = 0; index < count; index++)
            {
                AddBox(
                    root,
                    $"{name}{index + 1}",
                    new Vector3(start + step * index, y, z),
                    new Vector3(0.12f, 0.13f, 0.025f),
                    material);
            }
        }

        private static void AddCylinder(
            GameObject root,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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

        private static GameObject AddBox(
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
            return child;
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
