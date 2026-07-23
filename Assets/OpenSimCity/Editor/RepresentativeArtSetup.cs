using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenSimCity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Creates and validates the OpenSimCity-owned wrappers for the pinned P3-08
    /// vendor slice. Runtime integration is intentionally limited to Benchmark.
    /// </summary>
    public static class RepresentativeArtSetup
    {
        public const string VendorRoot =
            "Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard";
        public const string VendorTextureFolder = VendorRoot + "/Textures";
        public const string ProvenanceManifestPath =
            VendorRoot + "/Provenance/acquisition-manifest.json";
        public const string ProvenanceLicensePath =
            VendorRoot + "/Provenance/LICENSE_CC0_1.0.txt";
        public const string ArtRoot =
            "Assets/OpenSimCity/Content/Art/Representative";
        public const string MaterialFolder =
            "Assets/OpenSimCity/Content/Materials/RepresentativeArt";
        public const string PrefabFolder =
            "Assets/OpenSimCity/Content/Prefabs/RepresentativeArt";
        public const string DefinitionFolder =
            "Assets/OpenSimCity/Content/Definitions/RepresentativeArt";
        public const string CatalogPath =
            ArtRoot + "/RepresentativeArtCatalog.asset";
        public const string BenchmarkScenePath =
            "Assets/OpenSimCity/Scenes/Benchmark.unity";
        public const string CityPrototypeScenePath =
            "Assets/OpenSimCity/Scenes/CityPrototype.unity";

        private const string PlaceholderCatalogPath =
            "Assets/OpenSimCity/Content/Art/Placeholder/PlaceholderArtCatalog.asset";
        private const string BenchmarkRootName = "OpenSimCity.Benchmark";
        private const float BoundsTolerance = 0.03f;

        private static readonly RepresentativeSeed[] Seeds =
        {
            new RepresentativeSeed(
                "osc.benchmark.megakit.building.small",
                "OSC_REP_BLD_SMALL",
                RepresentativeArtRole.BuildingSmall,
                "Building_Small_1.fbx",
                "OSC_REP_BLD_SMALL.fbx",
                new Vector3(12.46f, 17.026f, 14.536f),
                new Vector2Int(2, 2),
                18152,
                13,
                "building.residential.low.a"),
            new RepresentativeSeed(
                "osc.benchmark.megakit.building.medium",
                "OSC_REP_BLD_MEDIUM",
                RepresentativeArtRole.BuildingMedium,
                "Building_Medium_2_001.fbx",
                "OSC_REP_BLD_MEDIUM.fbx",
                new Vector3(15.0557f, 25.0087f, 13.0557f),
                new Vector2Int(2, 2),
                25612,
                12,
                "building.residential.low.b"),
            new RepresentativeSeed(
                "osc.benchmark.megakit.building.large",
                "OSC_REP_BLD_LARGE",
                RepresentativeArtRole.BuildingLarge,
                "Building_Large_2.fbx",
                "OSC_REP_BLD_LARGE.fbx",
                new Vector3(20.6441f, 28f, 16.6446f),
                new Vector2Int(3, 3),
                44786,
                12,
                "building.residential.low.c"),
            new RepresentativeSeed(
                "osc.benchmark.megakit.road.street-2lane",
                "OSC_REP_ROAD_2LANE",
                RepresentativeArtRole.Road,
                "Street_2Lane.fbx",
                "OSC_REP_ROAD_2LANE.fbx",
                new Vector3(6f, 0.15f, 12f),
                new Vector2Int(1, 2),
                220,
                3,
                "road.street.placeholder"),
            new RepresentativeSeed(
                "osc.benchmark.megakit.facade.brick-plain",
                "OSC_REP_FACADE_BRICK",
                RepresentativeArtRole.Facade,
                "Brick_Plain_3.fbx",
                "OSC_REP_FACADE_BRICK.fbx",
                new Vector3(2f, 3f, 0.2f),
                Vector2Int.one,
                6,
                2,
                "building.commercial.low.a"),
            new RepresentativeSeed(
                "osc.benchmark.megakit.prop.ac-unit",
                "OSC_REP_PROP_AC_UNIT",
                RepresentativeArtRole.Prop,
                "Prop_ACUnit.fbx",
                "OSC_REP_PROP_AC_UNIT.fbx",
                new Vector3(0.8932f, 0.6f, 0.3471f),
                Vector2Int.one,
                136,
                1,
                "utility.power.plant.placeholder")
        };

        private static readonly string[] TextureNames =
        {
            "T_Concrete_Asphalt_BaseColor.png",
            "T_Concrete_BaseColor.png",
            "T_Concrete_Normal.png",
            "T_dark_interior.png",
            "T_lit_interior_1.png",
            "T_lit_interior_2.png",
            "T_MarbleFloor_BaseColor.png",
            "T_MarbleFloor_Normal.png",
            "T_MetalConcrete_BaseColor.png",
            "T_MetalConcrete_Normal.png",
            "T_Ornaments_BaseColor.png",
            "T_Ornaments_Normal.png",
            "T_RedBrick_BaseColor.png",
            "T_RedBrick_Normal.png",
            "T_Street_Decals.png",
            "T_Trim_BaseColor.png",
            "T_Trim_Normal.png"
        };

        [MenuItem("OpenSimCity/Setup/Configure P3-08 Representative Art")]
        public static void ConfigureAll()
        {
            EnsureFolder(ArtRoot);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(DefinitionFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureTextureImporters();
            ConfigureModelImporters();
            var materials = CreateMaterials();
            var definitions =
                new Dictionary<string, RepresentativeArtDefinition>(StringComparer.Ordinal);
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (var index = 0; index < Seeds.Length; index++)
            {
                var seed = Seeds[index];
                definitions.Add(seed.AssetId, CreateDefinition(seed));
                prefabs.Add(seed.PrefabKey, CreateWrapperPrefab(seed, materials));
            }

            var catalog = CreateCatalog(definitions, prefabs);
            ConfigureBenchmarkScene(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "OpenSimCity P3-08 representative art configured for the Benchmark scene.");
        }

        [MenuItem("OpenSimCity/Setup/Validate P3-08 Representative Art")]
        public static void ValidateMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("OpenSimCity P3-08 representative art validation passed.");
                return;
            }

            for (var index = 0; index < issues.Count; index++)
                Debug.LogError($"OpenSimCity P3-08 representative art: {issues[index]}");
        }

        [MenuItem("OpenSimCity/Benchmark/Capture P3-08 Import Baseline")]
        public static void CaptureImportBaseline()
        {
            var modelTimer = Stopwatch.StartNew();
            for (var index = 0; index < Seeds.Length; index++)
            {
                AssetDatabase.ImportAsset(
                    Seeds[index].NormalizedModelPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            modelTimer.Stop();

            var textureTimer = Stopwatch.StartNew();
            for (var index = 0; index < TextureNames.Length; index++)
            {
                AssetDatabase.ImportAsset(
                    TexturePath(TextureNames[index]),
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            textureTimer.Stop();
            Debug.Log(
                $"P3_08_IMPORT_BASELINE models={Seeds.Length} " +
                $"modelMs={modelTimer.ElapsedMilliseconds} textures={TextureNames.Length} " +
                $"textureMs={textureTimer.ElapsedMilliseconds} " +
                $"totalMs={modelTimer.ElapsedMilliseconds + textureTimer.ElapsedMilliseconds}");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            ValidateProvenance(issues);
            ValidateTextures(issues);
            ValidateModels(issues);
            ValidateCatalogAndWrappers(issues);
            ValidateSceneBoundary(issues);
            return issues.AsReadOnly();
        }

        private static void ConfigureTextureImporters()
        {
            for (var index = 0; index < TextureNames.Length; index++)
            {
                var textureName = TextureNames[index];
                var path = TexturePath(textureName);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException($"Texture is missing at '{path}'.");

                var isNormal = textureName.EndsWith(
                    "_Normal.png",
                    StringComparison.Ordinal);
                var changed = false;
                var expectedType = isNormal
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                if (importer.textureType != expectedType)
                {
                    importer.textureType = expectedType;
                    changed = true;
                }
                if (importer.sRGBTexture == isNormal)
                {
                    importer.sRGBTexture = !isNormal;
                    changed = true;
                }
                if (!importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = true;
                    changed = true;
                }
                if (importer.maxTextureSize != 2048)
                {
                    importer.maxTextureSize = 2048;
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();
            }
        }

        private static void ConfigureModelImporters()
        {
            for (var index = 0; index < Seeds.Length; index++)
            {
                var path = Seeds[index].NormalizedModelPath;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    throw new InvalidOperationException($"Normalized FBX is missing at '{path}'.");

                var changed = false;
                if (!Mathf.Approximately(importer.globalScale, 1f))
                {
                    importer.globalScale = 1f;
                    changed = true;
                }
                if (!importer.useFileScale)
                {
                    importer.useFileScale = true;
                    changed = true;
                }
                if (importer.importAnimation)
                {
                    importer.importAnimation = false;
                    changed = true;
                }
                if (importer.addCollider)
                {
                    importer.addCollider = false;
                    changed = true;
                }
                if (!importer.bakeAxisConversion)
                {
                    importer.bakeAxisConversion = true;
                    changed = true;
                }
                if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    changed = true;
                }
                if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            var sourceNames = new HashSet<string>(StringComparer.Ordinal);
            for (var seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Seeds[seedIndex].NormalizedModelPath);
                if (model == null)
                    throw new InvalidOperationException(
                        $"Normalized model is missing at '{Seeds[seedIndex].NormalizedModelPath}'.");
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                for (var rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    var sharedMaterials = renderers[rendererIndex].sharedMaterials;
                    for (var materialIndex = 0;
                         materialIndex < sharedMaterials.Length;
                         materialIndex++)
                    {
                        var material = sharedMaterials[materialIndex];
                        if (material != null)
                            sourceNames.Add(material.name);
                    }
                }
            }

            var materials =
                new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var sourceName in sourceNames.OrderBy(value => value, StringComparer.Ordinal))
                materials.Add(sourceName, EnsureWrapperMaterial(sourceName));
            return materials;
        }

        private static Material EnsureWrapperMaterial(string sourceName)
        {
            var safeName = sourceName.StartsWith("MI_", StringComparison.Ordinal)
                ? sourceName.Substring(3)
                : sourceName;
            var path = $"{MaterialFolder}/MAT_MegaKit_{safeName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader =
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");
                material = new Material(shader) { name = $"MAT_MegaKit_{safeName}" };
                AssetDatabase.CreateAsset(material, path);
            }

            ResolveMaterialInputs(sourceName, out var baseTextureName, out var normalTextureName);
            var baseTexture = string.IsNullOrEmpty(baseTextureName)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(baseTextureName));
            var normalTexture = string.IsNullOrEmpty(normalTextureName)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(normalTextureName));
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", baseTexture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", baseTexture);
            if (material.HasProperty("_BumpMap"))
                material.SetTexture("_BumpMap", normalTexture);
            if (normalTexture != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");

            var color = ResolveMaterialColor(sourceName);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ResolveMaterialInputs(
            string sourceName,
            out string baseTexture,
            out string normalTexture)
        {
            baseTexture = null;
            normalTexture = null;
            if (sourceName.IndexOf("Asphalt", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_Concrete_Asphalt_BaseColor.png";
                normalTexture = "T_Concrete_Normal.png";
            }
            else if (sourceName.IndexOf("MetalConcrete", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_MetalConcrete_BaseColor.png";
                normalTexture = "T_MetalConcrete_Normal.png";
            }
            else if (sourceName.IndexOf("RedBrick", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_RedBrick_BaseColor.png";
                normalTexture = "T_RedBrick_Normal.png";
            }
            else if (sourceName.IndexOf("InteriorFloor", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_MarbleFloor_BaseColor.png";
                normalTexture = "T_MarbleFloor_Normal.png";
            }
            else if (sourceName.IndexOf("Ornaments", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_Ornaments_BaseColor.png";
                normalTexture = "T_Ornaments_Normal.png";
            }
            else if (sourceName.IndexOf("StreetDecals", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_Street_Decals.png";
            }
            else if (sourceName.IndexOf("FakeInterior_1", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_lit_interior_1.png";
            }
            else if (sourceName.IndexOf("FakeInterior_2", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_lit_interior_2.png";
            }
            else if (sourceName.IndexOf("FakeInterior", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_dark_interior.png";
            }
            else if (sourceName.IndexOf("Trim", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_Trim_BaseColor.png";
                normalTexture = "T_Trim_Normal.png";
            }
            else if (sourceName.IndexOf("Concrete", StringComparison.Ordinal) >= 0 ||
                     sourceName.IndexOf("InteriorWall", StringComparison.Ordinal) >= 0)
            {
                baseTexture = "T_Concrete_BaseColor.png";
                normalTexture = "T_Concrete_Normal.png";
            }
        }

        private static Color ResolveMaterialColor(string sourceName)
        {
            if (sourceName.IndexOf("Glass", StringComparison.Ordinal) >= 0)
                return new Color(0.27f, 0.42f, 0.48f, 1f);
            if (sourceName.IndexOf("Dark", StringComparison.Ordinal) >= 0)
                return new Color(0.38f, 0.38f, 0.38f, 1f);
            if (sourceName.IndexOf("Green", StringComparison.Ordinal) >= 0)
                return new Color(0.55f, 0.72f, 0.55f, 1f);
            if (sourceName.IndexOf("Pale", StringComparison.Ordinal) >= 0)
                return new Color(0.90f, 0.82f, 0.72f, 1f);
            return Color.white;
        }

        private static RepresentativeArtDefinition CreateDefinition(
            RepresentativeSeed seed)
        {
            var path = $"{DefinitionFolder}/{seed.PrefabKey}_Definition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<RepresentativeArtDefinition>(path);
            if (definition == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<RepresentativeArtDefinition>();
                definition.name = $"{seed.PrefabKey}_Definition";
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("assetId").stringValue = seed.AssetId;
            serialized.FindProperty("prefabKey").stringValue = seed.PrefabKey;
            serialized.FindProperty("role").enumValueIndex = (int)seed.Role;
            serialized.FindProperty("footprintCells").vector2IntValue = seed.FootprintCells;
            serialized.FindProperty("sourceProvenanceKey").stringValue =
                seed.SourceProvenanceKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateWrapperPrefab(
            RepresentativeSeed seed,
            IReadOnlyDictionary<string, Material> materials)
        {
            var model =
                AssetDatabase.LoadAssetAtPath<GameObject>(seed.NormalizedModelPath);
            if (model == null)
                throw new InvalidOperationException(
                    $"Normalized model is missing at '{seed.NormalizedModelPath}'.");

            var root = new GameObject(seed.PrefabKey);
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException(
                        $"Could not instantiate normalized model '{seed.NormalizedModelPath}'.");
                instance.name = "Model";
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                AssignWrapperMaterials(instance, materials);
                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    $"{PrefabFolder}/{seed.PrefabKey}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssignWrapperMaterials(
            GameObject instance,
            IReadOnlyDictionary<string, Material> materials)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                var source = renderers[rendererIndex].sharedMaterials;
                var assigned = new Material[source.Length];
                for (var materialIndex = 0;
                     materialIndex < source.Length;
                     materialIndex++)
                {
                    var sourceMaterial = source[materialIndex];
                    if (sourceMaterial == null ||
                        !materials.TryGetValue(sourceMaterial.name, out assigned[materialIndex]))
                    {
                        throw new InvalidOperationException(
                            $"No OpenSimCity wrapper material maps source slot " +
                            $"'{sourceMaterial?.name ?? "<null>"}'.");
                    }
                }
                renderers[rendererIndex].sharedMaterials = assigned;
            }
        }

        private static RepresentativeArtCatalog CreateCatalog(
            IReadOnlyDictionary<string, RepresentativeArtDefinition> definitions,
            IReadOnlyDictionary<string, GameObject> prefabs)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RepresentativeArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RepresentativeArtCatalog>();
                catalog.name = "RepresentativeArtCatalog";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = Seeds.Length;
            for (var index = 0; index < Seeds.Length; index++)
            {
                var seed = Seeds[index];
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("definition").objectReferenceValue =
                    definitions[seed.AssetId];
                binding.FindPropertyRelative("prefab").objectReferenceValue =
                    prefabs[seed.PrefabKey];
                binding.FindPropertyRelative("placeholderPresentationKey").stringValue =
                    seed.PlaceholderPresentationKey;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ConfigureBenchmarkScene(RepresentativeArtCatalog catalog)
        {
            var placeholderCatalog =
                AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(PlaceholderCatalogPath);
            if (placeholderCatalog == null)
                throw new InvalidOperationException(
                    $"P3 placeholder catalog is missing at '{PlaceholderCatalogPath}'.");

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(
                BenchmarkScenePath,
                OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(
                    candidate => string.Equals(
                        candidate.name,
                        BenchmarkRootName,
                        StringComparison.Ordinal));
                if (root == null)
                    throw new InvalidOperationException(
                        $"Benchmark scene is missing root '{BenchmarkRootName}'.");

                var fixture =
                    root.GetComponent<RepresentativeArtBenchmarkFixture>() ??
                    root.AddComponent<RepresentativeArtBenchmarkFixture>();
                var serialized = new SerializedObject(fixture);
                serialized.FindProperty("representativeCatalog").objectReferenceValue =
                    catalog;
                serialized.FindProperty("placeholderCatalog").objectReferenceValue =
                    placeholderCatalog;
                serialized.FindProperty("initialMode").enumValueIndex =
                    (int)BenchmarkArtMode.ProductionLike;
                serialized.FindProperty("repeatsPerAsset").intValue = 4;
                serialized.FindProperty("assetSpacingMeters").floatValue = 32f;
                serialized.FindProperty("repeatSpacingMeters").floatValue = 36f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fixture);

                var camera = scene.GetRootGameObjects()
                    .SelectMany(candidate => candidate.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
                if (camera == null)
                    throw new InvalidOperationException("Benchmark scene has no MainCamera.");
                camera.orthographic = true;
                camera.orthographicSize = 115f;
                camera.transform.position = new Vector3(0f, 105f, -180f);
                camera.transform.rotation = Quaternion.Euler(27f, 0f, 0f);
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(camera.transform);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
                if (previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        private static void ValidateProvenance(ICollection<string> issues)
        {
            if (!File.Exists(ProvenanceManifestPath))
                issues.Add($"Acquisition manifest is missing at '{ProvenanceManifestPath}'.");
            if (!File.Exists(ProvenanceLicensePath))
                issues.Add($"CC0 license is missing at '{ProvenanceLicensePath}'.");
            if (!File.Exists($"{VendorRoot}/Provenance/normalization-report.json"))
                issues.Add("Blender normalization report is missing.");
            if (!File.Exists($"{VendorRoot}/Provenance/validation-report.json"))
                issues.Add("Blender validation report is missing.");

            if (!File.Exists(ProvenanceManifestPath))
                return;
            var manifest = File.ReadAllText(ProvenanceManifestPath);
            for (var index = 0; index < Seeds.Length; index++)
            {
                if (manifest.IndexOf(Seeds[index].AssetId, StringComparison.Ordinal) < 0)
                    issues.Add(
                        $"Acquisition manifest does not list stable asset ID " +
                        $"'{Seeds[index].AssetId}'.");
            }
            if (manifest.IndexOf("\"spdx\": \"CC0-1.0\"", StringComparison.Ordinal) < 0)
                issues.Add("Acquisition manifest does not pin the CC0-1.0 license.");
        }

        private static void ValidateTextures(ICollection<string> issues)
        {
            for (var index = 0; index < TextureNames.Length; index++)
            {
                var path = TexturePath(TextureNames[index]);
                if (!HasExactCase(path))
                {
                    issues.Add($"Texture is missing or case-mismatched at '{path}'.");
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    issues.Add($"Texture importer is unavailable for '{path}'.");
                    continue;
                }
                var isNormal =
                    TextureNames[index].EndsWith("_Normal.png", StringComparison.Ordinal);
                var expectedType = isNormal
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                if (importer.textureType != expectedType)
                    issues.Add($"Texture '{path}' has incorrect importer type.");
                if (importer.maxTextureSize != 2048)
                    issues.Add($"Texture '{path}' must keep the 2K import ceiling.");
            }
        }

        private static void ValidateModels(ICollection<string> issues)
        {
            for (var index = 0; index < Seeds.Length; index++)
            {
                var seed = Seeds[index];
                if (!HasExactCase(seed.SourceModelPath))
                    issues.Add(
                        $"Vendor source FBX is missing or case-mismatched at " +
                        $"'{seed.SourceModelPath}'.");
                if (!HasExactCase(seed.NormalizedModelPath))
                {
                    issues.Add(
                        $"Normalized FBX is missing or case-mismatched at " +
                        $"'{seed.NormalizedModelPath}'.");
                    continue;
                }

                var importer =
                    AssetImporter.GetAtPath(seed.NormalizedModelPath) as ModelImporter;
                if (importer == null)
                {
                    issues.Add($"Model importer is unavailable for '{seed.NormalizedModelPath}'.");
                    continue;
                }
                if (!Mathf.Approximately(importer.globalScale, 1f) || !importer.useFileScale)
                    issues.Add($"Model '{seed.NormalizedModelPath}' must import at scale 1.");
                if (importer.addCollider)
                    issues.Add($"Model '{seed.NormalizedModelPath}' must not generate colliders.");
                if (!importer.bakeAxisConversion)
                    issues.Add($"Model '{seed.NormalizedModelPath}' must bake Unity axis conversion.");
                if (importer.importAnimation)
                    issues.Add($"Model '{seed.NormalizedModelPath}' must not import animation.");
            }
        }

        private static void ValidateCatalogAndWrappers(ICollection<string> issues)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RepresentativeArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                issues.Add($"Representative catalog is missing at '{CatalogPath}'.");
                return;
            }
            if (catalog.Bindings.Count != Seeds.Length)
                issues.Add(
                    $"Representative catalog has {catalog.Bindings.Count} bindings, " +
                    $"expected {Seeds.Length}.");

            var seedById = Seeds.ToDictionary(
                seed => seed.AssetId,
                seed => seed,
                StringComparer.Ordinal);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < catalog.Bindings.Count; index++)
            {
                var binding = catalog.Bindings[index];
                if (binding?.Definition == null)
                {
                    issues.Add($"Representative binding {index} has no definition.");
                    continue;
                }

                var definition = binding.Definition;
                if (!seenIds.Add(definition.AssetId))
                    issues.Add($"Duplicate representative asset ID '{definition.AssetId}'.");
                if (!seenKeys.Add(definition.PrefabKey))
                    issues.Add($"Duplicate representative prefab key '{definition.PrefabKey}'.");
                if (!seedById.TryGetValue(definition.AssetId, out var seed))
                {
                    issues.Add($"Unexpected representative asset ID '{definition.AssetId}'.");
                    continue;
                }
                if (!string.Equals(
                        definition.PrefabKey,
                        seed.PrefabKey,
                        StringComparison.Ordinal))
                    issues.Add($"Definition '{definition.AssetId}' has an unstable prefab key.");
                if (definition.Role != seed.Role ||
                    definition.FootprintCells != seed.FootprintCells)
                    issues.Add($"Definition '{definition.AssetId}' metadata does not match manifest.");
                if (!string.Equals(
                        definition.SourceProvenanceKey,
                        seed.SourceProvenanceKey,
                        StringComparison.Ordinal))
                    issues.Add($"Definition '{definition.AssetId}' provenance key is incorrect.");
                if (binding.Prefab == null)
                {
                    issues.Add($"Definition '{definition.AssetId}' has no wrapper prefab.");
                    continue;
                }
                if (!string.Equals(
                        binding.Prefab.name,
                        seed.PrefabKey,
                        StringComparison.Ordinal))
                    issues.Add($"Definition '{definition.AssetId}' uses the wrong prefab.");
                if (!string.Equals(
                        binding.PlaceholderPresentationKey,
                        seed.PlaceholderPresentationKey,
                        StringComparison.Ordinal))
                    issues.Add($"Definition '{definition.AssetId}' has the wrong placeholder pair.");
                ValidateWrapper(seed, binding.Prefab, issues);
            }

            for (var index = 0; index < Seeds.Length; index++)
            {
                if (!seenIds.Contains(Seeds[index].AssetId))
                    issues.Add($"Representative asset ID '{Seeds[index].AssetId}' is not mapped.");
            }
        }

        private static void ValidateWrapper(
            RepresentativeSeed seed,
            GameObject prefab,
            ICollection<string> issues)
        {
            var transform = prefab.transform;
            if (transform.localPosition != Vector3.zero ||
                transform.localRotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
                issues.Add($"Wrapper prefab '{prefab.name}' root transform is not normalized.");
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                issues.Add($"Wrapper prefab '{prefab.name}' contains a per-view MonoBehaviour.");
            if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                issues.Add($"Wrapper prefab '{prefab.name}' must not add a P3-08 collider.");
            if (prefab.GetComponentsInChildren<LODGroup>(true).Length != 0)
                issues.Add($"Wrapper prefab '{prefab.name}' must not add a P3-08 LOD.");

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                issues.Add($"Wrapper prefab '{prefab.name}' could not be instantiated.");
                return;
            }
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    issues.Add($"Wrapper prefab '{prefab.name}' has no renderer.");
                    return;
                }
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                    bounds.Encapsulate(renderers[index].bounds);
                ValidateDimension(seed, "width", bounds.size.x, seed.ExpectedDimensions.x, issues);
                ValidateDimension(seed, "height", bounds.size.y, seed.ExpectedDimensions.y, issues);
                ValidateDimension(seed, "depth", bounds.size.z, seed.ExpectedDimensions.z, issues);
                if (Mathf.Abs(bounds.min.y) > BoundsTolerance)
                    issues.Add($"Wrapper prefab '{prefab.name}' pivot is not on the bottom plane.");
                if (Mathf.Abs(bounds.center.x) > BoundsTolerance ||
                    Mathf.Abs(bounds.center.z) > BoundsTolerance)
                    issues.Add($"Wrapper prefab '{prefab.name}' pivot is not footprint-centered.");

                var metrics = RepresentativeArtMetrics.Capture(instance);
                if (metrics.TriangleCount != seed.Triangles)
                    issues.Add(
                        $"Wrapper prefab '{prefab.name}' has {metrics.TriangleCount} triangles, " +
                        $"expected {seed.Triangles}.");
                if (metrics.MaterialSlotCount != seed.MaterialSlots)
                    issues.Add(
                        $"Wrapper prefab '{prefab.name}' has {metrics.MaterialSlotCount} " +
                        $"material slots, expected {seed.MaterialSlots}.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateDimension(
            RepresentativeSeed seed,
            string dimension,
            float actual,
            float expected,
            ICollection<string> issues)
        {
            if (Mathf.Abs(actual - expected) > BoundsTolerance)
                issues.Add(
                    $"Wrapper prefab '{seed.PrefabKey}' {dimension} is {actual:F4}m, " +
                    $"expected {expected:F4}m.");
        }

        private static void ValidateSceneBoundary(ICollection<string> issues)
        {
            ValidateBenchmarkScene(issues);
            var scene = EditorSceneManager.OpenScene(
                CityPrototypeScenePath,
                OpenSceneMode.Additive);
            try
            {
                var fixtures = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<RepresentativeArtBenchmarkFixture>(true))
                    .ToArray();
                if (fixtures.Length != 0)
                    issues.Add("CityPrototype must not contain the P3-08 Benchmark fixture.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ValidateBenchmarkScene(ICollection<string> issues)
        {
            var scene = EditorSceneManager.OpenScene(
                BenchmarkScenePath,
                OpenSceneMode.Additive);
            try
            {
                var fixtures = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<RepresentativeArtBenchmarkFixture>(true))
                    .ToArray();
                if (fixtures.Length != 1)
                {
                    issues.Add(
                        $"Benchmark scene has {fixtures.Length} representative fixtures; expected 1.");
                    return;
                }
                var fixture = fixtures[0];
                if (fixture.RepresentativeCatalog == null ||
                    fixture.PlaceholderCatalog == null)
                    issues.Add("Benchmark fixture catalog references are incomplete.");
                if (fixture.RepeatsPerAsset != 4)
                    issues.Add("Benchmark fixture must repeat each representative four times.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static string TexturePath(string textureName) =>
            $"{VendorTextureFolder}/{textureName}";

        private static bool HasExactCase(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
                return false;

            var current = root;
            var relative = fullPath.Substring(root.Length);
            var segments = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                if (!Directory.Exists(current))
                    return false;
                var expected = segments[index];
                var match = Directory.EnumerateFileSystemEntries(current)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileName(path),
                        expected,
                        StringComparison.Ordinal));
                if (match == null)
                    return false;
                current = match;
            }
            return File.Exists(current);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

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

        private readonly struct RepresentativeSeed
        {
            public RepresentativeSeed(
                string assetId,
                string prefabKey,
                RepresentativeArtRole role,
                string sourceFileName,
                string normalizedFileName,
                Vector3 expectedDimensions,
                Vector2Int footprintCells,
                int triangles,
                int materialSlots,
                string placeholderPresentationKey)
            {
                AssetId = assetId;
                PrefabKey = prefabKey;
                Role = role;
                SourceFileName = sourceFileName;
                NormalizedFileName = normalizedFileName;
                ExpectedDimensions = expectedDimensions;
                FootprintCells = footprintCells;
                Triangles = triangles;
                MaterialSlots = materialSlots;
                PlaceholderPresentationKey = placeholderPresentationKey;
            }

            public string AssetId { get; }
            public string PrefabKey { get; }
            public RepresentativeArtRole Role { get; }
            public string SourceFileName { get; }
            public string NormalizedFileName { get; }
            public Vector3 ExpectedDimensions { get; }
            public Vector2Int FootprintCells { get; }
            public int Triangles { get; }
            public int MaterialSlots { get; }
            public string PlaceholderPresentationKey { get; }
            public string SourceModelPath =>
                $"{VendorRoot}/Source/FBX/{SourceFileName}";
            public string NormalizedModelPath =>
                $"{VendorRoot}/Normalized/FBX/{NormalizedFileName}";
            public string SourceProvenanceKey =>
                $"DowntownCityMegaKit/Standard/{SourceFileName}";
        }

    }
}
