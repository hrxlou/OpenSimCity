using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace OpenSimCity.Presentation
{
    public readonly struct RepresentativeArtMetricsSnapshot
    {
        public RepresentativeArtMetricsSnapshot(
            int rendererCount,
            int materialSlotCount,
            int uniqueMaterialCount,
            int triangleCount,
            int uniqueMeshCount,
            int uniqueTextureCount,
            long textureMemoryBytes)
        {
            RendererCount = rendererCount;
            MaterialSlotCount = materialSlotCount;
            UniqueMaterialCount = uniqueMaterialCount;
            TriangleCount = triangleCount;
            UniqueMeshCount = uniqueMeshCount;
            UniqueTextureCount = uniqueTextureCount;
            TextureMemoryBytes = textureMemoryBytes;
        }

        public int RendererCount { get; }
        public int MaterialSlotCount { get; }
        public int UniqueMaterialCount { get; }
        public int TriangleCount { get; }
        public int UniqueMeshCount { get; }
        public int UniqueTextureCount { get; }
        public long TextureMemoryBytes { get; }

        public override string ToString()
        {
            return
                $"renderers={RendererCount}, materialSlots={MaterialSlotCount}, " +
                $"uniqueMaterials={UniqueMaterialCount}, triangles={TriangleCount}, " +
                $"uniqueMeshes={UniqueMeshCount}, textures={UniqueTextureCount}, " +
                $"textureMemoryBytes={TextureMemoryBytes}";
        }
    }

    public static class RepresentativeArtMetrics
    {
        public static RepresentativeArtMetricsSnapshot Capture(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            var materials = new HashSet<Material>();
            var meshes = new HashSet<Mesh>();
            var textures = new HashSet<Texture>();
            var materialSlots = 0;
            var triangles = 0;

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var sharedMaterials = renderers[rendererIndex].sharedMaterials;
                materialSlots += sharedMaterials.Length;
                for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    var material = sharedMaterials[materialIndex];
                    if (material == null || !materials.Add(material))
                        continue;
                    var texturePropertyNames = material.GetTexturePropertyNames();
                    for (var propertyIndex = 0;
                         propertyIndex < texturePropertyNames.Length;
                         propertyIndex++)
                    {
                        var texture = material.GetTexture(texturePropertyNames[propertyIndex]);
                        if (texture != null)
                            textures.Add(texture);
                    }
                }
            }

            for (var filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
            {
                var mesh = meshFilters[filterIndex].sharedMesh;
                if (mesh == null)
                    continue;
                meshes.Add(mesh);
                for (var subMeshIndex = 0;
                     subMeshIndex < mesh.subMeshCount;
                     subMeshIndex++)
                {
                    triangles += checked((int)(mesh.GetIndexCount(subMeshIndex) / 3));
                }
            }

            long textureMemory = 0;
            foreach (var texture in textures)
                textureMemory += Profiler.GetRuntimeMemorySizeLong(texture);

            return new RepresentativeArtMetricsSnapshot(
                renderers.Length,
                materialSlots,
                materials.Count,
                triangles,
                meshes.Count,
                textures.Count,
                textureMemory);
        }
    }
}
