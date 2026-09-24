using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenSimCity.Editor
{
    /// <summary>Renders the generated traffic silhouettes without requiring a player window.</summary>
    public static class VehicleArtPreviewCapture
    {
        private const string OutputArgument = "-osc-output";
        private const string DefaultOutput = "/tmp/opensimcity-vehicle-art.png";

        [MenuItem("OpenSimCity/Benchmark/Capture Vehicle Art")]
        public static void Capture()
        {
            var output = FindArgumentValue(Environment.GetCommandLineArgs(), OutputArgument);
            if (string.IsNullOrWhiteSpace(output)) output = DefaultOutput;
            var car = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtSetup.TrafficPrefabPath);
            var bus = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleArtSetup.BusPrefabPath);
            if (car == null || bus == null)
                throw new InvalidOperationException("Vehicle art must be configured before capture.");

            var previous = SceneManager.GetActiveScene();
            var additive = previous.IsValid() && !string.IsNullOrWhiteSpace(previous.path);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            if (additive) SceneManager.SetActiveScene(scene);
            try
            {
                ConfigureStage(scene, car, bus);
                Render(scene, output);
                Debug.Log($"OpenSimCity vehicle art captured to '{output}'.");
            }
            finally
            {
                if (additive && previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ConfigureStage(Scene scene, GameObject car, GameObject bus)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(0f, -0.14f, 0f);
            ground.transform.localScale = new Vector3(9f, 0.2f, 5f);
            var groundMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMaterial.color = new Color(0.12f, 0.18f, 0.19f, 1f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            Place(scene, car, new Vector3(-2.8f, 0f, -0.7f), 22f);
            Place(scene, car, new Vector3(-0.9f, 0f, 0.5f), -18f);
            Place(scene, car, new Vector3(1.0f, 0f, -0.9f), 36f);
            Place(scene, bus, new Vector3(2.6f, 0f, 0.55f), -24f);

            var keyObject = new GameObject("Key", typeof(Light));
            SceneManager.MoveGameObjectToScene(keyObject, scene);
            var key = keyObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.91f, 0.78f, 1f);
            key.intensity = 1.4f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fillObject = new GameObject("Fill", typeof(Light));
            SceneManager.MoveGameObjectToScene(fillObject, scene);
            var fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.58f, 0.78f, 0.90f, 1f);
            fill.intensity = 0.5f;
            fillObject.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.44f, 0.44f, 1f);
        }

        private static void Place(Scene scene, GameObject prefab, Vector3 position, float yaw)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException($"Could not instantiate '{prefab.name}'.");
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * 1.4f;
        }

        private static void Render(Scene scene, string output)
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.09f, 0.10f, 1f);
            camera.transform.position = new Vector3(7f, 6.5f, -9f);
            camera.transform.LookAt(new Vector3(0f, 0.45f, 0f));

            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(output, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
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
