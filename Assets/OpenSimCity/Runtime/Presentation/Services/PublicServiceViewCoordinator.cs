using System;
using System.Collections.Generic;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Services
{
    [DisallowMultipleComponent]
    public sealed class PublicServiceViewCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "PublicServiceViews";
        public const string UpdateProfilerMarkerName = "OpenSimCity.PublicServiceViews.Update";
        private static readonly ProfilerMarker Marker = new ProfilerMarker(UpdateProfilerMarkerName);

        private readonly SortedDictionary<StableId, GameObject> _active =
            new SortedDictionary<StableId, GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private CityRuntimeContext _context;
        private PublicServiceRuntimeModule _module;
        private int _readVersion = -1;
        private int _selectionVersion = -1;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _facilityMaterial;
        private Material _coverageMaterial;

        public int ActiveFacilityViewCount => _active.Count;
        public int PooledFacilityViewCount => _pool.Count;
        public int CoverageCellCount { get; private set; }
        public bool IsCoverageVisible => _meshRenderer != null && _meshRenderer.enabled;

        private void Update()
        {
            using (Marker.Auto())
                Synchronize();
        }

        private void Synchronize()
        {
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _module = context == null ? null : PublicServiceRuntimeRegistry.GetOrCreate(context.Runtime);
                _readVersion = -1;
                _selectionVersion = -1;
                ReleaseAll();
            }
            EnsureMesh();
            if (_context == null || !_context.HasWorldBounds || _module == null)
            {
                _meshRenderer.enabled = false;
                return;
            }

            _module.ReadModel.RefreshIfNeeded();
            var snapshot = _module.ReadModel.Snapshot;
            if (_readVersion != snapshot.Version)
            {
                SynchronizeFacilities(snapshot.Facilities, _context.WorldBounds);
                _readVersion = snapshot.Version;
            }

            var selection = PublicServiceSelection.Current;
            if (!selection.IsCoverageVisible)
            {
                _meshRenderer.enabled = false;
                CoverageCellCount = 0;
                _selectionVersion = selection.Version;
                return;
            }
            if (_selectionVersion != selection.Version || _readVersion == snapshot.Version)
            {
                RebuildCoverage(snapshot.Coverage, selection.ActiveKind, _context.WorldBounds);
                _selectionVersion = selection.Version;
            }
            _meshRenderer.enabled = true;
        }

        private void SynchronizeFacilities(
            IReadOnlyList<ServiceFacilityReadModel> facilities,
            WorldBoundsReadModel bounds)
        {
            var desired = new SortedSet<StableId>();
            for (var index = 0; index < facilities.Count; index++)
            {
                var facility = facilities[index];
                desired.Add(facility.FacilityId);
                if (!_active.TryGetValue(facility.FacilityId, out var view))
                {
                    view = Acquire();
                    _active.Add(facility.FacilityId, view);
                }
                Configure(view, facility, bounds);
            }
            var release = new List<StableId>();
            foreach (var pair in _active)
            {
                if (!desired.Contains(pair.Key)) release.Add(pair.Key);
            }
            for (var index = 0; index < release.Count; index++)
            {
                var view = _active[release[index]];
                _active.Remove(release[index]);
                view.SetActive(false);
                _pool.Push(view);
            }
        }

        private void Configure(GameObject view, ServiceFacilityReadModel facility, WorldBoundsReadModel bounds)
        {
            view.SetActive(true);
            view.name = $"Service-{facility.Kind}-{facility.FacilityId.Value}";
            view.transform.localPosition = new Vector3(
                bounds.MinX + (facility.Cell.X + 0.5f) * bounds.CellSize,
                bounds.CellSize * 0.35f,
                bounds.MinZ + (facility.Cell.Y + 0.5f) * bounds.CellSize);
            var scale = facility.Kind == ServiceKindView.School ? 0.62f : 0.48f;
            view.transform.localScale = new Vector3(
                bounds.CellSize * scale,
                bounds.CellSize * (0.45f + (int)facility.Kind * 0.06f),
                bounds.CellSize * scale);
            view.transform.localRotation = facility.Kind == ServiceKindView.Fire
                ? Quaternion.Euler(0f, 45f, 0f)
                : Quaternion.identity;
            var renderer = view.GetComponent<Renderer>();
            renderer.sharedMaterial = _facilityMaterial;
            renderer.material.color = ColorFor(facility.Kind);
        }

        private GameObject Acquire()
        {
            if (_pool.Count > 0) return _pool.Pop();
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.hideFlags = HideFlags.DontSave;
            view.transform.SetParent(transform, false);
            var collider = view.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return view;
        }

        private void ReleaseAll()
        {
            foreach (var pair in _active)
            {
                pair.Value.SetActive(false);
                _pool.Push(pair.Value);
            }
            _active.Clear();
        }

        private void EnsureMesh()
        {
            if (_meshFilter != null) return;
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = "PublicServiceCoverageMesh",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
                bounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
            _facilityMaterial = CreateMaterial("PublicServiceFacilityMaterial", Color.white);
            _coverageMaterial = CreateMaterial("PublicServiceCoverageMaterial", Color.white);
            _meshRenderer.sharedMaterial = _coverageMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;
        }

        private void RebuildCoverage(
            IReadOnlyList<ServiceCoverageReadModel> source,
            ServiceKindView kind,
            WorldBoundsReadModel bounds)
        {
            var rows = new List<ServiceCoverageReadModel>();
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index].Kind == kind) rows.Add(source[index]);
            }
            var vertices = new Vector3[rows.Count * 4];
            var colors = new Color32[rows.Count * 4];
            var normals = new Vector3[rows.Count * 4];
            var triangles = new int[rows.Count * 6];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var inset = row.FailureReason == ServiceFailureReasonView.None
                    ? bounds.CellSize * 0.08f
                    : bounds.CellSize * 0.20f;
                var minX = bounds.MinX + row.Cell.X * bounds.CellSize + inset;
                var maxX = bounds.MinX + (row.Cell.X + 1) * bounds.CellSize - inset;
                var minZ = bounds.MinZ + row.Cell.Y * bounds.CellSize + inset;
                var maxZ = bounds.MinZ + (row.Cell.Y + 1) * bounds.CellSize - inset;
                var y = bounds.CellSize * 0.035f;
                var vertex = index * 4;
                vertices[vertex] = new Vector3(minX, y, minZ);
                vertices[vertex + 1] = new Vector3(maxX, y, minZ);
                vertices[vertex + 2] = new Vector3(maxX, y, maxZ);
                vertices[vertex + 3] = new Vector3(minX, y, maxZ);
                var color = (Color32)CoverageColor(row.CoveragePermille, row.FailureReason);
                colors[vertex] = color;
                colors[vertex + 1] = color;
                colors[vertex + 2] = color;
                colors[vertex + 3] = color;
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
            CoverageCellCount = rows.Count;
        }

        private static Color ColorFor(ServiceKindView kind)
        {
            switch (kind)
            {
                case ServiceKindView.Police: return new Color(0.20f, 0.42f, 0.95f, 1f);
                case ServiceKindView.Fire: return new Color(0.92f, 0.20f, 0.14f, 1f);
                case ServiceKindView.School: return new Color(0.95f, 0.72f, 0.18f, 1f);
                case ServiceKindView.Healthcare: return new Color(0.20f, 0.80f, 0.62f, 1f);
                default: return new Color(0.48f, 0.62f, 0.34f, 1f);
            }
        }

        private static Color CoverageColor(int coveragePermille, ServiceFailureReasonView reason)
        {
            if (reason == ServiceFailureReasonView.BudgetTooLow)
                return new Color(0.55f, 0.32f, 0.78f, 0.70f);
            if (coveragePermille >= 900) return new Color(0.20f, 0.80f, 0.36f, 0.46f);
            if (coveragePermille > 0) return new Color(0.95f, 0.70f, 0.16f, 0.58f);
            return new Color(0.90f, 0.18f, 0.14f, 0.68f);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No service runtime shader is available.");
            return new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
        }

        private void OnDestroy()
        {
            foreach (var pair in _active) Destroy(pair.Value);
            while (_pool.Count > 0) Destroy(_pool.Pop());
            if (_mesh != null) Destroy(_mesh);
            if (_facilityMaterial != null) Destroy(_facilityMaterial);
            if (_coverageMaterial != null) Destroy(_coverageMaterial);
        }
    }

    public static class PublicServicePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PublicServiceViewCoordinator>() != null) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            var root = new GameObject(PublicServiceViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<PublicServiceViewCoordinator>();
        }
    }
}
