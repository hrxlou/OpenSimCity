using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>Converts linear Blender roughness into URP metallic/smoothness maps.</summary>
    public static class UrbanRoughnessSetup
    {
        private const string Root = "Assets/OpenSimCity/Content/Art/Yeouido/Textures";
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static void Apply(Material material, string surface)
        {
            if (!Cache.TryGetValue(surface, out var map) || map == null)
            {
                var source = $"{Root}/{surface}/Roughness.png";
                if (!File.Exists(source)) return;
                var path = $"{Root}/{surface}/UrpMetallicSmoothness.png";
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(path))
                {
                    var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    try
                    {
                        image.LoadImage(File.ReadAllBytes(source));
                        var pixels = image.GetPixels32();
                        for (var index = 0; index < pixels.Length; index++)
                            pixels[index] = new Color32(0, 0, 0, (byte)(255 - pixels[index].r));
                        image.SetPixels32(pixels);
                        image.Apply();
                        File.WriteAllBytes(path, image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(image); }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
                map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Cache[surface] = map;
            }
            material.SetTexture("_MetallicGlossMap", map);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
        }
    }
}
