using System;
using System.IO;
using System.Linq;
using OpenSimCity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Creates a deterministic off-screen contact sheet of product prefabs. This
    /// remains usable in batch mode when desktop capture is unavailable.
    /// </summary>
    public static class ProductArtPreviewCapture
    {
        private const string OutputArgument = "-osc-output";
        private const string DefaultOutput = "/tmp/opensimcity-product-art.png";

        [MenuItem("OpenSimCity/Benchmark/Capture Product Art Contact Sheet")]
        public static void Capture()
        {
            var output = FindArgumentValue(Environment.GetCommandLineArgs(), OutputArgument);
            if (string.IsNullOrWhiteSpace(output))
                output = DefaultOutput;

            var catalog = AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(
                ProductArtSetup.ProductCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Product art catalog must exist before capture.");

            var prefabs = catalog.Bindings
                .Where(binding => binding?.Prefab != null &&
                                  binding.Prefab.name.StartsWith("PRD_", StringComparison.Ordinal))
                .OrderBy(binding => binding.PresentationKey, StringComparer.Ordinal)
                .ToArray();
            if (prefabs.Length == 0)
                throw new InvalidOperationException("Product art catalog contains no product prefabs.");

            var previousScene = SceneManager.GetActiveScene();
            if (!UnityEngine.Application.isBatchMode && previousScene.isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var canOpenAdditively = previousScene.IsValid() &&
                                    !string.IsNullOrWhiteSpace(previousScene.path);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                canOpenAdditively ? NewSceneMode.Additive : NewSceneMode.Single);
            if (canOpenAdditively)
                SceneManager.SetActiveScene(scene);
            try
            {
                ConfigureStage(scene, prefabs);
                Render(scene, output);
                Debug.Log($"OpenSimCity product art contact sheet captured to '{output}'.");
            }
            finally
            {
                if (canOpenAdditively && previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ConfigureStage(Scene scene, PlaceholderPrefabBinding[] prefabs)
        {
            var streetReview = Environment.GetCommandLineArgs().Contains("-osc-street");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "PreviewGround";
            MoveToScene(ground, scene);
            ground.transform.position = new Vector3(0f, -0.08f, 0f);
            ground.transform.localScale = new Vector3(12f, 0.12f, 6.5f);
            var groundMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMaterial.color = new Color(0.30f, 0.38f, 0.31f, 1f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            for (var index = 0; index < prefabs.Length; index++)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefabs[index].Prefab, scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException($"Could not instantiate '{prefabs[index].Prefab.name}'.");
                var column = index % 5;
                var row = index / 5;
                instance.transform.position = streetReview
                    ? new Vector3(-3.2f + column * 1.6f, 0f, row == 0 ? -1.2f : 1.2f)
                    : new Vector3(-4.4f + column * 2.2f, 0f, -1.4f + row * 2.8f);
                instance.transform.rotation = Quaternion.Euler(0f, streetReview ? (row == 0 ? 180f : 0f) : (row == 0 ? 12f : -12f), 0f);
                var lodGroups = instance.GetComponentsInChildren<LODGroup>(true);
                for (var lodIndex = 0; lodIndex < lodGroups.Length; lodIndex++)
                    lodGroups[lodIndex].ForceLOD(0);
            }

            if (streetReview)
            {
                var road = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderArtSetup.MaterialFolder + "/MAT_Placeholder_Road.mat");
                var curb = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderArtSetup.MaterialFolder + "/MAT_Placeholder_Concrete.mat");
                foreach (var z in new[] { -.62f, .62f })
                {
                    var sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    MoveToScene(sidewalk, scene);
                    sidewalk.transform.position = new Vector3(0f, .015f, z);
                    sidewalk.transform.localScale = new Vector3(9f, .07f, .24f);
                    sidewalk.GetComponent<Renderer>().sharedMaterial = curb;
                }
                var asphalt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MoveToScene(asphalt, scene);
                asphalt.transform.position = Vector3.zero;
                asphalt.transform.localScale = new Vector3(9f, .03f, 1f);
                asphalt.GetComponent<Renderer>().sharedMaterial = road;
            }

            var lightObject = new GameObject("PreviewKeyLight", typeof(Light));
            MoveToScene(lightObject, scene);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.91f, 0.78f, 1f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var fillObject = new GameObject("PreviewFillLight", typeof(Light));
            MoveToScene(fillObject, scene);
            var fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.66f, 0.80f, 0.90f, 1f);
            fill.intensity = 0.48f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.53f, 0.50f, 1f);
        }

        private static void Render(Scene scene, string output)
        {
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            MoveToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.18f, 0.20f, 1f);
            camera.transform.position = new Vector3(7.6f, 7.8f, -10.5f);
            camera.transform.LookAt(new Vector3(0f, 0.5f, 0f));

            var target = new RenderTexture(1440, 810, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var image = new Texture2D(1440, 810, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
                {
                    destination = target
                };
                if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException(
                        "URP is not initialized for this camera. Capture from a running Editor after a rendered frame; do not accept a blank batch capture.");
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();
                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(output, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        private static void MoveToScene(GameObject value, Scene scene)
        {
            SceneManager.MoveGameObjectToScene(value, scene);
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
