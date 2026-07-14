using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("OpenSimCity project setup cancelled because an open scene has unsaved changes.");
                return;
            }

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
#pragma warning disable CS0618
            EditorSettings.externalVersionControl = "Visible Meta Files";
#pragma warning restore CS0618
        }

        private static void ConfigureUrp()
        {
            EnsureFolder(SettingsFolder);

            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset != null &&
                (urpAsset.rendererDataList.Length == 0 || urpAsset.rendererDataList[0] == null))
            {
                throw new InvalidOperationException(
                    $"The URP asset at {UrpAssetPath} has no renderer. " +
                    "Repair or intentionally replace it instead of deleting configured project settings.");
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

            var sceneDefinitions = new[]
            {
                new SceneDefinition("Bootstrap", "OpenSimCity.Bootstrap"),
                new SceneDefinition("MainMenu", "OpenSimCity.MainMenu"),
                new SceneDefinition("CityPrototype", "OpenSimCity.Prototype"),
                new SceneDefinition("Benchmark", "OpenSimCity.Benchmark")
            };

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var ownedScenePaths = new HashSet<string>(StringComparer.Ordinal);
            var buildScenes = new List<EditorBuildSettingsScene>(sceneDefinitions.Length);

            try
            {
                foreach (var definition in sceneDefinitions)
                {
                    var scenePath = $"{SceneFolder}/{definition.Name}.unity";
                    EnsureScene(scenePath, definition.RequiredRootName);
                    ownedScenePaths.Add(scenePath);
                    buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }
            finally
            {
                if (previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            foreach (var existingScene in EditorBuildSettings.scenes)
            {
                if (!ownedScenePaths.Contains(existingScene.path))
                    buildScenes.Add(existingScene);
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        private static void EnsureScene(string scenePath, string requiredRootName)
        {
            Scene scene;
            if (File.Exists(scenePath))
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            var hasRequiredRoot = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, requiredRootName, StringComparison.Ordinal))
                {
                    hasRequiredRoot = true;
                    break;
                }
            }

            if (!hasRequiredRoot)
            {
                var requiredRoot = new GameObject(requiredRootName);
                SceneManager.MoveGameObjectToScene(requiredRoot, scene);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (!File.Exists(scenePath) || scene.isDirty)
                EditorSceneManager.SaveScene(scene, scenePath);
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

        private readonly struct SceneDefinition
        {
            public SceneDefinition(string name, string requiredRootName)
            {
                Name = name;
                RequiredRootName = requiredRootName;
            }

            public string Name { get; }

            public string RequiredRootName { get; }
        }
    }
}
