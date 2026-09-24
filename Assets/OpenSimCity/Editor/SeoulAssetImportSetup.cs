using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Imports the user's Korean-city Blender library into normalized, behaviour-
    /// free Unity prefabs. Source .blend files remain outside the project and are
    /// never modified; the checked-in FBX exports are the build inputs.
    /// </summary>
    public static class SeoulAssetImportSetup
    {
        public const string ModelFolder = "Assets/OpenSimCity/Content/Art/Seoul/Models";
        public const string PrefabFolder = "Assets/OpenSimCity/Content/Prefabs/ProductionArt/Seoul";
        public const string MaterialFolder = "Assets/OpenSimCity/Content/Art/Seoul/Materials";
        public const string EnvironmentResourceFolder =
            "Assets/OpenSimCity/Resources/OpenSimCity/Environment";
        public const string DensityMediumOfficeResourcePath = "OpenSimCity/Environment/DensityMediumOffice";
        public const string DensityMediumApartmentResourcePath = "OpenSimCity/Environment/DensityMediumApartment";
        public const string DensityHighOfficeResourcePath = "OpenSimCity/Environment/DensityHighOffice";
        public const string DensityHighApartmentResourcePath = "OpenSimCity/Environment/DensityHighApartment";
        public const string UrbanBridgeResourcePath = "OpenSimCity/Environment/UrbanBridge";

        private static readonly IReadOnlyDictionary<string, string> BuildingModels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["building.residential.low.a"] = "V05_villa_00",
                ["building.residential.low.b"] = "V05_villa_10",
                ["building.residential.low.c"] = "V05_villa_15",
                ["building.commercial.low.a"] = "V05_mixed_00",
                ["building.commercial.low.b"] = "V05_mixed_03",
                ["building.commercial.low.c"] = "V05_mixed_09"
            };

        [MenuItem("OpenSimCity/Setup/Import User Seoul Asset Library")]
        public static void Configure()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(EnvironmentResourceFolder);

            foreach (var model in BuildingModels.Values)
                CreateNormalizedPrefab(
                    model, $"{PrefabFolder}/{model}.prefab", NormalizeByFootprint, 0.88f);

            CreateNormalizedPrefab(
                // A saturated taxi body dominated the city-scale read and made
                // the traffic layer feel imported. Use the neutral authored
                // silver variant as the shared baseline; service colours remain
                // available through their dedicated presentation layers.
                "KR_Car_Silver", VehicleArtSetup.TrafficPrefabPath, NormalizeByLength, 1.80f, "TrafficCar");
            CreateNormalizedPrefab(
                "KR_Bus_Blue", VehicleArtSetup.BusPrefabPath, NormalizeByLength, 2.20f, "TransitBus");
            CreateNormalizedPrefab(
                "V08_StreetTreeA",
                $"{EnvironmentResourceFolder}/StreetTree.prefab", NormalizeByHeight, 1.15f, "StreetTree");
            CreateNormalizedPrefab("V08_StreetTreeB",
                $"{EnvironmentResourceFolder}/StreetTreeB.prefab", NormalizeByHeight, 1.15f, "StreetTreeB");
            CreateNormalizedPrefab("V08_Bench",
                $"{EnvironmentResourceFolder}/StreetBench.prefab", NormalizeByLength, 0.23f, "StreetBench");
            CreateNormalizedPrefab("V08_WasteBin",
                $"{EnvironmentResourceFolder}/StreetWasteBin.prefab", NormalizeByHeight, 0.11f, "StreetWasteBin");
            CreateNormalizedPrefab(
                "KR_PROP_StreetLight_6m",
                $"{EnvironmentResourceFolder}/StreetLight.prefab", NormalizeByHeight, 0.95f, "StreetLight");
            CreateNormalizedPrefab(
                "KR_TRAFFIC_Signal_Mast_Red",
                $"{EnvironmentResourceFolder}/TrafficSignal.prefab",
                NormalizeByHeight, 0.88f, "TrafficSignal");
            CreateNormalizedPrefab(
                "V05_villa_20",
                $"{EnvironmentResourceFolder}/SeoulLowriseResidential.prefab",
                NormalizeByFootprint, 0.88f, "SeoulLowriseResidential");
            CreateNormalizedPrefab(
                "V05_mixed_06",
                $"{EnvironmentResourceFolder}/SeoulLowriseCommercial.prefab",
                NormalizeByFootprint, 0.88f, "SeoulLowriseCommercial");
            CreateNormalizedPrefab(
                "V05_Office_00",
                $"{EnvironmentResourceFolder}/DensityMediumOffice.prefab",
                NormalizeByFootprint, 0.82f, "DensityMediumOffice");
            CreateNormalizedPrefab(
                "V05_APT_Slab18",
                $"{EnvironmentResourceFolder}/DensityMediumApartment.prefab",
                NormalizeByFootprint, 0.82f, "DensityMediumApartment");
            CreateNormalizedPrefab(
                "V05_Office_03",
                $"{EnvironmentResourceFolder}/DensityMediumOfficeAlt.prefab",
                NormalizeByFootprint, 0.82f, "DensityMediumOfficeAlt");
            CreateNormalizedPrefab(
                "V05_APT_L_Wing24",
                $"{EnvironmentResourceFolder}/DensityMediumApartmentSlab.prefab",
                NormalizeByFootprint, 0.92f, "DensityMediumApartmentSlab");
            CreateNormalizedPrefab(
                "V05_Office_09",
                $"{EnvironmentResourceFolder}/DensityHighOffice.prefab",
                NormalizeByFootprint, 0.84f, "DensityHighOffice");
            CreateNormalizedPrefab(
                "V05_APT_SteppedTower",
                $"{EnvironmentResourceFolder}/DensityHighApartment.prefab",
                NormalizeByFootprint, 0.84f, "DensityHighApartment");
            CreateNormalizedPrefab(
                "V05_APT_Slab25",
                $"{EnvironmentResourceFolder}/DensityHighApartmentSlab.prefab",
                NormalizeByFootprint, 0.94f, "DensityHighApartmentSlab");
            CreateNormalizedPrefab(
                "V05_Office_06", $"{EnvironmentResourceFolder}/YeouidoOfficeMidHigh.prefab",
                NormalizeByFootprint, 0.84f, "YeouidoOfficeMidHigh");
            CreateNormalizedPrefab(
                "V05_villa_05", $"{EnvironmentResourceFolder}/YeouidoVillaA.prefab",
                NormalizeByFootprint, 0.82f, "YeouidoVillaA");
            CreateNormalizedPrefab(
                "V05_villa_10", $"{EnvironmentResourceFolder}/YeouidoVillaB.prefab",
                NormalizeByFootprint, 0.82f, "YeouidoVillaB");
            CreateNormalizedPrefab(
                "V05_villa_15", $"{EnvironmentResourceFolder}/YeouidoVillaC.prefab",
                NormalizeByFootprint, 0.82f, "YeouidoVillaC");
            CreateNormalizedPrefab(
                "V05_mixed_00", $"{EnvironmentResourceFolder}/YeouidoMixedA.prefab",
                NormalizeByFootprint, 0.84f, "YeouidoMixedA");
            CreateNormalizedPrefab(
                "V05_mixed_03", $"{EnvironmentResourceFolder}/YeouidoMixedB.prefab",
                NormalizeByFootprint, 0.84f, "YeouidoMixedB");
            CreateNormalizedPrefab(
                "V05_mixed_09", $"{EnvironmentResourceFolder}/YeouidoMixedC.prefab",
                NormalizeByFootprint, 0.84f, "YeouidoMixedC");
            CreateNormalizedPrefab(
                "V05_Underpass_DesignModule_NOT_SURVEYED",
                $"{EnvironmentResourceFolder}/UrbanUnderpass.prefab",
                NormalizeByLength, 2.4f, "UrbanUnderpass");
            CreateNormalizedPrefab(
                "KR_RD_4Lane_Straight40m",
                $"{EnvironmentResourceFolder}/RoadAvenue4Lane.prefab",
                NormalizeByLength, 1.02f, "RoadAvenue4Lane");
            CreateNormalizedPrefab(
                "KR_RD_6Lane_Straight40m",
                $"{EnvironmentResourceFolder}/RoadBoulevard6Lane.prefab",
                NormalizeByLength, 1.02f, "RoadBoulevard6Lane");
            CreateNormalizedPrefab(
                "KR_Bridge_Seoul",
                $"{EnvironmentResourceFolder}/UrbanBridge.prefab",
                // The source is a long modular deck. Normalize it to the actual
                // review river span instead of letting the mesh continue through
                // the district. Compensating X/Y factors retain a credible road
                // width and deck thickness after halving its longitudinal scale.
                NormalizeByLength, 6f, "UrbanBridge", verticalScale: 5f, horizontalXScale: 4.3f);
            var crosswalk = CreateNormalizedPrefab(
                "KR_RD_Crosswalk_4m",
                $"{PrefabFolder}/KR_RD_Crosswalk_4m.prefab", NormalizeByFootprint, 0.90f, "Crosswalk");
            InstallCrosswalk(crosswalk);
            RemoveEmbeddedWideRoadModules();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity user-authored Seoul buildings, vehicles, vegetation and road details imported.");
        }

        [MenuItem("OpenSimCity/Diagnostics/Log Seoul Review Asset Bounds")]
        public static void LogReviewAssetBounds()
        {
            foreach (var path in new[]
                     {
                         $"{EnvironmentResourceFolder}/UrbanBridge.prefab",
                         $"{EnvironmentResourceFolder}/UrbanUnderpass.prefab",
                         $"{EnvironmentResourceFolder}/DensityHighOffice.prefab",
                         $"{EnvironmentResourceFolder}/SeoulLowriseResidential.prefab"
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"OpenSimCity review asset missing: {path}");
                    continue;
                }
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                try
                {
                    var bounds = CombinedBounds(instance.GetComponentsInChildren<Renderer>(true));
                    Debug.Log($"OpenSimCity review asset bounds: {path} center={bounds.center} size={bounds.size}");
                }
                finally
                {
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        public static GameObject LoadBuildingPrefabForKey(string presentationKey)
        {
            return BuildingModels.TryGetValue(presentationKey, out var model)
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{model}.prefab")
                : null;
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            foreach (var pair in BuildingModels)
                ValidatePrefab(LoadBuildingPrefabForKey(pair.Key), pair.Value, 0.35f, issues);
            ValidatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtSetup.TrafficPrefabPath),
                "TrafficCar", 0.20f, issues);
            ValidatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtSetup.BusPrefabPath),
                "TransitBus", 0.20f, issues);
            ValidatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentResourceFolder}/StreetTree.prefab"),
                "StreetTree", 0.50f, issues);
            ValidatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentResourceFolder}/TrafficSignal.prefab"),
                "TrafficSignal", 0.50f, issues);
            ValidatePrefab(Resources.Load<GameObject>(DensityMediumOfficeResourcePath),
                "DensityMediumOffice", 0.70f, issues);
            ValidatePrefab(Resources.Load<GameObject>(DensityMediumApartmentResourcePath),
                "DensityMediumApartment", 0.70f, issues);
            ValidatePrefab(Resources.Load<GameObject>(DensityHighOfficeResourcePath),
                "DensityHighOffice", 0.70f, issues);
            ValidatePrefab(Resources.Load<GameObject>(DensityHighApartmentResourcePath),
                "DensityHighApartment", 0.70f, issues);
            ValidatePrefab(Resources.Load<GameObject>(UrbanBridgeResourcePath),
                "UrbanBridge", 1.0f, issues);
            ValidatePrefab(Resources.Load<GameObject>("OpenSimCity/Environment/RoadAvenue4Lane"),
                "RoadAvenue4Lane", 0.50f, issues);
            ValidatePrefab(Resources.Load<GameObject>("OpenSimCity/Environment/RoadBoulevard6Lane"),
                "RoadBoulevard6Lane", 0.50f, issues);
            var road = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PlaceholderArtSetup.PrefabFolder}/ROAD_STREET.prefab");
            if (road == null || road.transform.Find("Crosswalk") == null)
                issues.Add("The road prefab does not contain the Korean crosswalk detail.");
            return issues.AsReadOnly();
        }

        private static GameObject CreateNormalizedPrefab(
            string modelName,
            string outputPath,
            Func<Bounds, float> measure,
            float targetSize,
            string outputName = null,
            float verticalScale = 1f,
            float horizontalXScale = 1f)
        {
            var sourcePath = $"{ModelFolder}/{modelName}.fbx";
            var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Seoul FBX export is missing at '{sourcePath}'.");
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var root = new GameObject(outputName ?? modelName);
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform);
                model.name = "Model";
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(MapMaterial).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                var bounds = CombinedBounds(model.GetComponentsInChildren<Renderer>(true));
                if (measure(bounds) < 0.0001f)
                    throw new InvalidOperationException($"Imported Seoul model '{modelName}' has empty geometry.");
                var scale = targetSize / Mathf.Max(0.0001f, measure(bounds));
                model.transform.localScale = Vector3.Scale(
                    model.transform.localScale,
                    // Blender FBX arrives with a +90 degree X root rotation:
                    // local Z maps to Unity world Y and local Y maps to world Z.
                    new Vector3(scale * horizontalXScale, scale, scale * verticalScale));
                // Re-measure after the complete import rotation/scale. Some
                // authored modules retain a far-away source pivot even though
                // their geometry was exported in isolation; bounds-based final
                // centring handles both ordinary assets and those modules.
                var normalizedBounds = CombinedBounds(model.GetComponentsInChildren<Renderer>(true));
                model.transform.localPosition += new Vector3(
                    -normalizedBounds.center.x,
                    -normalizedBounds.min.y,
                    -normalizedBounds.center.z);
                return PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static float NormalizeByFootprint(Bounds bounds) => Mathf.Max(bounds.size.x, bounds.size.z);
        private static float NormalizeByLength(Bounds bounds) => Mathf.Max(bounds.size.x, bounds.size.z);
        private static float NormalizeByHeight(Bounds bounds) => bounds.size.y;

        private static Bounds CombinedBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers.Count == 0)
                throw new InvalidOperationException("A Seoul asset has no renderer.");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static Material MapMaterial(Material source)
        {
            if (source == null)
                throw new InvalidOperationException("A Seoul asset has an empty material slot.");
            var safeName = source.name.Replace(" (Instance)", string.Empty);
            var path = $"{MaterialFolder}/MAT_{safeName}.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (result == null)
            {
                result = new Material(shader) { name = $"MAT_{safeName}" };
                AssetDatabase.CreateAsset(result, path);
            }
            else if (result.shader != shader)
            {
                result.shader = shader;
            }

            var lower = safeName.ToLowerInvariant();
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            if (color.maxColorComponent < 0.08f)
                color = new Color(0.22f, 0.25f, 0.27f, 1f);
            if (lower.Contains("leaf"))
            {
                // Preserve canopy tonal variation through the FBX material conversion.
                var variant = 0;
                foreach (var character in lower) variant = unchecked(variant * 31 + character);
                var tone = (variant & 3) * 0.025f;
                color = new Color(0.18f + tone, 0.29f + tone, 0.12f + tone * 0.5f, 1f);
                result.SetFloat("_Cull", 0f);
            }
            else if (lower.Contains("bark")) color = new Color(0.20f, 0.13f, 0.08f, 1f);
            else if (lower.Contains("glass") || lower.Contains("glazing"))
            {
                var glazingVariant = 0;
                var separator = lower.LastIndexOf('_');
                if (separator >= 0)
                    int.TryParse(lower.Substring(separator + 1), out glazingVariant);
                var variation = (glazingVariant % 5) * 0.009f;
                color = new Color(0.15f + variation, 0.235f + variation, 0.275f + variation, 1f);
            }
            else if (lower.Contains("warmmineral")) color = new Color(0.66f, 0.60f, 0.50f, 1f);
            else if (lower.Contains("deepframe")) color = new Color(0.14f, 0.16f, 0.17f, 1f);
            else if (lower.Contains("satinsteel")) color = new Color(0.46f, 0.49f, 0.50f, 1f);
            else if (lower.Contains("blinds")) color = new Color(0.50f, 0.51f, 0.48f, 1f);
            else if (lower.Contains("roofwaterproofing")) color = new Color(0.235f, 0.28f, 0.265f, 1f);
            else if (lower.Contains("koreansigngreen")) color = new Color(0.17f, 0.30f, 0.245f, 1f);
            else if (lower.Contains("koreantrafficblue")) color = new Color(0.12f, 0.28f, 0.40f, 1f);
            else if (lower.Contains("buslane_mutedred")) color = new Color(0.40f, 0.17f, 0.15f, 1f);
            else if (lower.Contains("plaster")) color = new Color(0.72f, 0.69f, 0.62f, 1f);
            else if (lower.Contains("stonecream")) color = new Color(0.66f, 0.60f, 0.49f, 1f);
            else if (lower.Contains("stonedark")) color = new Color(0.22f, 0.23f, 0.22f, 1f);
            else if (lower.Contains("concrete")) color = new Color(0.60f, 0.62f, 0.60f, 1f);
            else if (lower.Contains("spandrel")) color = new Color(0.25f, 0.29f, 0.31f, 1f);
            else if (lower.Contains("trimlight")) color = new Color(0.69f, 0.70f, 0.67f, 1f);
            else if (lower.Contains("trimwarm")) color = new Color(0.50f, 0.42f, 0.33f, 1f);
            else if (lower.Contains("roomlight")) color = new Color(0.66f, 0.56f, 0.37f, 1f);
            else if (lower.Contains("roomdark")) color = new Color(0.055f, 0.065f, 0.070f, 1f);
            else if (lower.Contains("brick")) color = new Color(0.46f, 0.25f, 0.17f, 1f);
            else if (lower.Contains("roofgreen")) color = new Color(0.12f, 0.24f, 0.19f, 1f);
            else if (lower.Contains("roadpaint_white")) color = new Color(0.68f, 0.69f, 0.66f, 1f);
            else if (lower.Contains("roadpaint_yellow")) color = new Color(0.78f, 0.59f, 0.12f, 1f);
            else if (lower.Contains("asphalt") || lower.Contains("pavement"))
                color = new Color(0.72f, 0.73f, 0.71f, 1f);
            result.color = color;
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Smoothness"))
                result.SetFloat("_Smoothness",
                    lower.Contains("glass") || lower.Contains("glazing") ? 0.72f :
                    lower.Contains("satinsteel") || lower.Contains("metal") ? 0.42f :
                    lower.Contains("deepframe") ? 0.28f : 0.12f);
            if (result.HasProperty("_Metallic"))
                result.SetFloat("_Metallic",
                    lower.Contains("satinsteel") ? 0.72f :
                    lower.Contains("metal") || lower.Contains("stainless") || lower.Contains("deepframe") ? 0.42f : 0f);

            var v05Surface = lower.Contains("asphalt") || lower.Contains("pavement") ||
                             lower.Contains("roofwaterproofing")
                ? "Asphalt"
                : lower.Contains("warmmineral") || lower.Contains("concrete")
                    ? "Concrete"
                    : lower.Contains("plaster")
                        ? "Plaster"
                        : lower.Contains("timber")
                            ? "Timber"
                            : lower.Contains("bark") ? "Bark" : null;
            var polyHavenId = v05Surface == null && lower.Contains("brick")
                ? "brick_wall_001"
                : v05Surface == null && lower.Contains("stone") ? "concrete_wall_003" : null;
            if (v05Surface != null || polyHavenId != null)
            {
                var textureRoot = v05Surface != null
                    ? $"Assets/OpenSimCity/Content/Art/Yeouido/Textures/{v05Surface}"
                    : $"Assets/ThirdParty/PolyHaven/{polyHavenId}";
                var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(v05Surface != null
                    ? textureRoot + "/BaseColor.jpg"
                    : $"{textureRoot}/{polyHavenId}_Diffuse.jpg");
                var normalPath = v05Surface != null
                    ? textureRoot + "/NormalGL.png"
                    : $"{textureRoot}/{polyHavenId}_nor_gl.jpg";
                ConfigureNormalTexture(normalPath);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                var occlusion = v05Surface == null
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureRoot}/{polyHavenId}_AO.jpg")
                    : null;
                if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", diffuse);
                if (result.HasProperty("_BumpMap")) result.SetTexture("_BumpMap", normal);
                if (result.HasProperty("_OcclusionMap")) result.SetTexture("_OcclusionMap", occlusion);
                if (result.HasProperty("_BumpScale")) result.SetFloat("_BumpScale", 0.42f);
                if (result.HasProperty("_OcclusionStrength")) result.SetFloat("_OcclusionStrength", 0.62f);
                result.EnableKeyword("_NORMALMAP");
                result.EnableKeyword("_OCCLUSIONMAP");
            }
            if (v05Surface != null) UrbanRoughnessSetup.Apply(result, v05Surface);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void ConfigureNormalTexture(string path)
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

        private static void RemoveEmbeddedWideRoadModules()
        {
            var roadPath = $"{PlaceholderArtSetup.PrefabFolder}/ROAD_STREET.prefab";
            var road = PrefabUtility.LoadPrefabContents(roadPath);
            try
            {
                foreach (var name in new[]
                         {
                             "Avenue4LaneNS", "Avenue4LaneEW",
                             "Boulevard6LaneNS", "Boulevard6LaneEW"
                         })
                {
                    var existing = road.transform.Find(name);
                    if (existing != null)
                        UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(road, roadPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(road);
            }
        }

        private static void InstallCrosswalk(GameObject crosswalkPrefab)
        {
            var roadPath = $"{PlaceholderArtSetup.PrefabFolder}/ROAD_STREET.prefab";
            var road = PrefabUtility.LoadPrefabContents(roadPath);
            try
            {
                var existing = road.transform.Find("Crosswalk");
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                var crosswalk = new GameObject("Crosswalk");
                crosswalk.transform.SetParent(road.transform, false);
                AddCrosswalkApproach(crosswalk.transform, crosswalkPrefab, "North", new Vector3(0f, 0.064f, 0.32f), 0f);
                AddCrosswalkApproach(crosswalk.transform, crosswalkPrefab, "East", new Vector3(0.32f, 0.064f, 0f), 90f);
                AddCrosswalkApproach(crosswalk.transform, crosswalkPrefab, "South", new Vector3(0f, 0.064f, -0.32f), 0f);
                AddCrosswalkApproach(crosswalk.transform, crosswalkPrefab, "West", new Vector3(-0.32f, 0.064f, 0f), 90f);
                crosswalk.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(road, roadPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(road);
            }
        }

        private static void AddCrosswalkApproach(
            Transform parent,
            GameObject prefab,
            string name,
            Vector3 position,
            float rotation)
        {
            var approach = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (approach == null)
                throw new InvalidOperationException("Could not instantiate the Korean crosswalk prefab.");
            approach.name = name;
            approach.transform.SetParent(parent, false);
            approach.transform.localPosition = position;
            approach.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);
            // Authored at 6.4m wide by 4m deep: fit the complete stop-line and
            // stripe set across the carriageway instead of shrinking it to a tile.
            approach.transform.localScale = new Vector3(0.78f, 1f, 0.30f);
        }

        private static void ValidatePrefab(
            GameObject prefab,
            string name,
            float minimumDimension,
            ICollection<string> issues)
        {
            if (prefab == null)
            {
                issues.Add($"Seoul asset prefab '{name}' is missing.");
                return;
            }
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                issues.Add($"Seoul asset prefab '{name}' must remain behaviour-free.");
            if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                issues.Add($"Seoul asset prefab '{name}' must not contain colliders.");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 1)
                issues.Add($"Seoul asset prefab '{name}' must use one renderer; found {renderers.Length}.");
            else if (Mathf.Max(renderers[0].bounds.size.x, renderers[0].bounds.size.y, renderers[0].bounds.size.z) < minimumDimension)
                issues.Add($"Seoul asset prefab '{name}' is implausibly small: {renderers[0].bounds}.");
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
