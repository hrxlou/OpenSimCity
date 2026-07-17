using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Bootstrap·MainMenu·CityPrototype·Benchmark Scene과 필수 프로젝트 설정을
    /// 수동 YAML 편집 없이 Editor 메뉴로 생성·검증한다.
    /// Configure는 idempotent하며, Validate는 아무것도 변경하지 않고 원인만 보고한다.
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/OpenSimCity/Settings";
        private const string SceneFolder = "Assets/OpenSimCity/Scenes";
        private const string UrpAssetPath = SettingsFolder + "/OpenSimCityURP.asset";

        // 새 Input System만 사용(0=Old, 1=New, 2=Both). 검증에서는 1 또는 2를 허용한다.
        private const int InputHandlerNew = 1;
        private const int InputHandlerBoth = 2;

        // Configure와 Validate가 같은 기준을 쓰도록 하나의 원본으로 관리한다.
        // 순서는 build index 순서이며 Bootstrap이 index 0이어야 한다.
        private static readonly SceneDefinition[] SceneDefinitions =
        {
            new SceneDefinition("Bootstrap", "OpenSimCity.Bootstrap"),
            new SceneDefinition("MainMenu", "OpenSimCity.MainMenu"),
            new SceneDefinition("CityPrototype", "OpenSimCity.Prototype"),
            new SceneDefinition("Benchmark", "OpenSimCity.Benchmark")
        };

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
            var cameraInputActions = IsometricCameraRigSetup.EnsureInputActionsAsset();
            IsometricCameraRigSetup.ConfigureCityPrototype(cameraInputActions);

            // Runs after the camera rig because the UI boundary relay binds to the
            // GridSelectionTool the rig setup creates.
            UiFoundationSetup.ConfigureCityPrototype(UiFoundationSetup.EnsurePanelSettings());

            // Runs last: the placement fixture binds to the camera rig, the world
            // bounds source and the UI foundation the previous steps created.
            PlacementSetup.ConfigureCityPrototype();
            BuildingDefinitionSetup.ConfigureDefinitions();
            UtilityDefinitionSetup.ConfigureDefinitions();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity project setup completed. Run 'OpenSimCity/Setup/Validate Project' to confirm.");
        }

        [MenuItem("OpenSimCity/Setup/Validate Project")]
        public static void ValidateProjectMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("OpenSimCity project validation passed: scenes, URP, input, and build settings are correct.");
                return;
            }

            foreach (var issue in issues)
                Debug.LogError($"OpenSimCity project validation: {issue}");

            Debug.LogError(
                $"OpenSimCity project validation failed with {issues.Count} issue(s). " +
                "Run 'OpenSimCity/Setup/Configure Project' or fix the listed settings, then validate again.");
        }

        /// <summary>
        /// 프로젝트 설정을 변경 없이 검사하고 발견한 문제를 원인별로 반환한다.
        /// 반환 목록이 비어 있으면 설정이 모두 정상이다. 테스트에서도 호출한다.
        /// </summary>
        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            ValidateEditorSettings(issues);
            ValidateUrp(issues);
            ValidateInputSystem(issues);
            IsometricCameraRigSetup.Validate(issues);
            UiFoundationSetup.Validate(issues);
            PlacementSetup.Validate(issues);
            foreach (var issue in BuildingDefinitionSetup.Validate())
                issues.Add(issue);
            foreach (var issue in UtilityDefinitionSetup.Validate())
                issues.Add(issue);
            ValidateScenesAndBuildSettings(issues);
            ValidateLightingAndQualityStructure(issues);
            return issues;
        }

        private static void ConfigureEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
#pragma warning disable CS0618
            EditorSettings.externalVersionControl = "Visible Meta Files";
#pragma warning restore CS0618
        }

        private static void ValidateEditorSettings(ICollection<string> issues)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                issues.Add(
                    $"Serialization mode is '{EditorSettings.serializationMode}' but must be 'ForceText' " +
                    "so scenes and assets produce stable, reviewable diffs.");
            }
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

            // 이미 같은 에셋이 할당돼 있으면 다시 dirty로 만들지 않아 재실행 diff를 막는다.
            if (!ReferenceEquals(GraphicsSettings.defaultRenderPipeline, urpAsset))
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
            }

            if (!ReferenceEquals(QualitySettings.renderPipeline, urpAsset))
                QualitySettings.renderPipeline = urpAsset;
        }

        private static void ValidateUrp(ICollection<string> issues)
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset == null)
            {
                issues.Add(
                    $"URP asset is missing at '{UrpAssetPath}'. " +
                    "Run Configure Project to create it.");
                return;
            }

            if (urpAsset.rendererDataList.Length == 0 || urpAsset.rendererDataList[0] == null)
                issues.Add($"URP asset at '{UrpAssetPath}' has no renderer assigned.");

            if (!ReferenceEquals(GraphicsSettings.defaultRenderPipeline, urpAsset))
            {
                issues.Add(
                    "Graphics Settings default render pipeline does not reference " +
                    $"'{UrpAssetPath}'. URP will not be active for builds.");
            }

            if (!ReferenceEquals(QualitySettings.renderPipeline, urpAsset) &&
                GraphicsSettings.defaultRenderPipeline == null)
            {
                issues.Add(
                    "Neither Quality nor Graphics settings reference the OpenSimCity URP asset; " +
                    "the project would fall back to the built-in pipeline.");
            }
        }

        private static void ValidateInputSystem(ICollection<string> issues)
        {
            // activeInputHandler에는 공개 API가 없어 PlayerSettings 자산을 직접 읽는다.
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .FirstOrDefault();
            if (playerSettings == null)
            {
                issues.Add("Could not read ProjectSettings.asset to verify the active input handler.");
                return;
            }

            var serialized = new SerializedObject(playerSettings);
            var handlerProperty = serialized.FindProperty("activeInputHandler");
            if (handlerProperty == null)
            {
                issues.Add("activeInputHandler property not found in PlayerSettings; cannot verify Input System.");
                return;
            }

            var handler = handlerProperty.intValue;
            if (handler != InputHandlerNew && handler != InputHandlerBoth)
            {
                issues.Add(
                    "Active Input Handling is set to the legacy Input Manager only. " +
                    "Set it to 'Input System Package (New)' or 'Both' in " +
                    "Project Settings > Player > Active Input Handling (requires an editor restart).");
            }
        }

        // Unity can normalize these live values differently per platform, GPU, or Editor session.
        // Validate only the serialized shape here. The exact committed baseline is checked by
        // Tools/CI/validate-projectsettings-baseline.* against the Git index.
        private static void ValidateLightingAndQualityStructure(ICollection<string> issues)
        {
            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")
                .FirstOrDefault();
            if (graphicsSettings == null)
            {
                issues.Add("Could not read GraphicsSettings.asset to verify the serialized lighting settings shape.");
            }
            else
            {
                var colorTemperatureProperty =
                    new SerializedObject(graphicsSettings).FindProperty("m_LightsUseColorTemperature");
                if (colorTemperatureProperty == null)
                {
                    issues.Add("GraphicsSettings.asset is missing m_LightsUseColorTemperature; cannot verify the serialized lighting settings shape.");
                }
            }

            var qualitySettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")
                .FirstOrDefault();
            if (qualitySettings == null)
            {
                issues.Add("Could not read QualitySettings.asset to verify the serialized quality settings shape.");
                return;
            }

            var levels = new SerializedObject(qualitySettings).FindProperty("m_QualitySettings");
            const int expectedQualityLevelCount = 6;
            if (levels == null || levels.arraySize != expectedQualityLevelCount)
            {
                issues.Add(
                    $"QualitySettings has {(levels == null ? "an unreadable" : levels.arraySize.ToString())} " +
                    $"quality level(s) but {expectedQualityLevelCount} were expected.");
                return;
            }

            for (var i = 0; i < expectedQualityLevelCount; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var levelName = level.FindPropertyRelative("name")?.stringValue ?? $"index {i}";
                var antiAliasing = level.FindPropertyRelative("antiAliasing");
                if (antiAliasing == null)
                {
                    issues.Add($"Quality level '{levelName}' is missing the antiAliasing property.");
                }
            }
        }

        private static void CreateScenes()
        {
            EnsureFolder(SceneFolder);

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var ownedScenePaths = new HashSet<string>(StringComparer.Ordinal);
            var buildScenes = new List<EditorBuildSettingsScene>(SceneDefinitions.Length);

            try
            {
                foreach (var definition in SceneDefinitions)
                {
                    var scenePath = ScenePath(definition);
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

        private static void ValidateScenesAndBuildSettings(ICollection<string> issues)
        {
            var buildScenes = EditorBuildSettings.scenes;

            for (var index = 0; index < SceneDefinitions.Length; index++)
            {
                var definition = SceneDefinitions[index];
                var scenePath = ScenePath(definition);

                if (!File.Exists(scenePath))
                {
                    issues.Add($"Scene '{definition.Name}' is missing at '{scenePath}'. Run Configure Project.");
                    continue;
                }

                ValidateSceneRoot(definition, scenePath, issues);
                ValidateSceneBuildEntry(definition, scenePath, index, buildScenes, issues);
            }
        }

        private static void ValidateSceneRoot(
            SceneDefinition definition, string scenePath, ICollection<string> issues)
        {
            // 열려 있는 Scene을 흩뜨리지 않도록 Additive로 잠깐 열고 확인 후 닫는다.
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var hasRequiredRoot = scene.GetRootGameObjects()
                    .Any(root => string.Equals(root.name, definition.RequiredRootName, StringComparison.Ordinal));

                if (!hasRequiredRoot)
                {
                    issues.Add(
                        $"Scene '{definition.Name}' is missing its required root object " +
                        $"'{definition.RequiredRootName}'.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ValidateSceneBuildEntry(
            SceneDefinition definition,
            string scenePath,
            int expectedIndex,
            EditorBuildSettingsScene[] buildScenes,
            ICollection<string> issues)
        {
            var actualIndex = Array.FindIndex(
                buildScenes, s => string.Equals(s.path, scenePath, StringComparison.Ordinal));

            if (actualIndex < 0)
            {
                issues.Add(
                    $"Scene '{definition.Name}' is not registered in Build Settings. " +
                    "Run Configure Project to add it.");
                return;
            }

            if (!buildScenes[actualIndex].enabled)
                issues.Add($"Scene '{definition.Name}' is present in Build Settings but disabled.");

            if (actualIndex != expectedIndex)
            {
                issues.Add(
                    $"Scene '{definition.Name}' has build index {actualIndex} but should be at " +
                    $"index {expectedIndex}; Bootstrap must stay first as the startup scene.");
            }
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

        private static string ScenePath(SceneDefinition definition) => $"{SceneFolder}/{definition.Name}.unity";

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
