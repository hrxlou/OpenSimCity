using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Generates the small, first-party tool icon family used by the runtime UI.
    /// Keeping the source as drawing commands makes every PNG reproducible and
    /// prevents platform fonts from changing the visual language.
    /// </summary>
    public static class UiIconSetup
    {
        public const string IconFolder = "Assets/OpenSimCity/Content/UI/Icons";
        private const int CanvasSize = 96;
        private const int Stroke = 7;

        private static readonly IconSeed[] Seeds =
        {
            new IconSeed("tool-road", DrawRoad),
            new IconSeed("tool-road-upgrade", DrawRoadUpgrade),
            new IconSeed("tool-residential", DrawResidential),
            new IconSeed("tool-commercial", DrawCommercial),
            new IconSeed("tool-industrial", DrawIndustrial),
            new IconSeed("tool-zone-clear", DrawZoneClear),
            new IconSeed("tool-demolish", DrawDemolish),
            new IconSeed("tool-power", DrawPower),
            new IconSeed("tool-power-remove", DrawPowerRemove),
            new IconSeed("tool-management", DrawManagement)
        };

        [MenuItem("OpenSimCity/Setup/Configure UI Icons")]
        public static void Configure()
        {
            EnsureFolder(IconFolder);
            for (var index = 0; index < Seeds.Length; index++)
                WriteIcon(Seeds[index]);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (var index = 0; index < Seeds.Length; index++)
                ConfigureImporter(IconPath(Seeds[index].Name));
            AssetDatabase.SaveAssets();
            Debug.Log($"OpenSimCity UI icon family configured with {Seeds.Length} icons.");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            for (var index = 0; index < Seeds.Length; index++)
            {
                var path = IconPath(Seeds[index].Name);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    issues.Add($"UI tool icon is missing at '{path}'.");
                    continue;
                }
                if (texture.width != CanvasSize || texture.height != CanvasSize)
                    issues.Add($"UI tool icon '{path}' must be {CanvasSize}x{CanvasSize}px.");
            }
            return issues;
        }

        private static void WriteIcon(IconSeed seed)
        {
            var texture = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false, false)
            {
                name = seed.Name
            };
            try
            {
                var transparent = new Color32(255, 255, 255, 0);
                var pixels = new Color32[CanvasSize * CanvasSize];
                for (var index = 0; index < pixels.Length; index++)
                    pixels[index] = transparent;
                texture.SetPixels32(pixels);

                seed.Draw(new IconCanvas(texture));
                NormalizeOpticalBounds(texture);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(IconPath(seed.Name), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void NormalizeOpticalBounds(Texture2D texture)
        {
            var source = texture.GetPixels32();
            var minX = CanvasSize;
            var minY = CanvasSize;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < CanvasSize; y++)
            for (var x = 0; x < CanvasSize; x++)
            {
                if (source[y * CanvasSize + x].a == 0) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY) return;

            var targetCentre = (CanvasSize - 1) * 0.5f;
            var offsetX = Mathf.RoundToInt(targetCentre - (minX + maxX) * 0.5f);
            var offsetY = Mathf.RoundToInt(targetCentre - (minY + maxY) * 0.5f);
            if (offsetX == 0 && offsetY == 0) return;

            var transparent = new Color32(255, 255, 255, 0);
            var shifted = new Color32[source.Length];
            for (var index = 0; index < shifted.Length; index++) shifted[index] = transparent;
            for (var y = 0; y < CanvasSize; y++)
            for (var x = 0; x < CanvasSize; x++)
            {
                var targetX = x + offsetX;
                var targetY = y + offsetY;
                if ((uint)targetX >= CanvasSize || (uint)targetY >= CanvasSize) continue;
                shifted[targetY * CanvasSize + targetX] = source[y * CanvasSize + x];
            }
            texture.SetPixels32(shifted);
        }

        private static void ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
        }

        private static void DrawRoad(IconCanvas canvas)
        {
            canvas.Line(29, 12, 29, 84, Stroke);
            canvas.Line(67, 12, 67, 84, Stroke);
            canvas.Line(48, 14, 48, 29, 5);
            canvas.Line(48, 41, 48, 55, 5);
            canvas.Line(48, 67, 48, 82, 5);
        }

        private static void DrawRoadUpgrade(IconCanvas canvas)
        {
            canvas.Line(24, 16, 24, 48, 6);
            canvas.Line(72, 16, 72, 48, 6);
            canvas.Line(48, 18, 48, 70, Stroke);
            canvas.Line(48, 70, 30, 52, Stroke);
            canvas.Line(48, 70, 66, 52, Stroke);
            canvas.Line(30, 82, 66, 82, 6);
        }

        private static void DrawResidential(IconCanvas canvas)
        {
            canvas.Line(15, 45, 48, 17, Stroke);
            canvas.Line(48, 17, 81, 45, Stroke);
            canvas.Line(22, 41, 22, 81, Stroke);
            canvas.Line(74, 41, 74, 81, Stroke);
            canvas.Line(22, 81, 74, 81, Stroke);
            canvas.Rect(41, 56, 55, 81, 6);
        }

        private static void DrawCommercial(IconCanvas canvas)
        {
            canvas.Rect(20, 16, 76, 82, Stroke);
            canvas.Line(20, 42, 76, 42, Stroke);
            canvas.Line(22, 55, 74, 55, 5);
            canvas.Line(33, 43, 33, 55, 5);
            canvas.Line(48, 43, 48, 55, 5);
            canvas.Line(63, 43, 63, 55, 5);
            canvas.Rect(40, 62, 56, 82, 5);
        }

        private static void DrawIndustrial(IconCanvas canvas)
        {
            canvas.Line(16, 82, 80, 82, Stroke);
            canvas.Line(16, 82, 16, 49, Stroke);
            canvas.Line(16, 49, 35, 35, Stroke);
            canvas.Line(35, 35, 35, 50, Stroke);
            canvas.Line(35, 50, 55, 35, Stroke);
            canvas.Line(55, 35, 55, 82, Stroke);
            canvas.Rect(66, 17, 78, 82, 6);
            canvas.Rect(27, 62, 39, 73, 4);
        }

        private static void DrawZoneClear(IconCanvas canvas)
        {
            canvas.Line(22, 66, 56, 22, 13);
            canvas.Line(56, 22, 78, 39, 13);
            canvas.Line(78, 39, 44, 82, 13);
            canvas.Line(20, 67, 43, 84, 6);
            canvas.Line(43, 84, 77, 84, 6);
        }

        private static void DrawDemolish(IconCanvas canvas)
        {
            canvas.Line(26, 75, 64, 31, 10);
            canvas.Line(17, 84, 31, 70, 12);
            canvas.Line(51, 18, 78, 40, 12);
            canvas.Line(58, 13, 84, 34, 5);
        }

        private static void DrawPower(IconCanvas canvas)
        {
            canvas.Line(59, 13, 36, 48, 13);
            canvas.Line(36, 48, 58, 48, 13);
            canvas.Line(58, 48, 37, 83, 13);
        }

        private static void DrawPowerRemove(IconCanvas canvas)
        {
            DrawPower(canvas);
            canvas.Line(17, 18, 80, 81, 12, erase: true);
            canvas.Line(17, 18, 80, 81, 6);
        }

        private static void DrawManagement(IconCanvas canvas)
        {
            canvas.Line(15, 31, 48, 14, Stroke);
            canvas.Line(48, 14, 81, 31, Stroke);
            canvas.Line(18, 34, 78, 34, Stroke);
            canvas.Line(22, 79, 74, 79, Stroke);
            canvas.Line(29, 38, 29, 75, 7);
            canvas.Line(48, 38, 48, 75, 7);
            canvas.Line(67, 38, 67, 75, 7);
        }

        private static string IconPath(string name) => $"{IconFolder}/{name}.png";

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private readonly struct IconSeed
        {
            public IconSeed(string name, Action<IconCanvas> draw)
            {
                Name = name;
                Draw = draw;
            }

            public string Name { get; }
            public Action<IconCanvas> Draw { get; }
        }

        private sealed class IconCanvas
        {
            private static readonly Color32 Ink = new Color32(255, 255, 255, 255);
            private static readonly Color32 Clear = new Color32(255, 255, 255, 0);
            private readonly Texture2D _texture;

            public IconCanvas(Texture2D texture) => _texture = texture;

            public void Rect(int minX, int minY, int maxX, int maxY, int width)
            {
                Line(minX, minY, maxX, minY, width);
                Line(maxX, minY, maxX, maxY, width);
                Line(maxX, maxY, minX, maxY, width);
                Line(minX, maxY, minX, minY, width);
            }

            public void Line(int x0, int y0, int x1, int y1, int width, bool erase = false)
            {
                var dx = x1 - x0;
                var dy = y1 - y0;
                var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                if (steps == 0)
                {
                    Disc(x0, y0, width * 0.5f, erase ? Clear : Ink);
                    return;
                }
                for (var step = 0; step <= steps; step++)
                {
                    var t = step / (float)steps;
                    Disc(
                        Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                        Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)),
                        width * 0.5f,
                        erase ? Clear : Ink);
                }
            }

            private void Disc(int centerX, int centerY, float radius, Color32 color)
            {
                var extent = Mathf.CeilToInt(radius);
                var radiusSquared = radius * radius;
                for (var y = centerY - extent; y <= centerY + extent; y++)
                {
                    if (y < 0 || y >= CanvasSize)
                        continue;
                    for (var x = centerX - extent; x <= centerX + extent; x++)
                    {
                        if (x < 0 || x >= CanvasSize)
                            continue;
                        var offsetX = x - centerX;
                        var offsetY = y - centerY;
                        if ((offsetX * offsetX) + (offsetY * offsetY) <= radiusSquared)
                            // Texture2D pixel coordinates start at the bottom-left while
                            // icon drawing coordinates intentionally read top-to-bottom.
                            _texture.SetPixel(x, CanvasSize - 1 - y, color);
                    }
                }
            }
        }
    }
}
