using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenSimCity.Editor
{
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/OpenSimCity/Settings";
        private const string SceneFolder = "Assets/OpenSimCity/Scenes";
        private const string UrpAssetPath = SettingsFolder + "/OpenSimCityURP.asset";

        [MenuItem("OpenSimCity/Setup/Configure Project")]
        public static void ConfigureProject()
        {
            ConfigureEditorSettings();
            ConfigureUrp();
            CreateScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity project setup completed.");
        }

        private static void ConfigureEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.externalVersionControl = "Visible Meta Files";
        }

        private static void ConfigureUrp()
        {
            EnsureFolder(SettingsFolder);

            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset != null &&
                (urpAsset.rendererDataList.Length == 0 || urpAsset.rendererDataList[0] == null))
            {
                AssetDatabase.DeleteAsset(UrpAssetPath);
                urpAsset = null;
            }

            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create();
                var rendererData = urpAsset.rendererDataList[0];
                AssetDatabase.CreateAsset(urpAsset, UrpAssetPath);
                AssetDatabase.AddObjectToAsset(rendererData, urpAsset);
                AssetDatabase.ImportAsset(UrpAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;
            EditorUtility.SetDirty(urpAsset);
        }

        private static void CreateScenes()
        {
            EnsureFolder(SceneFolder);

            var sceneNames = new[]
            {
                "Bootstrap",
                "MainMenu",
                "CityPrototype",
                "Benchmark"
            };

            var buildScenes = new EditorBuildSettingsScene[sceneNames.Length];
            for (var i = 0; i < sceneNames.Length; i++)
            {
                var scenePath = $"{SceneFolder}/{sceneNames[i]}.unity";
                if (!File.Exists(scenePath))
                {
                    var scene = EditorSceneManager.NewScene(
                        NewSceneSetup.DefaultGameObjects,
                        NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, scenePath);
                }

                buildScenes[i] = new EditorBuildSettingsScene(scenePath, true);
            }

            EditorBuildSettings.scenes = buildScenes;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
