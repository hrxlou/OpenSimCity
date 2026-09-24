using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Roads;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.World
{
    /// <summary>
    /// Opt-in standalone visual-review fixture. It uses the same application
    /// construction commands as player input and never mutates simulation arrays.
    /// </summary>
    internal sealed class VisualReviewShowcaseBootstrap : MonoBehaviour
    {
        private const string Argument = "-osc-showcase";
        private const string CaptureArgument = "-osc-capture";
        private const string FocusArgument = "-osc-focus";
        private const string HideUiArgument = "-osc-hide-ui";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (!HasArgument(Environment.GetCommandLineArgs(), Argument))
                return;
            // Evidence capture must also complete when a test runner takes focus.
            UnityEngine.Application.runInBackground = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, "CityPrototype", StringComparison.Ordinal) ||
                FindAnyObjectByType<VisualReviewShowcaseBootstrap>() != null)
                return;
            new GameObject("VisualReviewShowcase").AddComponent<VisualReviewShowcaseBootstrap>();
        }

        private IEnumerator Start()
        {
            for (var frame = 0; frame < 8 && CityRuntimeContext.Current == null; frame++)
                yield return null;
            var context = CityRuntimeContext.Current;
            if (context == null)
                yield break;

            var runtime = context.Runtime;
            runtime.SetSimulationSpeed(SimulationSpeed.Paused);
            var roadY = runtime.Height / 2;

            // A compact, connected district grid exposes facade variation,
            // intersections, road material, traffic and skyline depth in one
            // authoritative player render. Every mutation still flows through
            // the public construction command boundary.
            foreach (var offset in new[] { -8, -4, 0, 4, 8 })
                Commit(context, ConstructionToolMode.Road,
                    new CellCoord(0, roadY + offset), new CellCoord(58, roadY + offset));
            foreach (var x in new[] { 12, 20, 28, 36, 44, 52, 58 })
            {
                foreach (var range in new[] { (-7, -5), (-3, -1), (1, 3), (5, 7) })
                    Commit(context, ConstructionToolMode.Road,
                        new CellCoord(x, roadY + range.Item1),
                        new CellCoord(x, roadY + range.Item2));
            }

            var xBands = new[] { (1, 11), (13, 19), (21, 27), (29, 35), (37, 43), (45, 51), (53, 57) };
            var yBands = new[] { (-7, -5), (-3, -1), (1, 3), (5, 7) };
            var zoneModes = new[]
            {
                ConstructionToolMode.ZoneResidential,
                ConstructionToolMode.ZoneCommercial,
                ConstructionToolMode.ZoneResidential,
                ConstructionToolMode.ZoneCommercial,
                ConstructionToolMode.ZoneIndustrial
            };
            var zoneIndex = 0;
            foreach (var xBand in xBands)
            foreach (var yBand in yBands)
            {
                Commit(context, zoneModes[zoneIndex++ % zoneModes.Length],
                    new CellCoord(xBand.Item1, roadY + yBand.Item1),
                    new CellCoord(xBand.Item2, roadY + yBand.Item2));
            }
            Commit(context, ConstructionToolMode.PowerPlant,
                new CellCoord(57, roadY - 1), new CellCoord(57, roadY - 1));

            var boulevard = runtime.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.BoulevardDefinitionId,
                RoadDirectionMode.Bidirectional,
                new CellCoord(0, roadY),
                new CellCoord(58, roadY)));
            if (!boulevard.IsApplied)
                Debug.LogWarning($"OpenSimCity showcase boulevard failed: {boulevard.Message}");
            var avenue = runtime.Execute(new UpgradeRoadCommand(
                RoadHierarchyDefaults.AvenueDefinitionId,
                RoadDirectionMode.Bidirectional,
                new CellCoord(0, roadY + 4),
                new CellCoord(58, roadY + 4)));
            if (!avenue.IsApplied)
                Debug.LogWarning($"OpenSimCity showcase avenue failed: {avenue.Message}");

            for (var month = 0; month < 48; month++)
            {
                var firstTick = month * runtime.MonthlyScheduler.TicksPerMonth;
                for (var offset = 0; offset < runtime.MonthlyScheduler.TicksPerMonth; offset++)
                    runtime.ProcessSimulationTick(new SimulationTick(firstTick + offset));
            }
            runtime.RefreshReadModels();
            context.SelectTool(ConstructionToolMode.None);
            SpawnUrbanBlockSurfaces(context, roadY);
            SpawnRoadFrontageCity(context, roadY);
            SpawnInfrastructureVisualSlice(context, roadY);

            // Give the central pooled renderers time to consume the refreshed
            // application snapshots, then record authoritative showcase state.
            yield return null;
            yield return null;
            FrameShowcase(context, roadY);
            yield return new WaitForEndOfFrame();
            var buildingRenderer = FindAnyObjectByType<Buildings.BuildingViewRenderer>();
            var rendererSamples = buildingRenderer == null
                ? "none"
                : string.Join(" | ", buildingRenderer.GetComponentsInChildren<Renderer>(false)
                    .Take(6)
                    .Select(value =>
                        $"{value.name}: bounds={value.bounds.size}, material={value.sharedMaterial?.name}"));
            Debug.Log(
                $"OpenSimCity showcase: buildingRows={runtime.BuildingViews.Views.Count}, " +
                $"activeBuildingViews={buildingRenderer?.ActiveViewCount ?? -1}, " +
                $"keys={string.Join(",", runtime.BuildingViews.Views.Select(value => value.PresentationKey))}, " +
                $"renderers={rendererSamples}.");

            var capturePath = FindArgumentValue(Environment.GetCommandLineArgs(), CaptureArgument);
            if (string.IsNullOrWhiteSpace(capturePath))
                yield break;

            // Let pooled presentation views and UI Toolkit finish one complete
            // layout/render cycle before requesting the standalone screenshot.
            var directory = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            // Native-resolution evidence preserves the real UI glyph rasterization.
            ScreenCapture.CaptureScreenshot(capturePath, 1);
            yield return new WaitForSecondsRealtime(1f);
            UnityEngine.Application.Quit(0);
        }

        private void SpawnRoadFrontageCity(CityRuntimeContext context, int roadY)
        {
            var paths = new[]
            {
                "OpenSimCity/Environment/SeoulLowriseResidential",
                "OpenSimCity/Environment/SeoulLowriseCommercial",
                "OpenSimCity/Environment/DensityMediumApartment",
                "OpenSimCity/Environment/DensityMediumOffice",
                "OpenSimCity/Environment/DensityMediumApartmentSlab",
                "OpenSimCity/Environment/DensityMediumOfficeAlt",
                "OpenSimCity/Environment/DensityHighApartment",
                "OpenSimCity/Environment/DensityHighApartmentSlab",
                "OpenSimCity/Environment/DensityHighOffice",
                "OpenSimCity/Environment/YeouidoOfficeMidHigh",
                "OpenSimCity/Environment/YeouidoVillaA",
                "OpenSimCity/Environment/YeouidoVillaB",
                "OpenSimCity/Environment/YeouidoVillaC",
                "OpenSimCity/Environment/YeouidoMixedA",
                "OpenSimCity/Environment/YeouidoMixedB",
                "OpenSimCity/Environment/YeouidoMixedC"
            };
            var lowriseChoices = new[] { 0, 1, 10, 11, 12, 13, 14, 15 };
            var occupied = new HashSet<CellCoord>();
            var rows = context.Runtime.BuildingViews.Views;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                for (var x = 0; x < row.FootprintWidth; x++)
                for (var y = 0; y < row.FootprintHeight; y++)
                    occupied.Add(new CellCoord(row.Anchor.X + x, row.Anchor.Y + y));
            }

            var bounds = context.WorldBounds;
            var visualIndex = 0;
            var frontageRows = new[] { -7, -6, -5, -3, -2, -1, 1, 2, 3, 5, 6, 7 };
            for (var rowIndex = 0; rowIndex < frontageRows.Length; rowIndex++)
            for (var x = 1; x <= 57; x++)
            {
                var yOffset = frontageRows[rowIndex];
                var cell = new CellCoord(x, roadY + yOffset);
                if (context.Runtime.Roads.IsOccupied(cell) || occupied.Contains(cell)) continue;
                var hash = unchecked(x * 73856093 ^ yOffset * 19349663);
                if (Math.Abs(hash % 5) == 0) continue;
                var central = Math.Abs(yOffset) <= 3 && x >= 16 && x <= 48;
                var pathIndex = central && Math.Abs(hash % 4) != 0
                    ? 2 + Math.Abs(hash % 8)
                    : Math.Abs(hash % 7) == 0
                        ? 2 + Math.Abs(hash % 4)
                        : lowriseChoices[Math.Abs(hash % lowriseChoices.Length)];
                var prefab = Resources.Load<GameObject>(paths[pathIndex]);
                if (prefab == null) continue;
                var instance = Instantiate(prefab, transform);
                instance.name = $"VisualReviewRoadFrontage-{++visualIndex}";
                var lateralJitter = ((hash >> 4) & 3) * 0.025f - 0.0375f;
                var depthJitter = ((hash >> 7) & 3) * 0.018f - 0.027f;
                instance.transform.localPosition = new Vector3(
                    bounds.MinX + (cell.X + 0.5f + lateralJitter) * bounds.CellSize,
                    0f,
                    bounds.MinZ + (cell.Y + 0.5f + depthJitter) * bounds.CellSize);
                instance.transform.localRotation = Quaternion.Euler(0f, (Math.Abs(hash) % 4) * 90f, 0f);
                var footprintScale = bounds.CellSize * (0.88f + Math.Abs(hash % 5) * 0.025f);
                var heightScale = footprintScale * (0.90f + Math.Abs((hash >> 9) % 7) * 0.035f);
                instance.transform.localScale = new Vector3(footprintScale, heightScale, footprintScale);
            }
        }

        private static void FrameShowcase(CityRuntimeContext context, int roadY)
        {
            var args = Environment.GetCommandLineArgs();
            var focus = FindArgumentValue(args, FocusArgument);
            var camera = Camera.main;
            if (camera != null && string.Equals(focus, "bridge", StringComparison.OrdinalIgnoreCase))
            {
                var bounds = context.WorldBounds;
                var bridgeX = bounds.MinX + 28.5f * bounds.CellSize;
                var riverZ = bounds.MinZ + (roadY + 11f) * bounds.CellSize;
                camera.transform.position += new Vector3(
                    bridgeX - bounds.CenterX,
                    0f,
                    riverZ - bounds.CenterZ);
                camera.orthographicSize = 5.8f;
            }
            else if (camera != null && string.Equals(focus, "street", StringComparison.OrdinalIgnoreCase))
            {
                camera.orthographicSize = 3.8f;
            }
            else if (camera != null && camera.orthographic)
            {
                camera.orthographicSize = 8.75f;
            }

            if (!HasArgument(args, HideUiArgument))
                return;
            foreach (var document in FindObjectsByType<UnityEngine.UIElements.UIDocument>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                document.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
        }

        private void SpawnUrbanBlockSurfaces(CityRuntimeContext context, int roadY)
        {
            var concrete = Resources.Load<Material>(
                "OpenSimCity/Environment/Materials/UrbanParcelConcrete");
            var asphalt = Resources.Load<Material>(
                "OpenSimCity/Environment/Materials/UrbanParcelAsphalt");
            var granite = Resources.Load<Material>(
                "OpenSimCity/Environment/Materials/UrbanParcelGranite");
            if (concrete == null || asphalt == null || granite == null)
            {
                Debug.LogWarning("OpenSimCity showcase city-block PBR materials are missing.");
                return;
            }

            var marking = CreateShowcaseMaterial(
                "VisualReviewParkingMarking", new Color(0.70f, 0.71f, 0.68f, 1f), 0.10f, 0f);
            var bounds = context.WorldBounds;
            var xBands = new[]
            {
                (1, 11), (13, 19), (21, 27), (29, 35),
                (37, 43), (45, 51), (53, 57)
            };
            var yBands = new[] { (-7, -5), (-3, -1), (1, 3), (5, 7) };
            var blockIndex = 0;
            foreach (var xBand in xBands)
            foreach (var yBand in yBands)
            {
                var hash = unchecked(xBand.Item1 * 73856093 ^ yBand.Item1 * 19349663);
                var material = Math.Abs(hash % 5) == 0
                    ? asphalt
                    : Math.Abs(hash % 4) == 0 ? granite : concrete;
                var center = new Vector3(
                    bounds.MinX + (xBand.Item1 + xBand.Item2 + 1) * 0.5f * bounds.CellSize,
                    0.012f,
                    bounds.MinZ + (roadY + (yBand.Item1 + yBand.Item2 + 1) * 0.5f) * bounds.CellSize);
                var width = (xBand.Item2 - xBand.Item1 + 1) * bounds.CellSize * 0.94f;
                var depth = (yBand.Item2 - yBand.Item1 + 1) * bounds.CellSize * 0.90f;
                AddPrimitive($"VisualReviewUrbanBlock-{++blockIndex}", center,
                    new Vector3(width, 0.024f, depth), material);

                if (material != asphalt)
                    continue;
                for (var stall = -2; stall <= 2; stall++)
                {
                    AddPrimitive($"VisualReviewParkingBay-{blockIndex}-{stall}",
                        new Vector3(center.x + stall * 0.31f, 0.027f, center.z - depth * 0.34f),
                        new Vector3(0.016f, 0.006f, depth * 0.22f), marking);
                }
            }
        }

        private void SpawnInfrastructureVisualSlice(CityRuntimeContext context, int roadY)
        {
            var bounds = context.WorldBounds;
            var water = CreateWaterMaterial();
            var concrete = Resources.Load<Material>(
                "OpenSimCity/Environment/Materials/UrbanParcelConcrete") ??
                CreateShowcaseMaterial(
                    "VisualReviewBridgeConcrete", new Color(0.42f, 0.44f, 0.42f, 1f), 0.20f, 0f);
            var asphalt = Resources.Load<Material>(
                "OpenSimCity/Environment/Materials/UrbanParcelAsphalt") ??
                CreateShowcaseMaterial(
                    "VisualReviewBridgeAsphalt", new Color(0.30f, 0.31f, 0.30f, 1f), 0.10f, 0f);

            var riverZ = bounds.MinZ + (roadY + 11f) * bounds.CellSize;
            AddPrimitive("VisualReviewRiver", new Vector3(bounds.CenterX, -0.005f, riverZ),
                new Vector3((bounds.Width + 2) * bounds.CellSize, 0.08f, 4.5f * bounds.CellSize), water);
            AddPrimitive("VisualReviewRiverwalkNear", new Vector3(bounds.CenterX, 0.045f, riverZ - 2.55f),
                new Vector3((bounds.Width + 2) * bounds.CellSize, 0.09f, 0.55f), concrete);
            AddPrimitive("VisualReviewRiverwalkFar", new Vector3(bounds.CenterX, 0.045f, riverZ + 2.55f),
                new Vector3((bounds.Width + 2) * bounds.CellSize, 0.09f, 0.55f), concrete);

            var bridgeX = bounds.MinX + 28.5f * bounds.CellSize;
            var bridgePrefab = Resources.Load<GameObject>(SeoulAssetImportSetupResourceBridgePath);
            if (bridgePrefab != null)
            {
                var bridge = Instantiate(bridgePrefab, transform);
                bridge.name = "VisualReviewSeoulBridge";
                bridge.transform.localPosition = new Vector3(bridgeX, 0.10f, riverZ);
                bridge.transform.localScale = new Vector3(
                    0.75f * bounds.CellSize, bounds.CellSize, bounds.CellSize);

                // Two in-water piers, textured approaches and a consistent
                // marking vocabulary make the user-authored deck read as one
                // piece of the same road network instead of a floating prop.
                foreach (var offset in new[] { -1.55f, 1.55f })
                {
                    AddPrimitive($"VisualReviewBridgePier-{offset}",
                        new Vector3(bridgeX, 0.14f, riverZ + offset),
                        new Vector3(0.38f, 0.28f, 0.38f), concrete);
                    AddPrimitive($"VisualReviewBridgePierCap-{offset}",
                        new Vector3(bridgeX, 0.25f, riverZ + offset),
                        new Vector3(0.88f, 0.10f, 0.50f), concrete);
                }
                AddPrimitive("VisualReviewBridgeBarrierWest",
                    new Vector3(bridgeX - 0.44f, 0.58f, riverZ),
                    new Vector3(0.09f, 0.20f, 5.92f), concrete);
                AddPrimitive("VisualReviewBridgeBarrierEast",
                    new Vector3(bridgeX + 0.44f, 0.58f, riverZ),
                    new Vector3(0.09f, 0.20f, 5.92f), concrete);
                var bridgeMarking = CreateShowcaseMaterial(
                    "VisualReviewBridgeMarking", new Color(0.76f, 0.65f, 0.25f, 1f), 0.08f, 0f);
                for (var offset = -5; offset <= 5; offset += 2)
                    AddPrimitive($"VisualReviewBridgeCenterline-{offset}",
                        new Vector3(bridgeX, 0.585f, riverZ + offset * 0.50f),
                        new Vector3(0.035f, 0.008f, 0.40f), bridgeMarking);

                // Short textured transition pads overlap the bank road and the
                // opposite-bank access stub. They are intentionally horizontal:
                // the visual slice does not pretend to implement grade-aware
                // elevated-road pathfinding yet.
                AddPrimitive("VisualReviewBridgeTransitionNear",
                    new Vector3(bridgeX, 0.075f, riverZ - 3.32f),
                    new Vector3(0.92f, 0.10f, 0.72f), asphalt);
                AddPrimitive("VisualReviewBridgeTransitionFar",
                    new Vector3(bridgeX, 0.075f, riverZ + 3.32f),
                    new Vector3(0.92f, 0.10f, 0.72f), asphalt);

                AddPrimitive("VisualReviewFarBankApproach",
                    new Vector3(bridgeX, 0.045f, riverZ + 5.65f),
                    new Vector3(0.88f, 0.09f, 4.0f), asphalt);
                AddPrimitive("VisualReviewFarBankApproachLine",
                    new Vector3(bridgeX, 0.096f, riverZ + 5.65f),
                    new Vector3(0.032f, 0.008f, 3.55f), bridgeMarking);
            }
            else
            {
                Debug.LogWarning("OpenSimCity showcase could not load the grounded Seoul bridge resource.");
            }
            AddPrimitive("VisualReviewBridgeAbutmentNear", new Vector3(bridgeX, 0.20f, riverZ - 2.82f),
                new Vector3(1.04f * bounds.CellSize, 0.40f, 0.48f), concrete);
            AddPrimitive("VisualReviewBridgeAbutmentFar", new Vector3(bridgeX, 0.20f, riverZ + 2.82f),
                new Vector3(1.04f * bounds.CellSize, 0.40f, 0.48f), concrete);

            var tunnelX = bounds.MinX + 4f * bounds.CellSize;
            var tunnelZ = bounds.MinZ + (roadY - 8f) * bounds.CellSize;
            var underpassPrefab = Resources.Load<GameObject>(
                "OpenSimCity/Environment/UrbanUnderpass");
            if (underpassPrefab != null)
            {
                var underpass = Instantiate(underpassPrefab, transform);
                underpass.name = "VisualReviewYeouidoUnderpass";
                underpass.transform.localPosition = new Vector3(tunnelX, 0f, tunnelZ);
                underpass.transform.localScale = Vector3.one * bounds.CellSize;
            }
        }

        private GameObject AddPrimitive(string name, Vector3 position, Vector3 scale, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(transform, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = value.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return value;
        }

        private static Material CreateShowcaseMaterial(
            string name,
            Color color,
            float smoothness,
            float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Material CreateWaterMaterial()
        {
            var material = CreateShowcaseMaterial(
                "VisualReviewRiverWater", new Color(0.035f, 0.105f, 0.12f, 1f), 0.88f, 0f);
            var normal = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            {
                name = "VisualReviewRiverNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[128 * 128];
            for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var u = x / 128f;
                var v = y / 128f;
                var dx = 0.025f * Mathf.Cos((u * 8f + v * 3f) * Mathf.PI * 2f) +
                         0.012f * Mathf.Cos((u * 17f - v * 4f) * Mathf.PI * 2f);
                var dz = 0.018f * Mathf.Sin((u * 8f + v * 3f) * Mathf.PI * 2f) -
                         0.014f * Mathf.Sin((u * 17f - v * 4f) * Mathf.PI * 2f);
                var value = new Vector3(-dx, -dz, 1f).normalized;
                pixels[y * 128 + x] = new Color(
                    value.x * 0.5f + 0.5f,
                    value.y * 0.5f + 0.5f,
                    value.z * 0.5f + 0.5f,
                    1f);
            }
            normal.SetPixels(pixels);
            normal.Apply(false, true);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.62f);
            material.SetTextureScale("_BaseMap", new Vector2(12f, 2.5f));
            material.EnableKeyword("_NORMALMAP");
            return material;
        }

        private const string SeoulAssetImportSetupResourceBridgePath =
            "OpenSimCity/Environment/UrbanBridge";

        private static void Commit(
            CityRuntimeContext context,
            ConstructionToolMode mode,
            CellCoord start,
            CellCoord end)
        {
            context.SelectTool(mode);
            context.Tool.BeginDrag(start);
            context.Tool.UpdateDrag(end);
            var result = context.Tool.CommitDrag();
            if (!result.Committed)
                Debug.LogWarning($"OpenSimCity showcase command failed: {mode}, {result.Message}");
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FindArgumentValue(string[] arguments, string expected)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], expected, StringComparison.Ordinal))
                    return arguments[index + 1];
            }
            return string.Empty;
        }
    }
}
