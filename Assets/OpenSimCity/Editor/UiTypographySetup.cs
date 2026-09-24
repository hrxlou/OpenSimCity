using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace OpenSimCity.Editor
{
    public static class UiTypographySetup
    {
        public const string FontPath =
            "Assets/ThirdParty/Fonts/Pretendard/Pretendard-Regular.otf";
        public const string RegularAssetPath = "Assets/ThirdParty/Fonts/Pretendard/Pretendard-Regular-SDF.asset";
        public const string SemiboldAssetPath = "Assets/ThirdParty/Fonts/Pretendard/Pretendard-SemiBold-SDF.asset";
        public const string LicensePath = "Assets/ThirdParty/Fonts/Pretendard/LICENSE.txt";
        public const string ProvenancePath = "Assets/ThirdParty/Fonts/Pretendard/PROVENANCE.md";

        [MenuItem("OpenSimCity/Setup/Configure Korean Typography")]
        public static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var characters = new HashSet<char>(Enumerable.Range(32, 95).Select(value => (char)value));
            foreach (var file in Directory.GetFiles("Assets/OpenSimCity/Runtime", "*.cs", SearchOption.AllDirectories))
            foreach (var character in File.ReadAllText(file))
                if (character >= '\uAC00' && character <= '\uD7A3' || "✓!→←·×−₩…".Contains(character))
                    characters.Add(character);
            var glyphs = new string(characters.OrderBy(value => value).ToArray());
            var regular = EnsureFont(FontPath, RegularAssetPath, glyphs);
            var semibold = EnsureFont("Assets/ThirdParty/Fonts/Pretendard/Pretendard-SemiBold.otf", SemiboldAssetPath, glyphs);
            // Legacy binders that request FontStyle.Bold must also select a real face.
            regular.fontWeightTable[7].regularTypeface = semibold;
            EditorUtility.SetDirty(regular);
            const string textPath = "Assets/OpenSimCity/Settings/OpenSimCityTextSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(textPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(settings, textPath);
            }
            settings.defaultFontAsset = regular;
            var panel = UiFoundationSetup.EnsurePanelSettings();
            panel.textSettings = settings;
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static FontAsset EnsureFont(string sourcePath, string outputPath, string glyphs)
        {
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(outputPath);
            if (asset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                asset = FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
                asset.name = Path.GetFileNameWithoutExtension(outputPath);
                AssetDatabase.CreateAsset(asset, outputPath);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            asset.TryAddCharacters(glyphs, out _);
            var missing = new string(glyphs.Where(character => !asset.characterLookupTable.ContainsKey(character)).ToArray());
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.filterMode = FilterMode.Bilinear;
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
                EditorUtility.SetDirty(atlas);
            }
            EditorUtility.SetDirty(asset);
            Debug.Log($"Korean typography: {asset.name}, {asset.characterTable.Count} prewarmed glyphs, " +
                $"{asset.atlasTextures.Length} atlases; unsupported symbols: {missing}");
            return asset;
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<Font>(FontPath) == null)
                issues.Add($"Runtime Korean UI font is missing or invalid at '{FontPath}'.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(LicensePath) == null)
                issues.Add($"Runtime Korean UI font license is missing at '{LicensePath}'.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(ProvenancePath) == null)
                issues.Add($"Runtime Korean UI font provenance is missing at '{ProvenancePath}'.");
            foreach (var path in new[] { RegularAssetPath, SemiboldAssetPath })
            {
                var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (font == null || font.characterTable.Count < 300 || font.atlasTextures.Length == 0)
                    issues.Add($"Korean TextCore font is missing its prewarmed glyphs at '{path}'.");
            }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/OpenSimCity/Settings/OpenSimCityPanelSettings.asset");
            if (panel == null || panel.textSettings == null || panel.textSettings.defaultFontAsset !=
                AssetDatabase.LoadAssetAtPath<FontAsset>(RegularAssetPath))
                issues.Add("UI PanelSettings must use the shared Korean TextCore settings.");
            var regular = AssetDatabase.LoadAssetAtPath<FontAsset>(RegularAssetPath);
            if (regular != null && regular.fontWeightTable[7].regularTypeface !=
                AssetDatabase.LoadAssetAtPath<FontAsset>(SemiboldAssetPath))
                issues.Add("Bold UI text must resolve to the real Pretendard SemiBold face.");
            return issues;
        }
    }
}
