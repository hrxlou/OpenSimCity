using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Blender Generated coordinates do not survive FBX. Supply metre-based box
    /// projection only for source meshes that have no authored UV channel.
    /// </summary>
    public sealed class SeoulSurfaceUvPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (assetPath.StartsWith(SeoulAssetImportSetup.ModelFolder + "/", System.StringComparison.Ordinal))
                ((ModelImporter)assetImporter).isReadable = true;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(SeoulAssetImportSetup.ModelFolder + "/",
                    System.StringComparison.Ordinal)) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.uv.Length != 0) continue;
                Project(mesh);
            }
        }

        private static void Project(Mesh mesh)
        {
            var originalVertices = mesh.vertices;
            var originalNormals = mesh.normals;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var submeshes = new List<int[]>();
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var source = mesh.GetTriangles(submesh);
                var indices = new int[source.Length];
                for (var triangle = 0; triangle < source.Length; triangle += 3)
                {
                    var face = Vector3.Cross(originalVertices[source[triangle + 1]] - originalVertices[source[triangle]],
                        originalVertices[source[triangle + 2]] - originalVertices[source[triangle]]).normalized;
                    var axis = new Vector3(Mathf.Abs(face.x), Mathf.Abs(face.y), Mathf.Abs(face.z));
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var oldIndex = source[triangle + corner];
                        var position = originalVertices[oldIndex];
                        indices[triangle + corner] = vertices.Count;
                        vertices.Add(position);
                        normals.Add(originalNormals.Length == originalVertices.Length ? originalNormals[oldIndex] : face);
                        uv.Add((axis.y >= axis.x && axis.y >= axis.z ? new Vector2(position.x, position.z) :
                            axis.x >= axis.z ? new Vector2(position.z, position.y) : new Vector2(position.x, position.y)) * 0.25f);
                    }
                }
                submeshes.Add(indices);
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = submeshes.Count;
            for (var index = 0; index < submeshes.Count; index++) mesh.SetTriangles(submeshes[index], index);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
