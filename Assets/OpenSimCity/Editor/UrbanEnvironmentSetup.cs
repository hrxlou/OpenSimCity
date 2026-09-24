using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Editor
{
    /// <summary>Reproducible, prefiltered outdoor reflections for the city scene.</summary>
    public static class UrbanEnvironmentSetup
    {
        public const string ReflectionPath =
            "Assets/ThirdParty/PolyHaven/green_point_park/green_point_park_1k.hdr";
        public const string ProfilePath = "Assets/OpenSimCity/Settings/UrbanDaylightProfile.asset";

        public static void ConfigureColorPipeline(Camera camera, Scene scene)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);
            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(4f);
            color.saturation.Override(-8f);
            var white = Component<WhiteBalance>(profile);
            white.temperature.Override(4f);
            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.10f);
            bloom.scatter.Override(0.55f);
            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.45f);
            Volume volume = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Urban Color Pipeline") volume = root.GetComponent<Volume>();
            if (volume == null)
            {
                var root = new GameObject("Urban Color Pipeline");
                SceneManager.MoveGameObjectToScene(root, scene);
                volume = root.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(profile);
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(component);
            return component;
        }

        public static void ConfigureReflections()
        {
            var importer = AssetImporter.GetAtPath(ReflectionPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Outdoor HDR reflection source is missing.");
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
            if (reflection == null) throw new InvalidOperationException("HDR import did not produce a cubemap.");
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = 0.65f;
            RenderSettings.reflectionBounces = 1;
        }

        [MenuItem("OpenSimCity/Setup/Refresh Urban Presentation")]
        public static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UiTypographySetup.Configure();
            SeoulAssetImportSetup.Configure();
            IsometricCameraRigSetup.ConfigureCameraRigMenu();
            UrbanRenderQualitySetup.Configure();
            UrbanParcelSurfaceSetup.Configure();
            GroundSurfaceSetup.Configure();
            ProductArtSetup.ConfigureProductCatalog();
            AssetDatabase.SaveAssets();
        }
    }
}
