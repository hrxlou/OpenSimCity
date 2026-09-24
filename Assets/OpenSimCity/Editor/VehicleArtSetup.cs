using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenSimCity.Editor
{
    /// <summary>
    /// Generates compact first-party traffic prefabs. Each vehicle uses one
    /// behaviour-free renderer with material submeshes so the shared central pool
    /// keeps presentation cost predictable.
    /// </summary>
    public static class VehicleArtSetup
    {
        public const string RootFolder = "Assets/OpenSimCity/Content/Art/Vehicles";
        public const string MaterialFolder = RootFolder + "/Materials";
        public const string MeshFolder = RootFolder + "/Meshes";
        public const string ResourceFolder = "Assets/OpenSimCity/Resources/OpenSimCity/Vehicles";
        public const string TrafficResourcePath = "OpenSimCity/Vehicles/TrafficCar";
        public const string BusResourcePath = "OpenSimCity/Vehicles/TransitBus";
        public const string TrafficPrefabPath = ResourceFolder + "/TrafficCar.prefab";
        public const string BusPrefabPath = ResourceFolder + "/TransitBus.prefab";

        private static readonly string[] MaterialKeys = { "traffic", "transit", "glass", "tire", "light" };

        [MenuItem("OpenSimCity/Setup/Configure Vehicle Art")]
        public static void Configure()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(ResourceFolder);
            var materials = CreateMaterials();

            CreateVehicle(
                "TrafficCar",
                TrafficPrefabPath,
                new[] { materials["traffic"], materials["glass"], materials["tire"], materials["light"] },
                BuildTrafficCarGroups());
            CreateVehicle(
                "TransitBus",
                BusPrefabPath,
                new[] { materials["transit"], materials["glass"], materials["tire"], materials["light"] },
                BuildTransitBusGroups());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OpenSimCity behaviour-free vehicle art configured.");
        }

        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            ValidatePrefab(TrafficPrefabPath, TrafficResourcePath, issues);
            ValidatePrefab(BusPrefabPath, BusResourcePath, issues);
            for (var index = 0; index < MaterialKeys.Length; index++)
            {
                var path = $"{MaterialFolder}/MAT_Vehicle_{MaterialKeys[index]}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                    issues.Add($"Vehicle material is missing at '{path}'.");
            }
            return issues.AsReadOnly();
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["traffic"] = EnsureMaterial("traffic", new Color(0.64f, 0.20f, 0.16f), 0.34f),
                ["transit"] = EnsureMaterial("transit", new Color(0.10f, 0.43f, 0.47f), 0.30f),
                ["glass"] = EnsureMaterial("glass", new Color(0.075f, 0.17f, 0.20f), 0.76f),
                ["tire"] = EnsureMaterial("tire", new Color(0.055f, 0.070f, 0.075f), 0.08f),
                ["light"] = EnsureMaterial("light", new Color(0.96f, 0.76f, 0.25f), 0.42f)
            };
        }

        private static Material EnsureMaterial(string key, Color color, float smoothness)
        {
            var path = $"{MaterialFolder}/MAT_Vehicle_{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported shader is available for vehicle art.");
            if (material == null)
            {
                material = new Material(shader) { name = $"MAT_Vehicle_{key}" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateVehicle(
            string name,
            string prefabPath,
            Material[] materials,
            IReadOnlyList<BoxPart>[] groups)
        {
            var meshPath = $"{MeshFolder}/{name}.asset";
            var generated = BuildMesh(name, groups);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = generated;
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else
            {
                EditorUtility.CopySerialized(generated, mesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }

            var root = new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Mesh BuildMesh(string name, IReadOnlyList<BoxPart>[] groups)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            try
            {
                var groupMeshes = new Mesh[groups.Length];
                for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    var parts = groups[groupIndex];
                    var combine = new CombineInstance[parts.Count];
                    for (var partIndex = 0; partIndex < parts.Count; partIndex++)
                    {
                        var part = parts[partIndex];
                        combine[partIndex] = new CombineInstance
                        {
                            mesh = cubeMesh,
                            transform = Matrix4x4.TRS(part.Position, Quaternion.Euler(part.Euler), part.Scale)
                        };
                    }
                    groupMeshes[groupIndex] = new Mesh { name = $"{name}_{groupIndex}" };
                    groupMeshes[groupIndex].CombineMeshes(combine, true, true, false);
                }

                var finalCombine = new CombineInstance[groupMeshes.Length];
                for (var index = 0; index < groupMeshes.Length; index++)
                {
                    finalCombine[index] = new CombineInstance
                    {
                        mesh = groupMeshes[index],
                        transform = Matrix4x4.identity
                    };
                }
                var result = new Mesh { name = name };
                result.CombineMeshes(finalCombine, false, false, false);
                result.RecalculateBounds();
                MeshUtility.Optimize(result);
                for (var index = 0; index < groupMeshes.Length; index++)
                    UnityEngine.Object.DestroyImmediate(groupMeshes[index]);
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        private static IReadOnlyList<BoxPart>[] BuildTrafficCarGroups()
        {
            return new IReadOnlyList<BoxPart>[]
            {
                new[]
                {
                    Box(0f, 0.33f, 0f, 1.08f, 0.34f, 1.70f),
                    Box(0f, 0.55f, 0.48f, 0.98f, 0.18f, 0.52f),
                    Box(0f, 0.55f, -0.58f, 0.98f, 0.18f, 0.34f),
                    Box(0f, 0.88f, -0.08f, 0.84f, 0.08f, 0.76f)
                },
                new[] { Box(0f, 0.69f, -0.08f, 0.78f, 0.34f, 0.70f) },
                new[]
                {
                    Box(-0.59f, 0.20f, 0.53f, 0.16f, 0.28f, 0.31f),
                    Box(0.59f, 0.20f, 0.53f, 0.16f, 0.28f, 0.31f),
                    Box(-0.59f, 0.20f, -0.53f, 0.16f, 0.28f, 0.31f),
                    Box(0.59f, 0.20f, -0.53f, 0.16f, 0.28f, 0.31f),
                    Box(0f, 0.27f, -0.89f, 0.72f, 0.10f, 0.06f)
                },
                new[]
                {
                    Box(-0.35f, 0.43f, 0.875f, 0.22f, 0.12f, 0.04f),
                    Box(0.35f, 0.43f, 0.875f, 0.22f, 0.12f, 0.04f)
                }
            };
        }

        private static IReadOnlyList<BoxPart>[] BuildTransitBusGroups()
        {
            return new IReadOnlyList<BoxPart>[]
            {
                new[]
                {
                    Box(0f, 0.38f, 0f, 1.12f, 0.54f, 2.02f),
                    Box(0f, 0.94f, 0f, 1.06f, 0.10f, 1.96f),
                    Box(0f, 0.69f, -0.99f, 1.08f, 0.22f, 0.05f)
                },
                new[]
                {
                    Box(0f, 0.73f, 0.08f, 1.02f, 0.34f, 1.72f),
                    Box(0f, 0.70f, 1.025f, 0.92f, 0.30f, 0.04f)
                },
                new[]
                {
                    Box(-0.61f, 0.20f, 0.66f, 0.16f, 0.32f, 0.34f),
                    Box(0.61f, 0.20f, 0.66f, 0.16f, 0.32f, 0.34f),
                    Box(-0.61f, 0.20f, -0.66f, 0.16f, 0.32f, 0.34f),
                    Box(0.61f, 0.20f, -0.66f, 0.16f, 0.32f, 0.34f)
                },
                new[]
                {
                    Box(-0.34f, 0.43f, 1.035f, 0.22f, 0.14f, 0.04f),
                    Box(0.34f, 0.43f, 1.035f, 0.22f, 0.14f, 0.04f)
                }
            };
        }

        private static BoxPart Box(float x, float y, float z, float sx, float sy, float sz)
        {
            return new BoxPart(new Vector3(x, y, z), new Vector3(sx, sy, sz), Vector3.zero);
        }

        private static void ValidatePrefab(string assetPath, string resourcePath, ICollection<string> issues)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                issues.Add($"Vehicle prefab is missing at '{assetPath}'.");
                return;
            }
            if (Resources.Load<GameObject>(resourcePath) != prefab)
                issues.Add($"Vehicle prefab '{assetPath}' is not loadable as '{resourcePath}'.");
            if (prefab.transform.localPosition != Vector3.zero ||
                prefab.transform.localRotation != Quaternion.identity ||
                prefab.transform.localScale != Vector3.one)
                issues.Add($"Vehicle prefab '{prefab.name}' root must be normalized.");
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                issues.Add($"Vehicle prefab '{prefab.name}' must remain behaviour-free.");
            if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                issues.Add($"Vehicle prefab '{prefab.name}' must not contain colliders.");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 1)
                issues.Add($"Vehicle prefab '{prefab.name}' must use exactly one renderer, found {renderers.Length}.");
            else if (renderers[0].sharedMaterials.Length < 3)
                issues.Add($"Vehicle prefab '{prefab.name}' must preserve body, glazing and detail materials.");
            var filter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.subMeshCount < 3)
                issues.Add($"Vehicle prefab '{prefab.name}' must use a multi-submesh authored mesh.");
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private readonly struct BoxPart
        {
            public BoxPart(Vector3 position, Vector3 scale, Vector3 euler)
            {
                Position = position;
                Scale = scale;
                Euler = euler;
            }

            public Vector3 Position { get; }
            public Vector3 Scale { get; }
            public Vector3 Euler { get; }
        }
    }
}
