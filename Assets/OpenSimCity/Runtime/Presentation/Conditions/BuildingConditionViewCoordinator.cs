using System;
using System.Collections.Generic;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Conditions
{
    /// <summary>
    /// Central pooled condition markers plus one dynamic condition datamap mesh.
    /// Marker objects contain no scripts; all pulse and lifecycle work is here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingConditionViewCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "BuildingConditionViews";
        public const string UpdateProfilerMarkerName = "OpenSimCity.BuildingConditionViews.Update";
        private static readonly ProfilerMarker UpdateProfilerMarker =
            new ProfilerMarker(UpdateProfilerMarkerName);

        private readonly SortedDictionary<StableId, GameObject> _activeMarkers =
            new SortedDictionary<StableId, GameObject>();
        private readonly Stack<GameObject> _pooledMarkers = new Stack<GameObject>();

        private CityRuntimeContext _context;
        private BuildingConditionRuntimeModule _module;
        private int _observedReadVersion = -1;
        private int _observedOverlayVersion = -1;
        private int _observedDatamapSelectionVersion = -1;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _stressedMaterial;
        private Material _decliningMaterial;
        private Material _abandonedMaterial;
        private Material _redevelopingMaterial;
        private Material _datamapMaterial;

        public int ActiveMarkerCount => _activeMarkers.Count;
        public int PooledMarkerCount => _pooledMarkers.Count;
        public int DatamapCellCount { get; private set; }
        public bool IsDatamapVisible => _meshRenderer != null && _meshRenderer.enabled;
        public int DatamapMeshVersion { get; private set; }

        private void Update()
        {
            using (UpdateProfilerMarker.Auto())
                UpdateViews();
        }

        private void UpdateViews()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current;
                _module = current == null
                    ? null
                    : BuildingConditionRuntimeRegistry.GetOrCreate(current.Runtime);
                _observedReadVersion = -1;
                _observedOverlayVersion = -1;
                _observedDatamapSelectionVersion = -1;
                ReleaseAllMarkers();
            }

            EnsureResources();
            if (_context == null || !_context.HasWorldBounds || _module == null)
            {
                _meshRenderer.enabled = false;
                return;
            }

            _module.RefreshReadModels();
            var read = _module.ReadModel.Snapshot;
            if (_observedReadVersion != read.Version)
            {
                SynchronizeMarkers(read.Buildings, _context.WorldBounds);
                _observedReadVersion = read.Version;
            }
            UpdateRedevelopmentPulse(_context.WorldBounds);

            var selection = BuildingConditionDatamapSelection.Current;
            if (!selection.IsVisible)
            {
                _meshRenderer.enabled = false;
                DatamapCellCount = 0;
                _observedDatamapSelectionVersion = selection.Version;
                return;
            }

            _module.Overlay.RefreshIfNeeded();
            if (_observedOverlayVersion != _module.Overlay.Version ||
                _observedDatamapSelectionVersion != selection.Version)
            {
                RebuildDatamap(_module.Overlay.Cells, _context.WorldBounds);
                _observedOverlayVersion = _module.Overlay.Version;
                _observedDatamapSelectionVersion = selection.Version;
            }
            _meshRenderer.enabled = true;
        }

        private void OnDestroy()
        {
            foreach (var pair in _activeMarkers)
                Destroy(pair.Value);
            _activeMarkers.Clear();
            while (_pooledMarkers.Count > 0)
                Destroy(_pooledMarkers.Pop());
            if (_mesh != null) Destroy(_mesh);
            DestroyMaterial(_stressedMaterial);
            DestroyMaterial(_decliningMaterial);
            DestroyMaterial(_abandonedMaterial);
            DestroyMaterial(_redevelopingMaterial);
            DestroyMaterial(_datamapMaterial);
        }

        private void EnsureResources()
        {
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.GetComponent<MeshFilter>();
                if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
                _meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
                _mesh = new Mesh
                {
                    name = "BuildingConditionDatamapMesh",
                    indexFormat = IndexFormat.UInt32,
                    hideFlags = HideFlags.DontSave,
                    bounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
                };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
                _datamapMaterial = CreateMaterial(
                    "BuildingConditionDatamapMaterial",
                    Color.white);
                _meshRenderer.sharedMaterial = _datamapMaterial;
                _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _meshRenderer.receiveShadows = false;
                _meshRenderer.enabled = false;
            }

            if (_stressedMaterial == null)
            {
                _stressedMaterial = CreateMaterial(
                    "ConditionStressed", new Color(0.95f, 0.75f, 0.15f, 1f));
                _decliningMaterial = CreateMaterial(
                    "ConditionDeclining", new Color(0.95f, 0.38f, 0.12f, 1f));
                _abandonedMaterial = CreateMaterial(
                    "ConditionAbandoned", new Color(0.25f, 0.25f, 0.28f, 1f));
                _redevelopingMaterial = CreateMaterial(
                    "ConditionRedeveloping", new Color(0.25f, 0.65f, 0.95f, 1f));
            }
        }

        private void SynchronizeMarkers(
            IReadOnlyList<BuildingConditionBuildingReadModel> buildings,
            WorldBoundsReadModel bounds)
        {
            var desired = new SortedSet<StableId>();
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (building.Stage == BuildingConditionStageView.Healthy)
                    continue;
                desired.Add(building.BuildingId);
                if (!_activeMarkers.TryGetValue(building.BuildingId, out var marker))
                {
                    marker = AcquireMarker();
                    _activeMarkers.Add(building.BuildingId, marker);
                }
                ConfigureMarker(marker, building, bounds);
            }

            var release = new List<StableId>();
            foreach (var pair in _activeMarkers)
            {
                if (!desired.Contains(pair.Key))
                    release.Add(pair.Key);
            }
            for (var index = 0; index < release.Count; index++)
            {
                var marker = _activeMarkers[release[index]];
                _activeMarkers.Remove(release[index]);
                marker.SetActive(false);
                _pooledMarkers.Push(marker);
            }
        }

        private void ConfigureMarker(
            GameObject marker,
            BuildingConditionBuildingReadModel building,
            WorldBoundsReadModel bounds)
        {
            marker.name = $"BuildingCondition-{building.Stage}-{building.BuildingId.Value}";
            marker.SetActive(true);
            var width = Math.Max(1, building.FootprintWidth) * bounds.CellSize;
            var height = Math.Max(1, building.FootprintHeight) * bounds.CellSize;
            marker.transform.localPosition = new Vector3(
                bounds.MinX + (building.Anchor.X + building.FootprintWidth * 0.5f) * bounds.CellSize,
                bounds.CellSize * 0.55f,
                bounds.MinZ + (building.Anchor.Y + building.FootprintHeight * 0.5f) * bounds.CellSize);

            var renderer = marker.GetComponent<Renderer>();
            switch (building.Stage)
            {
                case BuildingConditionStageView.Stressed:
                    marker.transform.localRotation = Quaternion.identity;
                    marker.transform.localScale = new Vector3(
                        width * 0.32f, bounds.CellSize * 0.08f, height * 0.32f);
                    renderer.sharedMaterial = _stressedMaterial;
                    break;
                case BuildingConditionStageView.Declining:
                    marker.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    marker.transform.localScale = new Vector3(
                        width * 0.42f, bounds.CellSize * 0.14f, height * 0.16f);
                    renderer.sharedMaterial = _decliningMaterial;
                    break;
                case BuildingConditionStageView.Abandoned:
                    marker.transform.localRotation = Quaternion.Euler(18f, 45f, 0f);
                    marker.transform.localScale = new Vector3(
                        width * 0.62f, bounds.CellSize * 0.10f, height * 0.18f);
                    renderer.sharedMaterial = _abandonedMaterial;
                    break;
                case BuildingConditionStageView.Redeveloping:
                    marker.transform.localRotation = Quaternion.identity;
                    marker.transform.localScale = new Vector3(
                        width * 0.24f, bounds.CellSize * 0.48f, height * 0.24f);
                    renderer.sharedMaterial = _redevelopingMaterial;
                    break;
            }
        }

        private void UpdateRedevelopmentPulse(WorldBoundsReadModel bounds)
        {
            var pulse = 0.9f + Mathf.Sin(Time.unscaledTime * 4f) * 0.1f;
            var absoluteHeight = bounds.CellSize * 0.48f * pulse;
            foreach (var pair in _activeMarkers)
            {
                if (!pair.Value.name.StartsWith(
                        "BuildingCondition-Redeveloping-",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                var scale = pair.Value.transform.localScale;
                pair.Value.transform.localScale = new Vector3(
                    scale.x,
                    absoluteHeight,
                    scale.z);
            }
        }

        private GameObject AcquireMarker()
        {
            if (_pooledMarkers.Count > 0)
                return _pooledMarkers.Pop();
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "BuildingConditionMarker";
            marker.hideFlags = HideFlags.DontSave;
            marker.transform.SetParent(transform, false);
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return marker;
        }

        private void ReleaseAllMarkers()
        {
            foreach (var pair in _activeMarkers)
            {
                pair.Value.SetActive(false);
                _pooledMarkers.Push(pair.Value);
            }
            _activeMarkers.Clear();
        }

        private void RebuildDatamap(
            IReadOnlyList<BuildingConditionOverlayCell> cells,
            WorldBoundsReadModel bounds)
        {
            var count = cells.Count;
            var vertices = new Vector3[count * 4];
            var colors = new Color32[count * 4];
            var normals = new Vector3[count * 4];
            var triangles = new int[count * 6];
            for (var index = 0; index < count; index++)
            {
                var cell = cells[index];
                var stageIndex = (int)cell.Stage;
                var inset = bounds.CellSize * Math.Min(0.18f, stageIndex * 0.03f);
                var minX = bounds.MinX + cell.Cell.X * bounds.CellSize + inset;
                var maxX = bounds.MinX + (cell.Cell.X + 1) * bounds.CellSize - inset;
                var minZ = bounds.MinZ + cell.Cell.Y * bounds.CellSize + inset;
                var maxZ = bounds.MinZ + (cell.Cell.Y + 1) * bounds.CellSize - inset;
                var y = bounds.CellSize * (0.02f + stageIndex * 0.004f);
                var vertex = index * 4;
                vertices[vertex] = new Vector3(minX, y, minZ);
                vertices[vertex + 1] = new Vector3(maxX, y, minZ);
                vertices[vertex + 2] = new Vector3(maxX, y, maxZ);
                vertices[vertex + 3] = new Vector3(minX, y, maxZ);
                var color = (Color32)DatamapColor(cell.Stage);
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
            DatamapCellCount = count;
            DatamapMeshVersion = checked(DatamapMeshVersion + 1);
        }

        private static Color DatamapColor(BuildingConditionStageView stage)
        {
            switch (stage)
            {
                case BuildingConditionStageView.Healthy:
                    return new Color(0.20f, 0.72f, 0.34f, 0.38f);
                case BuildingConditionStageView.Stressed:
                    return new Color(0.95f, 0.76f, 0.18f, 0.52f);
                case BuildingConditionStageView.Declining:
                    return new Color(0.95f, 0.38f, 0.12f, 0.62f);
                case BuildingConditionStageView.Abandoned:
                    return new Color(0.18f, 0.18f, 0.22f, 0.72f);
                default:
                    return new Color(0.22f, 0.64f, 0.94f, 0.62f);
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No runtime shader is available for condition views.");
            return new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
                Destroy(material);
        }
    }

    public static class BuildingConditionPresentationBootstrap
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
            if (UnityEngine.Object.FindFirstObjectByType<BuildingConditionViewCoordinator>() != null)
                return;
            var root = new GameObject(BuildingConditionViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<BuildingConditionViewCoordinator>();
        }
    }
}
