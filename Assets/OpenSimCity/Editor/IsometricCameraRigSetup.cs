using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Selection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Reproducibly creates the CityPrototype camera rig and its Input System asset.
    /// The scene graph is changed through Unity APIs rather than hand-edited YAML.
    /// </summary>
    public static class IsometricCameraRigSetup
    {
        public const string CityPrototypeScenePath = "Assets/OpenSimCity/Scenes/CityPrototype.unity";
        public const string InputActionsAssetPath = "Assets/OpenSimCity/Settings/IsometricCamera.inputactions";
        public const string RigName = "IsometricCameraRig";
        public const string RequiredRootName = "OpenSimCity.Prototype";
        public const string SelectionRootName = "GridSelection";
        public const string DebugGridName = "DebugGrid";
        public const string HighlightName = "SelectionHighlight";
        public const string WorldBackdropName = "WorldBackdrop";
        public const string SelectionMaterialPath = "Assets/OpenSimCity/Content/Materials/OpenSimCityDebugGrid.mat";
        public const string GridLineMaterialPath = "Assets/OpenSimCity/Content/Materials/OpenSimCityGridLines.mat";
        public const string GroundMaterialPath = "Assets/OpenSimCity/Content/Materials/OpenSimCityGround.mat";

        [MenuItem("OpenSimCity/Setup/Configure Camera Rig")]
        public static void ConfigureCameraRigMenu()
        {
            var inputActions = EnsureInputActionsAsset();
            ConfigureCityPrototype(inputActions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity camera rig and Input System actions configured.");
        }

        public static InputActionAsset EnsureInputActionsAsset()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var inputActions = LoadInputActionsAsset();
            if (inputActions == null)
            {
                WriteInputActionsJsonAsset();
                inputActions = LoadInputActionsAsset();
                if (inputActions == null)
                {
                    throw new InvalidOperationException(
                        $"Could not import generated Input System asset '{InputActionsAssetPath}'.");
                }
            }
            else if (!IsometricCameraInputActions.HasRequiredActions(inputActions) ||
                     !GridSelectionInputActions.HasRequiredActions(inputActions))
            {
                // .inputactions files are JSON source assets. Persist the updated
                // JSON then reimport instead of only dirtying the imported object.
                IsometricCameraInputActions.ConfigureAsset(inputActions);
                WriteInputActionsJsonAsset(inputActions);
                inputActions = LoadInputActionsAsset();
                if (inputActions == null)
                {
                    throw new InvalidOperationException(
                        $"Could not reimport generated Input System asset '{InputActionsAssetPath}'.");
                }
            }

            return inputActions;
        }

        private static void WriteInputActionsJsonAsset(InputActionAsset sourceAsset = null)
        {
            var generatedAsset = sourceAsset ?? IsometricCameraInputActions.CreateDefaultAsset();
            var ownsGeneratedAsset = sourceAsset == null;
            try
            {
                File.WriteAllText(InputActionsAssetPath, generatedAsset.ToJson());
            }
            finally
            {
                if (ownsGeneratedAsset)
                    UnityEngine.Object.DestroyImmediate(generatedAsset);
            }

            AssetDatabase.ImportAsset(
                InputActionsAssetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static InputActionAsset LoadInputActionsAsset()
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (inputActions != null)
                return inputActions;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(InputActionsAssetPath);
            foreach (var asset in allAssets)
            {
                if (asset is InputActionAsset actionAsset)
                    return actionAsset;
            }

            return null;
        }

        public static void ConfigureCityPrototype(InputActionAsset inputActions)
        {
            if (inputActions == null)
                throw new ArgumentNullException(nameof(inputActions));

            var previousScene = SceneManager.GetActiveScene();
            var scene = GetOrOpenScene(CityPrototypeScenePath, out var ownsScene);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = scene.GetRootGameObjects()
                    .FirstOrDefault(gameObject => string.Equals(gameObject.name, RequiredRootName, StringComparison.Ordinal));
                if (root == null)
                {
                    throw new InvalidOperationException(
                        $"Scene '{CityPrototypeScenePath}' is missing its required root '{RequiredRootName}'.");
                }

                var rigObject = root.transform.Find(RigName)?.gameObject;
                var sceneChanged = false;
                if (rigObject == null)
                {
                    rigObject = new GameObject(RigName);
                    SceneManager.MoveGameObjectToScene(rigObject, scene);
                    rigObject.transform.SetParent(root.transform, worldPositionStays: false);
                    sceneChanged = true;
                }

                var boundsSource = rigObject.GetComponent<WorldBoundsReadModelSource>();
                if (boundsSource == null)
                {
                    boundsSource = rigObject.AddComponent<WorldBoundsReadModelSource>();
                    sceneChanged = true;
                }

                var camera = FindCityCamera(scene);
                if (camera == null)
                {
                    var cameraObject = new GameObject("Main Camera", typeof(UnityCamera), typeof(AudioListener));
                    cameraObject.tag = "MainCamera";
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    camera = cameraObject.GetComponent<UnityCamera>();
                    sceneChanged = true;
                }

                if (camera.transform.parent != rigObject.transform)
                {
                    camera.transform.SetParent(rigObject.transform, worldPositionStays: false);
                    sceneChanged = true;
                }

                if (!string.Equals(camera.gameObject.tag, "MainCamera", StringComparison.Ordinal))
                {
                    camera.gameObject.tag = "MainCamera";
                    sceneChanged = true;
                }

                var rig = rigObject.GetComponent<IsometricCameraRig>();
                if (rig == null)
                {
                    rig = rigObject.AddComponent<IsometricCameraRig>();
                    sceneChanged = true;
                }

                rig.ConfigureReferences(camera, boundsSource, inputActions);
                rig.SetWorldBounds(boundsSource.ReadWorldBounds());
                var serializedRig = new SerializedObject(rig);
                serializedRig.FindProperty("controlledCamera").objectReferenceValue = camera;
                serializedRig.FindProperty("worldBoundsSource").objectReferenceValue = boundsSource;
                serializedRig.FindProperty("inputActions").objectReferenceValue = inputActions;
                serializedRig.FindProperty("initialOrthographicSize").floatValue = 14f;
                SetSerializedComponentName(serializedRig, RigName);
                serializedRig.ApplyModifiedPropertiesWithoutUndo();

                var serializedBounds = new SerializedObject(boundsSource);
                SetSerializedComponentName(serializedBounds, "WorldBoundsReadModelSource");
                serializedBounds.ApplyModifiedPropertiesWithoutUndo();

                camera.orthographic = true;
                camera.nearClipPlane = Mathf.Max(0.05f, camera.nearClipPlane);
                camera.farClipPlane = Mathf.Max(200f, camera.farClipPlane);
                ConfigureWorldPresentation(root, scene, camera, boundsSource);
                EditorUtility.SetDirty(rig);
                EditorUtility.SetDirty(boundsSource);
                EditorUtility.SetDirty(camera);

                ConfigureGridSelection(root, rig, boundsSource, inputActions, ref sceneChanged);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ConfigureWorldPresentation(
            GameObject root,
            Scene scene,
            UnityCamera camera,
            WorldBoundsReadModelSource boundsSource)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.39f, 0.55f, 0.61f, 1f);
            camera.allowHDR = true;
            camera.allowMSAA = true;

            UrbanEnvironmentSetup.ConfigureReflections();
            UrbanEnvironmentSetup.ConfigureColorPipeline(camera, scene);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.57f, 0.62f, 0.67f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.41f, 0.44f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.19f, 0.18f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.43f, 0.48f, 0.50f, 1f);
            RenderSettings.fogDensity = 0.0020f;

            var sun = scene.GetRootGameObjects()
                .SelectMany(candidate => candidate.GetComponentsInChildren<Light>(includeInactive: true))
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
                sun.color = new Color(1f, 0.93f, 0.84f, 1f);
                sun.intensity = 2.1f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.92f;
                sun.shadowBias = 0.025f;
                sun.shadowNormalBias = 0.06f;
                RenderSettings.sun = sun;
                EditorUtility.SetDirty(sun);
            }

            var material = EnsureGroundMaterial();
            var backdrop = root.transform.Find(WorldBackdropName)?.gameObject;
            if (backdrop == null)
            {
                backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backdrop.name = WorldBackdropName;
                backdrop.transform.SetParent(root.transform, worldPositionStays: false);
            }

            var bounds = boundsSource.ReadWorldBounds();
            var width = (bounds.Width + 2) * bounds.CellSize;
            var depth = (bounds.Height + 2) * bounds.CellSize;
            backdrop.transform.localPosition = new Vector3(
                (bounds.MinX + bounds.MaxX) * 0.5f,
                -0.12f,
                (bounds.MinZ + bounds.MaxZ) * 0.5f);
            backdrop.transform.localRotation = Quaternion.identity;
            backdrop.transform.localScale = new Vector3(width, 0.20f, depth);
            var collider = backdrop.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            backdrop.GetComponent<Renderer>().sharedMaterial = material;
            EditorUtility.SetDirty(backdrop);
        }

        private static Material EnsureGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "OpenSimCity Ground" };
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }

            var color = new Color(0.27f, 0.31f, 0.285f, 1f);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureGridSelection(
            GameObject root,
            IsometricCameraRig rig,
            WorldBoundsReadModelSource boundsSource,
            InputActionAsset inputActions,
            ref bool sceneChanged)
        {
            var selectionMaterial = EnsureSelectionMaterial();
            var gridMaterial = EnsureGridLineMaterial();
            var selectionRoot = root.transform.Find(SelectionRootName)?.gameObject;
            if (selectionRoot == null)
            {
                selectionRoot = new GameObject(SelectionRootName);
                selectionRoot.transform.SetParent(root.transform, worldPositionStays: false);
                sceneChanged = true;
            }

            var debugGridObject = selectionRoot.transform.Find(DebugGridName)?.gameObject;
            if (debugGridObject == null)
            {
                debugGridObject = new GameObject(DebugGridName);
                debugGridObject.transform.SetParent(selectionRoot.transform, worldPositionStays: false);
                sceneChanged = true;
            }

            var debugGrid = debugGridObject.GetComponent<GridDebugGridRenderer>();
            if (debugGrid == null)
            {
                debugGrid = debugGridObject.AddComponent<GridDebugGridRenderer>();
                sceneChanged = true;
            }
            debugGrid.Configure(boundsSource, gridMaterial);

            var highlightObject = selectionRoot.transform.Find(HighlightName)?.gameObject;
            if (highlightObject == null)
            {
                highlightObject = new GameObject(HighlightName);
                highlightObject.transform.SetParent(selectionRoot.transform, worldPositionStays: false);
                sceneChanged = true;
            }

            var highlight = highlightObject.GetComponent<GridSelectionHighlightView>();
            if (highlight == null)
            {
                highlight = highlightObject.AddComponent<GridSelectionHighlightView>();
                sceneChanged = true;
            }
            highlight.Configure(selectionMaterial);

            var tool = selectionRoot.GetComponent<GridSelectionTool>();
            if (tool == null)
            {
                tool = selectionRoot.AddComponent<GridSelectionTool>();
                sceneChanged = true;
            }
            tool.ConfigureReferences(rig, highlight, inputActions);

            ApplySelectionSerializedReferences(
                debugGrid,
                highlight,
                tool,
                boundsSource,
                rig,
                gridMaterial,
                selectionMaterial,
                inputActions);
            EditorUtility.SetDirty(debugGrid);
            EditorUtility.SetDirty(highlight);
            EditorUtility.SetDirty(tool);
        }

        private static Material EnsureSelectionMaterial()
        {
            return EnsureUnlitMaterial(
                SelectionMaterialPath,
                "OpenSimCity Selection Highlight",
                new Color(0.39f, 0.84f, 0.77f, 1f));
        }

        private static Material EnsureGridLineMaterial()
        {
            return EnsureUnlitMaterial(
                GridLineMaterialPath,
                "OpenSimCity Grid Lines",
                new Color(0.20f, 0.275f, 0.255f, 1f));
        }

        private static Material EnsureUnlitMaterial(string path, string name, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.name = name;
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
                EditorUtility.SetDirty(material);
                return material;
            }

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
                EnsureAssetFolder(folder);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("Could not find an unlit shader for the P1 debug grid material.");

            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ApplySelectionSerializedReferences(
            GridDebugGridRenderer debugGrid,
            GridSelectionHighlightView highlight,
            GridSelectionTool tool,
            WorldBoundsReadModelSource boundsSource,
            IsometricCameraRig rig,
            Material gridMaterial,
            Material selectionMaterial,
            InputActionAsset inputActions)
        {
            var serializedGrid = new SerializedObject(debugGrid);
            serializedGrid.FindProperty("worldBoundsSource").objectReferenceValue = boundsSource;
            serializedGrid.FindProperty("lineMaterial").objectReferenceValue = gridMaterial;
            SetSerializedComponentName(serializedGrid, DebugGridName);
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();

            var serializedHighlight = new SerializedObject(highlight);
            serializedHighlight.FindProperty("lineMaterial").objectReferenceValue = selectionMaterial;
            SetSerializedComponentName(serializedHighlight, HighlightName);
            serializedHighlight.ApplyModifiedPropertiesWithoutUndo();

            var serializedTool = new SerializedObject(tool);
            serializedTool.FindProperty("cameraRig").objectReferenceValue = rig;
            serializedTool.FindProperty("highlightView").objectReferenceValue = highlight;
            serializedTool.FindProperty("inputActions").objectReferenceValue = inputActions;
            SetSerializedComponentName(serializedTool, SelectionRootName);
            serializedTool.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Validate(ICollection<string> issues)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var inputActions = LoadInputActionsAsset();
            if (inputActions == null)
            {
                issues.Add($"Camera Input System asset is missing at '{InputActionsAssetPath}'. Run Configure Project.");
            }
            else if (!IsometricCameraInputActions.HasRequiredActions(inputActions))
            {
                issues.Add(
                    $"Camera Input System asset at '{InputActionsAssetPath}' is missing one or more required actions.");
            }
            else if (!GridSelectionInputActions.HasRequiredActions(inputActions))
            {
                issues.Add(
                    $"Input System asset at '{InputActionsAssetPath}' is missing required selection actions.");
            }

            if (!System.IO.File.Exists(CityPrototypeScenePath))
                return;

            var scene = GetOrOpenScene(CityPrototypeScenePath, out var ownsScene);
            try
            {
                var root = scene.GetRootGameObjects()
                    .FirstOrDefault(gameObject => string.Equals(gameObject.name, RequiredRootName, StringComparison.Ordinal));
                var rigObject = root?.transform.Find(RigName)?.gameObject;
                var rig = rigObject?.GetComponent<IsometricCameraRig>();
                var boundsSource = rigObject?.GetComponent<WorldBoundsReadModelSource>();

                if (rig == null)
                {
                    issues.Add(
                        $"CityPrototype is missing '{RigName}' with an IsometricCameraRig component. " +
                        "Run Configure Project.");
                    return;
                }

                if (rig.ControlledCamera == null)
                    issues.Add("CityPrototype IsometricCameraRig has no controlled camera reference.");
                else if (!rig.ControlledCamera.orthographic)
                    issues.Add("CityPrototype camera rig must use an orthographic camera.");

                if (root.transform.Find(WorldBackdropName) == null)
                    issues.Add($"CityPrototype is missing '{WorldBackdropName}'. Run Configure Project.");
                if (AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath) == null)
                    issues.Add($"CityPrototype ground material is missing at '{GroundMaterialPath}'.");

                if (boundsSource == null)
                    issues.Add("CityPrototype IsometricCameraRig has no world-bounds read-model source.");

                if (inputActions != null && rig.ConfiguredInputActions != inputActions)
                {
                    issues.Add(
                        "CityPrototype IsometricCameraRig is not wired to the committed " +
                        $"Input System asset '{InputActionsAssetPath}'.");
                }

                ValidateGridSelection(root, rig, inputActions, issues);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ValidateGridSelection(
            GameObject root,
            IsometricCameraRig rig,
            InputActionAsset inputActions,
            ICollection<string> issues)
        {
            var selectionRoot = root.transform.Find(SelectionRootName)?.gameObject;
            var tool = selectionRoot?.GetComponent<GridSelectionTool>();
            var grid = selectionRoot?.transform.Find(DebugGridName)?.GetComponent<GridDebugGridRenderer>();
            var highlight = selectionRoot?.transform.Find(HighlightName)?.GetComponent<GridSelectionHighlightView>();

            if (tool == null)
                issues.Add($"CityPrototype is missing '{SelectionRootName}' with a GridSelectionTool. Run Configure Project.");
            else
            {
                if (tool.CameraRig != rig)
                    issues.Add("CityPrototype GridSelectionTool is not wired to the IsometricCameraRig.");
                if (tool.HighlightView != highlight)
                    issues.Add("CityPrototype GridSelectionTool is not wired to its single selection highlight view.");
                if (inputActions != null && tool.ConfiguredInputActions != inputActions)
                    issues.Add("CityPrototype GridSelectionTool is not wired to the committed Input System asset.");
            }

            if (grid == null)
                issues.Add($"CityPrototype is missing '{DebugGridName}' with a GridDebugGridRenderer. Run Configure Project.");
            if (highlight == null)
                issues.Add($"CityPrototype is missing '{HighlightName}' with a GridSelectionHighlightView. Run Configure Project.");
        }

        private static void EnsureAssetFolder(string path)
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

        private static UnityCamera FindCityCamera(Scene scene)
        {
            var cameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UnityCamera>(includeInactive: true))
                .ToArray();

            return cameras.FirstOrDefault(camera => camera.CompareTag("MainCamera")) ??
                   cameras.FirstOrDefault(camera => string.Equals(camera.name, "Main Camera", StringComparison.Ordinal));
        }

        private static void SetSerializedComponentName(SerializedObject serializedObject, string componentName)
        {
            var nameProperty = serializedObject.FindProperty("m_Name");
            if (nameProperty != null)
                nameProperty.stringValue = componentName;
        }

        private static Scene GetOrOpenScene(string scenePath, out bool ownsScene)
        {
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                ownsScene = false;
                return loadedScene;
            }

            ownsScene = true;
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }
    }
}
