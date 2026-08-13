using System;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Spatial;
using OpenSimCity.Application.World;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Spatial
{
    /// <summary>
    /// One dynamic mesh for the active spatial datamap. Band-dependent inset and
    /// height encode intensity in addition to vertex color.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialFieldOverlayCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "SpatialFieldOverlay";
        public const string RebuildProfilerMarkerName = "OpenSimCity.SpatialFieldOverlay.Rebuild";
        private static readonly ProfilerMarker RebuildProfilerMarker =
            new ProfilerMarker(RebuildProfilerMarkerName);

        [SerializeField] private Material material;
        [SerializeField, Range(0.05f, 1f)] private float opacity = 0.55f;
        [SerializeField, Min(0f)] private float baseHeight = 0.035f;

        private CityRuntimeContext _context;
        private SpatialFieldRuntimeModule _module;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _ownedMaterial;
        private int _observedSelectionVersion = -1;
        private int _observedReadVersion = -1;

        public int RenderedCellCount { get; private set; }
        public int MeshVersion { get; private set; }
        public SpatialFieldViewKind ActiveKind { get; private set; }
        public bool IsVisible => _meshRenderer != null && _meshRenderer.enabled;

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current;
                _module = current == null ? null : SpatialFieldRuntimeRegistry.GetOrCreate(current.Runtime);
                _observedReadVersion = -1;
                _observedSelectionVersion = -1;
            }

            EnsureRenderer();
            var selection = SpatialFieldOverlaySelection.Current;
            if (_context == null || !_context.HasWorldBounds || _module == null || !selection.IsVisible)
            {
                _meshRenderer.enabled = false;
                RenderedCellCount = 0;
                return;
            }

            _module.RefreshIfNeeded();
            var readVersion = _module.ReadModel.Snapshot.Version;
            if (_observedSelectionVersion != selection.Version ||
                _observedReadVersion != readVersion)
            {
                using (RebuildProfilerMarker.Auto())
                    Rebuild(selection.Kind);
                _observedSelectionVersion = selection.Version;
                _observedReadVersion = readVersion;
            }
            _meshRenderer.enabled = true;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
            if (_ownedMaterial != null)
                Destroy(_ownedMaterial);
        }

        private void EnsureRenderer()
        {
            if (_meshFilter != null)
                return;
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = "SpatialFieldOverlayMesh",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
                bounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
            _meshRenderer.sharedMaterial = ResolveMaterial();
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;
        }

        private void Rebuild(SpatialFieldViewKind kind)
        {
            var overlay = _module.Overlay.Build(kind);
            var bounds = _context.WorldBounds;
            var count = overlay.Cells.Count;
            var vertices = new Vector3[count * 4];
            var colors = new Color32[count * 4];
            var triangles = new int[count * 6];
            var normals = new Vector3[count * 4];

            for (var index = 0; index < count; index++)
            {
                var cell = overlay.Cells[index];
                var bandIndex = (int)cell.Band;
                var insetFraction = (4 - bandIndex) * 0.035f;
                var inset = bounds.CellSize * insetFraction;
                var minX = bounds.MinX + cell.Cell.X * bounds.CellSize + inset;
                var maxX = bounds.MinX + (cell.Cell.X + 1) * bounds.CellSize - inset;
                var minZ = bounds.MinZ + cell.Cell.Y * bounds.CellSize + inset;
                var maxZ = bounds.MinZ + (cell.Cell.Y + 1) * bounds.CellSize - inset;
                var y = baseHeight + bandIndex * bounds.CellSize * 0.004f;
                var vertex = index * 4;
                vertices[vertex] = new Vector3(minX, y, minZ);
                vertices[vertex + 1] = new Vector3(maxX, y, minZ);
                vertices[vertex + 2] = new Vector3(maxX, y, maxZ);
                vertices[vertex + 3] = new Vector3(minX, y, maxZ);
                var color = ColorFor(cell.Value, cell.IsAdverse);
                var color32 = (Color32)color;
                colors[vertex] = color32;
                colors[vertex + 1] = color32;
                colors[vertex + 2] = color32;
                colors[vertex + 3] = color32;
                normals[vertex] = Vector3.up;
                normals[vertex + 1] = Vector3.up;
                normals[vertex + 2] = Vector3.up;
                normals[vertex + 3] = Vector3.up;
                var triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            _mesh.Clear(false);
            _mesh.vertices = vertices;
            _mesh.colors32 = colors;
            _mesh.normals = normals;
            _mesh.triangles = triangles;
            _mesh.UploadMeshData(false);
            RenderedCellCount = count;
            ActiveKind = kind;
            MeshVersion = checked(MeshVersion + 1);
        }

        private Color ColorFor(int value, bool adverse)
        {
            var t = Mathf.Clamp01(value / 1000f);
            Color low;
            Color high;
            if (adverse)
            {
                low = new Color(0.15f, 0.65f, 0.35f, opacity * 0.65f);
                high = new Color(0.92f, 0.18f, 0.12f, opacity);
            }
            else
            {
                low = new Color(0.20f, 0.30f, 0.72f, opacity * 0.65f);
                high = new Color(0.25f, 0.82f, 0.38f, opacity);
            }
            return Color.Lerp(low, high, t);
        }

        private Material ResolveMaterial()
        {
            if (material != null)
                return material;
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No shader is available for the spatial-field overlay.");
            _ownedMaterial = new Material(shader)
            {
                name = "SpatialFieldOverlayRuntimeMaterial",
                hideFlags = HideFlags.DontSave
            };
            return _ownedMaterial;
        }
    }

    public static class SpatialFieldPresentationBootstrap
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            if (_subscribed) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitial() => Install(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
                Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            if (UnityEngine.Object.FindFirstObjectByType<SpatialFieldOverlayCoordinator>() != null)
                return;
            var root = new GameObject(SpatialFieldOverlayCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<SpatialFieldOverlayCoordinator>();
        }
    }
}
