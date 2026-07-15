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
        public const string SelectionMaterialPath = "Assets/OpenSimCity/Content/Materials/OpenSimCityDebugGrid.mat";

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

            var scene = GetOrOpenScene(CityPrototypeScenePath, out var ownsScene);
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
                SetSerializedComponentName(serializedRig, RigName);
                serializedRig.ApplyModifiedPropertiesWithoutUndo();

                var serializedBounds = new SerializedObject(boundsSource);
                SetSerializedComponentName(serializedBounds, "WorldBoundsReadModelSource");
                serializedBounds.ApplyModifiedPropertiesWithoutUndo();

                camera.orthographic = true;
                camera.nearClipPlane = Mathf.Max(0.05f, camera.nearClipPlane);
                camera.farClipPlane = Mathf.Max(200f, camera.farClipPlane);
                EditorUtility.SetDirty(rig);
                EditorUtility.SetDirty(boundsSource);
                EditorUtility.SetDirty(camera);

                ConfigureGridSelection(root, rig, boundsSource, inputActions, ref sceneChanged);

                if (sceneChanged || scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ConfigureGridSelection(
            GameObject root,
            IsometricCameraRig rig,
            WorldBoundsReadModelSource boundsSource,
            InputActionAsset inputActions,
            ref bool sceneChanged)
        {
            var material = EnsureSelectionMaterial();
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
            debugGrid.Configure(boundsSource, material);

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
            highlight.Configure(material);

            var tool = selectionRoot.GetComponent<GridSelectionTool>();
            if (tool == null)
            {
                tool = selectionRoot.AddComponent<GridSelectionTool>();
                sceneChanged = true;
            }
            tool.ConfigureReferences(rig, highlight, inputActions);

            ApplySelectionSerializedReferences(debugGrid, highlight, tool, boundsSource, rig, material, inputActions);
            EditorUtility.SetDirty(debugGrid);
            EditorUtility.SetDirty(highlight);
            EditorUtility.SetDirty(tool);
        }

        private static Material EnsureSelectionMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SelectionMaterialPath);
            if (material != null)
                return material;

            var folder = Path.GetDirectoryName(SelectionMaterialPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
                EnsureAssetFolder(folder);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("Could not find an unlit shader for the P1 debug grid material.");

            material = new Material(shader) { name = "OpenSimCity Debug Grid" };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.15f, 0.9f, 1f, 0.9f));
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(0.15f, 0.9f, 1f, 0.9f));
            AssetDatabase.CreateAsset(material, SelectionMaterialPath);
            return material;
        }

        private static void ApplySelectionSerializedReferences(
            GridDebugGridRenderer debugGrid,
            GridSelectionHighlightView highlight,
            GridSelectionTool tool,
            WorldBoundsReadModelSource boundsSource,
            IsometricCameraRig rig,
            Material material,
            InputActionAsset inputActions)
        {
            var serializedGrid = new SerializedObject(debugGrid);
            serializedGrid.FindProperty("worldBoundsSource").objectReferenceValue = boundsSource;
            serializedGrid.FindProperty("lineMaterial").objectReferenceValue = material;
            SetSerializedComponentName(serializedGrid, DebugGridName);
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();

            var serializedHighlight = new SerializedObject(highlight);
            serializedHighlight.FindProperty("lineMaterial").objectReferenceValue = material;
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
