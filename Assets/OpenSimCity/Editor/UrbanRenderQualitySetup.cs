using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenSimCity.Editor
{
    public static class UrbanRenderQualitySetup
    {
        [MenuItem("OpenSimCity/Setup/Configure Urban Render Quality")]
        public static void Configure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/OpenSimCity/Settings/OpenSimCityURP.asset");
            if (pipeline == null) throw new InvalidOperationException("City URP asset is missing.");
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_RequireDepthTexture").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 4096;
            pipeline.shadowCascadeCount = 4;
            // The orthographic rig sits 100 units from its target. A 100-unit
            // cutoff faded away the shadows exactly where the city was framed.
            pipeline.shadowDistance = 180f;
            pipeline.cascade4Split = new Vector3(0.35f, 0.52f, 0.72f);
            pipeline.shadowDepthBias = 0.12f;
            pipeline.shadowNormalBias = 0.08f;
            ConfigureAmbientOcclusion(pipeline);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"Urban render quality: MSAA={pipeline.msaaSampleCount}, " +
                $"softShadows={pipeline.supportsSoftShadows}, atlas={pipeline.mainLightShadowmapResolution}, " +
                $"cascades={pipeline.shadowCascadeCount}. GPU cost requires runtime profiling.");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/OpenSimCity/Settings/OpenSimCityURP.asset");
            if (pipeline == null || pipeline.rendererDataList.Length == 0 ||
                pipeline.rendererDataList[0] == null)
            {
                issues.Add("Urban render quality cannot find the configured URP renderer.");
                return issues.AsReadOnly();
            }

            var features = pipeline.rendererDataList[0].rendererFeatures;
            if (features.Count(value => value is ScreenSpaceAmbientOcclusion) != 1)
                issues.Add("The URP renderer must contain exactly one shared SSAO feature.");
            else if (!features.OfType<ScreenSpaceAmbientOcclusion>().Single().isActive)
                issues.Add("The shared SSAO feature is disabled.");
            return issues.AsReadOnly();
        }

        private static void ConfigureAmbientOcclusion(UniversalRenderPipelineAsset pipeline)
        {
            var renderer = pipeline.rendererDataList[0];
            var feature = renderer.rendererFeatures
                .OfType<ScreenSpaceAmbientOcclusion>()
                .FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "OpenSimCity Screen Space Ambient Occlusion";
                feature.hideFlags |= HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, renderer);

                var rendererObject = new SerializedObject(renderer);
                var features = rendererObject.FindProperty("m_RendererFeatures");
                var featureMap = rendererObject.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
                rendererObject.ApplyModifiedPropertiesWithoutUndo();
            }

            feature.SetActive(true);
            var settings = new SerializedObject(feature);
            settings.FindProperty("m_Settings.Intensity").floatValue = 0.85f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.30f;
            settings.FindProperty("m_Settings.Radius").floatValue = 0.08f;
            settings.FindProperty("m_Settings.Falloff").floatValue = 65f;
            settings.FindProperty("m_Settings.Downsample").boolValue = false;
            settings.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            settings.FindProperty("m_Settings.Source").enumValueIndex = 1;
            settings.FindProperty("m_Settings.NormalSamples").enumValueIndex = 1;
            settings.FindProperty("m_Settings.Samples").enumValueIndex = 1;
            settings.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }
    }
}
