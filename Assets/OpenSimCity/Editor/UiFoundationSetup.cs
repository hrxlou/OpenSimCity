using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Gameplay.Selection;
using OpenSimCity.UI.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Reproducibly creates the shared PanelSettings and the CityPrototype
    /// UIDocument that hosts the P2-00 shells, plus the Gameplay-side relay that
    /// keeps UI pointer/focus out of cell selection. Scene changes go through
    /// Unity APIs rather than hand-edited YAML.
    /// </summary>
    public static class UiFoundationSetup
    {
        public const string PanelSettingsPath = "Assets/OpenSimCity/Settings/OpenSimCityPanelSettings.asset";
        public const string UiRootName = "UI";
        public const string UiDocumentName = "UIDocument";
        public const string BoundaryRelayName = "UiInputBoundaryRelay";

        /// <summary>
        /// Reference resolution for the panel scale. 1280x720 with
        /// <see cref="PanelScaleMode.ScaleWithScreenSize"/> is what makes 100/125/150%
        /// OS scaling and window resize land on the same type and spacing.
        /// </summary>
        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        [MenuItem("OpenSimCity/Setup/Configure UI Foundation")]
        public static void ConfigureUiFoundationMenu()
        {
            var panelSettings = EnsurePanelSettings();
            ConfigureCityPrototype(panelSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity UI foundation configured.");
        }

        public static PanelSettings EnsurePanelSettings()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var created = false;
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "OpenSimCity Panel Settings";
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
                created = true;
            }

            var changed = created;
            if (panelSettings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
            {
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                changed = true;
            }

            if (panelSettings.referenceResolution != ReferenceResolution)
            {
                panelSettings.referenceResolution = ReferenceResolution;
                changed = true;
            }

            // Match on width and height equally so ultrawide does not inflate type
            // and a short window does not crush it.
            if (!Mathf.Approximately(panelSettings.match, 0.5f))
            {
                panelSettings.match = 0.5f;
                changed = true;
            }

            if (panelSettings.screenMatchMode != PanelScreenMatchMode.MatchWidthOrHeight)
            {
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(panelSettings);
                AssetDatabase.ImportAsset(PanelSettingsPath);
            }

            return panelSettings;
        }

        public static void ConfigureCityPrototype(PanelSettings panelSettings)
        {
            if (panelSettings == null)
                throw new ArgumentNullException(nameof(panelSettings));

            var tokens = LoadStyleSheet(UiFoundationNames.TokensStyleSheetPath);
            var components = LoadStyleSheet(UiFoundationNames.ComponentsStyleSheetPath);

            var scene = GetOrOpenScene(IsometricCameraRigSetup.CityPrototypeScenePath, out var ownsScene);
            try
            {
                var root = scene.GetRootGameObjects()
                    .FirstOrDefault(gameObject => string.Equals(
                        gameObject.name, IsometricCameraRigSetup.RequiredRootName, StringComparison.Ordinal));
                if (root == null)
                {
                    throw new InvalidOperationException(
                        $"Scene '{IsometricCameraRigSetup.CityPrototypeScenePath}' is missing its required root " +
                        $"'{IsometricCameraRigSetup.RequiredRootName}'.");
                }

                var sceneChanged = false;
                var uiRoot = EnsureChild(root.transform, UiRootName, ref sceneChanged);
                var documentObject = EnsureChild(uiRoot.transform, UiDocumentName, ref sceneChanged);

                var document = documentObject.GetComponent<UIDocument>();
                if (document == null)
                {
                    document = documentObject.AddComponent<UIDocument>();
                    sceneChanged = true;
                }

                var bootstrap = documentObject.GetComponent<UiFoundationBootstrap>();
                if (bootstrap == null)
                {
                    bootstrap = documentObject.AddComponent<UiFoundationBootstrap>();
                    sceneChanged = true;
                }

                bootstrap.ConfigureStyleSheets(tokens, components);

                var serializedDocument = new SerializedObject(document);
                serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
                serializedDocument.ApplyModifiedPropertiesWithoutUndo();

                var serializedBootstrap = new SerializedObject(bootstrap);
                serializedBootstrap.FindProperty("tokensStyleSheet").objectReferenceValue = tokens;
                serializedBootstrap.FindProperty("componentsStyleSheet").objectReferenceValue = components;
                serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

                ConfigureBoundaryRelay(root, uiRoot, ref sceneChanged);

                EditorUtility.SetDirty(document);
                EditorUtility.SetDirty(bootstrap);

                if (sceneChanged || scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ConfigureBoundaryRelay(GameObject root, GameObject uiRoot, ref bool sceneChanged)
        {
            var selectionRoot = root.transform.Find(IsometricCameraRigSetup.SelectionRootName)?.gameObject;
            var selectionTool = selectionRoot?.GetComponent<GridSelectionTool>();
            if (selectionTool == null)
            {
                throw new InvalidOperationException(
                    "CityPrototype has no GridSelectionTool to bind the UI input boundary to. " +
                    "Run 'OpenSimCity/Setup/Configure Project' first.");
            }

            var relayObject = EnsureChild(uiRoot.transform, BoundaryRelayName, ref sceneChanged);
            var relay = relayObject.GetComponent<UiInputBoundaryRelay>();
            if (relay == null)
            {
                relay = relayObject.AddComponent<UiInputBoundaryRelay>();
                sceneChanged = true;
            }

            relay.ConfigureReferences(selectionTool);

            var serializedRelay = new SerializedObject(relay);
            serializedRelay.FindProperty("selectionTool").objectReferenceValue = selectionTool;
            serializedRelay.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(relay);
        }

        public static void Validate(ICollection<string> issues)
        {
            UiTokenValidator.Validate(issues);

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                issues.Add(
                    $"Shared PanelSettings is missing at '{PanelSettingsPath}'. " +
                    "Run 'OpenSimCity/Setup/Configure UI Foundation'.");
            }
            else if (panelSettings.scaleMode != PanelScaleMode.ScaleWithScreenSize ||
                     panelSettings.referenceResolution != ReferenceResolution)
            {
                issues.Add(
                    $"PanelSettings at '{PanelSettingsPath}' must scale with screen size at " +
                    $"{ReferenceResolution.x}x{ReferenceResolution.y} so UI scale smoke stays comparable.");
            }

            if (!System.IO.File.Exists(IsometricCameraRigSetup.CityPrototypeScenePath))
                return;

            var scene = GetOrOpenScene(IsometricCameraRigSetup.CityPrototypeScenePath, out var ownsScene);
            try
            {
                var root = scene.GetRootGameObjects()
                    .FirstOrDefault(gameObject => string.Equals(
                        gameObject.name, IsometricCameraRigSetup.RequiredRootName, StringComparison.Ordinal));
                var uiRoot = root?.transform.Find(UiRootName);
                var document = uiRoot?.Find(UiDocumentName)?.GetComponent<UIDocument>();
                var bootstrap = uiRoot?.Find(UiDocumentName)?.GetComponent<UiFoundationBootstrap>();
                var relay = uiRoot?.Find(BoundaryRelayName)?.GetComponent<UiInputBoundaryRelay>();

                if (document == null)
                {
                    issues.Add(
                        $"CityPrototype is missing '{UiRootName}/{UiDocumentName}' with a UIDocument. " +
                        "Run 'OpenSimCity/Setup/Configure UI Foundation'.");
                    return;
                }

                if (panelSettings != null && document.panelSettings != panelSettings)
                    issues.Add("CityPrototype UIDocument is not wired to the committed PanelSettings asset.");

                if (bootstrap == null)
                    issues.Add($"CityPrototype '{UiDocumentName}' has no UiFoundationBootstrap component.");
                else if (bootstrap.ConfiguredTokensStyleSheet == null ||
                         bootstrap.ConfiguredComponentsStyleSheet == null)
                    issues.Add("CityPrototype UiFoundationBootstrap is missing a token or component stylesheet.");

                if (relay == null)
                {
                    issues.Add(
                        $"CityPrototype is missing '{UiRootName}/{BoundaryRelayName}'. Without it, UI pointer and " +
                        "focus would leak into cell selection.");
                }
                else if (relay.ConfiguredSelectionTool == null)
                {
                    issues.Add("CityPrototype UiInputBoundaryRelay is not wired to the GridSelectionTool.");
                }
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static StyleSheet LoadStyleSheet(string path)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (styleSheet == null)
                throw new InvalidOperationException($"Could not load UI stylesheet '{path}'.");

            return styleSheet;
        }

        private static GameObject EnsureChild(Transform parent, string childName, ref bool sceneChanged)
        {
            var existing = parent.Find(childName);
            if (existing != null)
                return existing.gameObject;

            var child = new GameObject(childName);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, worldPositionStays: false);
            sceneChanged = true;
            return child;
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
