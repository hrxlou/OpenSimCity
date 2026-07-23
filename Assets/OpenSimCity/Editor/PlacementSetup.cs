using System;
using System.Collections.Generic;
using System.Linq;
using OpenSimCity.Gameplay.Camera;
using OpenSimCity.Gameplay.Construction;
using OpenSimCity.Presentation.Overlays;
using OpenSimCity.Presentation;
using OpenSimCity.Presentation.Buildings;
using OpenSimCity.Presentation.Roads;
using OpenSimCity.Presentation.Utilities;
using OpenSimCity.UI.Foundation;
using OpenSimCity.UI.HUD;
using OpenSimCity.UI.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Reproducibly wires the P2-05 placement fixture into CityPrototype: the
    /// CityRuntime host and construction input driver, the world-space overlay
    /// renderer, and the UI binder that connects the four shells. Scene changes go
    /// through Unity APIs rather than hand-edited YAML.
    /// </summary>
    public static class PlacementSetup
    {
        public const string RuntimeRootName = "CityRuntime";
        public const string OverlayRendererName = "PlacementOverlay";
        public const string PowerRendererName = "PowerProducerViews";
        public const string BuildingRendererName = "BuildingViews";
        public const string RoadRendererName = "RoadViews";

        [MenuItem("OpenSimCity/Setup/Configure Placement")]
        public static void ConfigurePlacementMenu()
        {
            ConfigureCityPrototype();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity placement fixture configured.");
        }

        public static void ConfigureCityPrototype()
        {
            ConfigureCityPrototype(AssetDatabase.LoadAssetAtPath<PlaceholderArtCatalog>(
                PlaceholderArtSetup.CatalogPath));
        }

        public static void ConfigureCityPrototype(PlaceholderArtCatalog placeholderCatalog)
        {
            var scene = GetOrOpenScene(IsometricCameraRigSetup.CityPrototypeScenePath, out var ownsScene);
            try
            {
                var root = RequireRoot(scene);
                var sceneChanged = false;

                var rigObject = root.transform.Find(IsometricCameraRigSetup.RigName)?.gameObject;
                var rig = rigObject?.GetComponent<IsometricCameraRig>();
                var boundsSource = rigObject?.GetComponent<WorldBoundsReadModelSource>();
                if (rig == null || boundsSource == null)
                {
                    throw new InvalidOperationException(
                        "CityPrototype has no camera rig or world-bounds source. Run 'Configure Project' first.");
                }

                var runtimeObject = EnsureChild(root.transform, RuntimeRootName, ref sceneChanged);
                var host = EnsureComponent<CityRuntimeHost>(runtimeObject, ref sceneChanged);
                host.ConfigureReferences(boundsSource, rig);
                ApplyReferences(host, ("worldBoundsSource", boundsSource), ("cameraRig", rig));

                var driver = EnsureComponent<ConstructionInputDriver>(runtimeObject, ref sceneChanged);
                driver.ConfigureReferences(rig);
                ApplyReferences(driver, ("cameraRig", rig));

                var overlayObject = EnsureChild(root.transform, OverlayRendererName, ref sceneChanged);
                EnsureComponent<PlacementOverlayRenderer>(overlayObject, ref sceneChanged);

                var powerObject = EnsureChild(root.transform, PowerRendererName, ref sceneChanged);
                var powerRenderer = EnsureComponent<PowerProducerViewRenderer>(powerObject, ref sceneChanged);
                powerRenderer.Configure(placeholderCatalog);
                ApplyReferences(powerRenderer, ("catalog", placeholderCatalog));

                var buildingObject = EnsureChild(root.transform, BuildingRendererName, ref sceneChanged);
                var buildingRenderer = EnsureComponent<BuildingViewRenderer>(buildingObject, ref sceneChanged);
                buildingRenderer.Configure(placeholderCatalog);
                ApplyReferences(buildingRenderer, ("catalog", placeholderCatalog));

                var roadObject = EnsureChild(root.transform, RoadRendererName, ref sceneChanged);
                var roadRenderer = EnsureComponent<RoadViewRenderer>(roadObject, ref sceneChanged);
                roadRenderer.Configure(placeholderCatalog);
                ApplyReferences(roadRenderer, ("catalog", placeholderCatalog));

                ConfigureUiBinder(root, ref sceneChanged);

                if (sceneChanged || scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void ConfigureUiBinder(GameObject root, ref bool sceneChanged)
        {
            var uiRoot = root.transform.Find(UiFoundationSetup.UiRootName);
            var documentObject = uiRoot?.Find(UiFoundationSetup.UiDocumentName)?.gameObject;
            var bootstrap = documentObject?.GetComponent<UiFoundationBootstrap>();
            if (bootstrap == null)
            {
                throw new InvalidOperationException(
                    "CityPrototype has no UI foundation. Run 'Configure UI Foundation' first.");
            }

            var binder = EnsureComponent<PlacementUiBinder>(documentObject, ref sceneChanged);
            binder.ConfigureReferences(bootstrap);
            ApplyReferences(binder, ("bootstrap", bootstrap));

            var municipalHud = EnsureComponent<MunicipalHudBinder>(documentObject, ref sceneChanged);
            municipalHud.ConfigureReferences(bootstrap);
            ApplyReferences(municipalHud, ("bootstrap", bootstrap));
        }

        public static void Validate(ICollection<string> issues)
        {
            if (!System.IO.File.Exists(IsometricCameraRigSetup.CityPrototypeScenePath))
                return;

            var scene = GetOrOpenScene(IsometricCameraRigSetup.CityPrototypeScenePath, out var ownsScene);
            try
            {
                var root = scene.GetRootGameObjects()
                    .FirstOrDefault(gameObject => string.Equals(
                        gameObject.name,
                        IsometricCameraRigSetup.RequiredRootName,
                        StringComparison.Ordinal));
                if (root == null)
                    return;

                var runtime = root.transform.Find(RuntimeRootName)?.gameObject;
                if (runtime == null || runtime.GetComponent<CityRuntimeHost>() == null)
                {
                    issues.Add(
                        $"CityPrototype is missing '{RuntimeRootName}' with a CityRuntimeHost. " +
                        "Run 'OpenSimCity/Setup/Configure Placement'.");
                }
                else if (runtime.GetComponent<ConstructionInputDriver>() == null)
                {
                    issues.Add("CityPrototype CityRuntime object has no ConstructionInputDriver.");
                }

                var overlay = root.transform.Find(OverlayRendererName)?.GetComponent<PlacementOverlayRenderer>();
                if (overlay == null)
                {
                    issues.Add(
                        $"CityPrototype is missing '{OverlayRendererName}' with a PlacementOverlayRenderer.");
                }

                var powerRenderer = root.transform.Find(PowerRendererName)?.GetComponent<PowerProducerViewRenderer>();
                if (powerRenderer == null)
                    issues.Add($"CityPrototype is missing '{PowerRendererName}' with a PowerProducerViewRenderer.");

                var buildingRenderer = root.transform.Find(BuildingRendererName)?.GetComponent<BuildingViewRenderer>();
                if (buildingRenderer == null)
                    issues.Add($"CityPrototype is missing '{BuildingRendererName}' with a BuildingViewRenderer.");

                var roadRenderer = root.transform.Find(RoadRendererName)?.GetComponent<RoadViewRenderer>();
                if (roadRenderer == null)
                    issues.Add($"CityPrototype is missing '{RoadRendererName}' with a RoadViewRenderer.");

                var uiRoot = root.transform.Find(UiFoundationSetup.UiRootName);
                var document = uiRoot?.Find(UiFoundationSetup.UiDocumentName)?.gameObject;
                if (document == null || document.GetComponent<PlacementUiBinder>() == null)
                    issues.Add("CityPrototype UIDocument has no PlacementUiBinder.");
                else if (document.GetComponent<MunicipalHudBinder>() == null)
                    issues.Add("CityPrototype UIDocument has no MunicipalHudBinder.");
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static GameObject RequireRoot(Scene scene)
        {
            var root = scene.GetRootGameObjects()
                .FirstOrDefault(gameObject => string.Equals(
                    gameObject.name,
                    IsometricCameraRigSetup.RequiredRootName,
                    StringComparison.Ordinal));
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"Scene '{IsometricCameraRigSetup.CityPrototypeScenePath}' is missing its required root " +
                    $"'{IsometricCameraRigSetup.RequiredRootName}'.");
            }

            return root;
        }

        private static TComponent EnsureComponent<TComponent>(GameObject target, ref bool sceneChanged)
            where TComponent : Component
        {
            var component = target.GetComponent<TComponent>();
            if (component == null)
            {
                component = target.AddComponent<TComponent>();
                sceneChanged = true;
            }

            EditorUtility.SetDirty(component);
            return component;
        }

        private static void ApplyReferences(
            Component component,
            params (string PropertyName, UnityEngine.Object Value)[] references)
        {
            var serialized = new SerializedObject(component);
            foreach (var reference in references)
            {
                var property = serialized.FindProperty(reference.PropertyName);
                if (property != null)
                    property.objectReferenceValue = reference.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
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
