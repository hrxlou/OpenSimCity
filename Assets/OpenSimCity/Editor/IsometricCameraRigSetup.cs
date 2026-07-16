using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSimCity.Gameplay.Camera;
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
            else if (!IsometricCameraInputActions.HasRequiredActions(inputActions))
            {
                IsometricCameraInputActions.ConfigureAsset(inputActions);
                EditorUtility.SetDirty(inputActions);
            }

            return inputActions;
        }

        private static void WriteInputActionsJsonAsset()
        {
            if (File.Exists(InputActionsAssetPath))
                AssetDatabase.DeleteAsset(InputActionsAssetPath);

            var generatedAsset = IsometricCameraInputActions.CreateDefaultAsset();
            try
            {
                File.WriteAllText(InputActionsAssetPath, generatedAsset.ToJson());
            }
            finally
            {
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

                if (sceneChanged || scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
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
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
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
