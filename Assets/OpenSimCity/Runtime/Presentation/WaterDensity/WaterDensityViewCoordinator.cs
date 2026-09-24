using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.WaterDensity;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.WaterDensity
{
    [DisallowMultipleComponent]
    public sealed class WaterDensityViewCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "WaterDensityViews";
        public const string UpdateProfilerMarkerName = "OpenSimCity.WaterDensityViews.Update";
        private static readonly ProfilerMarker Marker = new ProfilerMarker(UpdateProfilerMarkerName);

        private readonly SortedDictionary<StableId, GameObject> _pumpViews =
            new SortedDictionary<StableId, GameObject>();
        private readonly Stack<GameObject> _pumpPool = new Stack<GameObject>();
        private readonly SortedDictionary<StableId, DensityVisual> _densityViews =
            new SortedDictionary<StableId, DensityVisual>();
        private readonly Dictionary<DensityVisualKind, Stack<GameObject>> _densityPools =
            new Dictionary<DensityVisualKind, Stack<GameObject>>();
        private CityRuntimeContext _context;
        private WaterDensityRuntimeModule _module;
        private int _readVersion = -1;
        private int _overlayReadVersion = -1;
        private int _selectionVersion = -1;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _pumpMaterial;
        private Material _densityMaterial;
        private Material _overlayMaterial;
        private MaterialPropertyBlock _properties;
        private GameObject _densityMediumOfficePrefab;
        private GameObject _densityMediumApartmentPrefab;
        private GameObject _densityHighOfficePrefab;
        private GameObject _densityHighApartmentPrefab;

        public int ActivePumpViewCount => _pumpViews.Count;
        public int ActiveDensityViewCount => _densityViews.Count;
        public int PooledViewCount
        {
            get
            {
                var count = _pumpPool.Count;
                foreach (var pair in _densityPools) count += pair.Value.Count;
                return count;
            }
        }
        public int OverlayCellCount { get; private set; }
        public bool IsOverlayVisible => _meshRenderer != null && _meshRenderer.enabled;

        private void Update()
        {
            using (Marker.Auto()) Synchronize();
        }

        private void Synchronize()
        {
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _module = context == null ? null : WaterDensityRuntimeRegistry.GetOrCreate(context.Runtime);
                _readVersion = -1;
                _overlayReadVersion = -1;
                _selectionVersion = -1;
                ReleaseAll();
            }
            EnsureResources();
            if (_context == null || !_context.HasWorldBounds || _module == null)
            {
                _meshRenderer.enabled = false;
                return;
            }
            _module.ReadModel.RefreshIfNeeded();
            var snapshot = _module.ReadModel.Snapshot;
            if (_readVersion != snapshot.Version)
            {
                SynchronizePumps(snapshot.Producers, _context.WorldBounds);
                SynchronizeDensity(snapshot.Buildings, _context.WorldBounds);
                _readVersion = snapshot.Version;
            }

            var selection = WaterDensitySelection.Current;
            if (!selection.IsOverlayVisible)
            {
                _meshRenderer.enabled = false;
                OverlayCellCount = 0;
                _selectionVersion = selection.Version;
                return;
            }
            if (_selectionVersion != selection.Version || _overlayReadVersion != snapshot.Version)
            {
                RebuildOverlay(snapshot, selection.OverlayKind, _context.WorldBounds);
                _selectionVersion = selection.Version;
                _overlayReadVersion = snapshot.Version;
            }
            _meshRenderer.enabled = true;
        }

        private void SynchronizePumps(
            IReadOnlyList<WaterProducerReadModel> producers,
            WorldBoundsReadModel bounds)
        {
            var desired = new SortedSet<StableId>();
            for (var index = 0; index < producers.Count; index++)
            {
                var producer = producers[index];
                desired.Add(producer.ProducerId);
                if (!_pumpViews.TryGetValue(producer.ProducerId, out var view))
                {
                    view = Acquire(_pumpPool, PrimitiveType.Cylinder, "WaterPumpView");
                    _pumpViews.Add(producer.ProducerId, view);
                }
                view.SetActive(true);
                view.name = $"WaterPump-{producer.ProducerId.Value}";
                view.transform.localPosition = new Vector3(
                    bounds.MinX + (producer.Cell.X + 0.5f) * bounds.CellSize,
                    bounds.CellSize * 0.28f,
                    bounds.MinZ + (producer.Cell.Y + 0.5f) * bounds.CellSize);
                view.transform.localScale = new Vector3(
                    bounds.CellSize * 0.42f,
                    bounds.CellSize * 0.24f,
                    bounds.CellSize * 0.42f);
                var renderer = view.GetComponent<Renderer>();
                renderer.sharedMaterial = _pumpMaterial;
                _properties.Clear();
                var utilization = producer.Capacity <= 0 ? 0 : producer.Used * 1000 / producer.Capacity;
                var color = utilization > 900
                    ? new Color(0.18f, 0.48f, 0.92f, 1f)
                    : new Color(0.20f, 0.75f, 0.95f, 1f);
                _properties.SetColor("_Color", color);
                _properties.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_properties);
            }
            ReleaseMissing(_pumpViews, _pumpPool, desired);
        }

        private void SynchronizeDensity(
            IReadOnlyList<DensityBuildingReadModel> buildings,
            WorldBoundsReadModel bounds)
        {
            var desired = new SortedSet<StableId>();
            for (var index = 0; index < buildings.Count; index++)
            {
                var building = buildings[index];
                if (building.Stage == DensityStageView.Low && building.UpgradeMonthsRemaining <= 0) continue;
                desired.Add(building.BuildingId);
                var kind = ResolveDensityVisualKind(building);
                if (!_densityViews.TryGetValue(building.BuildingId, out var visual) || visual.Kind != kind)
                {
                    if (visual.GameObject != null)
                        RecycleDensity(visual);
                    visual = new DensityVisual(kind, AcquireDensity(kind));
                    _densityViews[building.BuildingId] = visual;
                }
                var view = visual.GameObject;
                view.SetActive(true);
                view.name = $"Density-{building.Stage}-{building.BuildingId.Value}";
                var centre = new Vector3(
                    bounds.MinX + (building.Cell.X + 0.5f) * bounds.CellSize,
                    0f,
                    bounds.MinZ + (building.Cell.Y + 0.5f) * bounds.CellSize);
                if (kind == DensityVisualKind.UpgradeMarker)
                {
                    view.transform.localPosition = centre + Vector3.up * bounds.CellSize * 0.72f;
                    view.transform.localScale = Vector3.one * bounds.CellSize * 0.26f;
                    view.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    var renderer = view.GetComponent<Renderer>();
                    renderer.sharedMaterial = _densityMaterial;
                    _properties.Clear();
                    var color = new Color(0.95f, 0.68f, 0.18f, 1f);
                    _properties.SetColor("_Color", color);
                    _properties.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(_properties);
                }
                else
                {
                    view.transform.localPosition = centre;
                    view.transform.localScale = Vector3.one * bounds.CellSize;
                    view.transform.localRotation = Quaternion.Euler(
                        0f, (building.BuildingId.Value & 1UL) == 0UL ? 0f : 90f, 0f);
                }
            }
            ReleaseMissingDensity(desired);
        }

        private static DensityVisualKind ResolveDensityVisualKind(DensityBuildingReadModel building)
        {
            if (building.UpgradeMonthsRemaining > 0) return DensityVisualKind.UpgradeMarker;
            var housing = building.EffectiveHousingCapacity > 0;
            if (building.Stage == DensityStageView.High)
                return housing ? DensityVisualKind.HighApartment : DensityVisualKind.HighOffice;
            return housing ? DensityVisualKind.MediumApartment : DensityVisualKind.MediumOffice;
        }

        private void RebuildOverlay(
            WaterDensityReadModelSnapshot snapshot,
            WaterDensityOverlayKind kind,
            WorldBoundsReadModel bounds)
        {
            var count = snapshot.Buildings.Count +
                        (kind == WaterDensityOverlayKind.Water ? snapshot.Producers.Count : 0);
            var vertices = new Vector3[count * 4];
            var colors = new Color32[count * 4];
            var normals = new Vector3[count * 4];
            var triangles = new int[count * 6];
            var row = 0;
            for (var index = 0; index < snapshot.Buildings.Count; index++)
            {
                var building = snapshot.Buildings[index];
                var color = kind == WaterDensityOverlayKind.Water
                    ? WaterColor(building.WaterSupplyPermille, building.WaterReason)
                    : DensityColor(building.Stage, building.UpgradeMonthsRemaining > 0);
                WriteQuad(row++, building.Cell.X, building.Cell.Y, color, bounds,
                    vertices, colors, normals, triangles);
            }
            if (kind == WaterDensityOverlayKind.Water)
            {
                for (var index = 0; index < snapshot.Producers.Count; index++)
                {
                    var producer = snapshot.Producers[index];
                    WriteQuad(row++, producer.Cell.X, producer.Cell.Y,
                        new Color(0.18f, 0.72f, 0.95f, 0.78f), bounds,
                        vertices, colors, normals, triangles);
                }
            }
            _mesh.Clear(false);
            _mesh.vertices = vertices;
            _mesh.colors32 = colors;
            _mesh.normals = normals;
            _mesh.triangles = triangles;
            _mesh.UploadMeshData(false);
            OverlayCellCount = count;
        }

        private static void WriteQuad(
            int index,
            int x,
            int yCell,
            Color color,
            WorldBoundsReadModel bounds,
            Vector3[] vertices,
            Color32[] colors,
            Vector3[] normals,
            int[] triangles)
        {
            var inset = bounds.CellSize * 0.12f;
            var minX = bounds.MinX + x * bounds.CellSize + inset;
            var maxX = bounds.MinX + (x + 1) * bounds.CellSize - inset;
            var minZ = bounds.MinZ + yCell * bounds.CellSize + inset;
            var maxZ = bounds.MinZ + (yCell + 1) * bounds.CellSize - inset;
            var height = bounds.CellSize * 0.045f;
            var vertex = index * 4;
            vertices[vertex] = new Vector3(minX, height, minZ);
            vertices[vertex + 1] = new Vector3(maxX, height, minZ);
            vertices[vertex + 2] = new Vector3(maxX, height, maxZ);
            vertices[vertex + 3] = new Vector3(minX, height, maxZ);
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

        private static Color WaterColor(int permille, WaterSupplyReasonView reason)
        {
            if (reason == WaterSupplyReasonView.NoRoadAccess ||
                reason == WaterSupplyReasonView.DifferentRoadComponent)
                return new Color(0.82f, 0.20f, 0.18f, 0.72f);
            if (permille >= 1000) return new Color(0.18f, 0.72f, 0.95f, 0.52f);
            if (permille > 0) return new Color(0.90f, 0.66f, 0.16f, 0.62f);
            return new Color(0.58f, 0.24f, 0.72f, 0.68f);
        }

        private static Color DensityColor(DensityStageView stage, bool progressing)
        {
            if (progressing) return new Color(0.95f, 0.68f, 0.18f, 0.64f);
            switch (stage)
            {
                case DensityStageView.Low: return new Color(0.30f, 0.72f, 0.36f, 0.42f);
                case DensityStageView.Medium: return new Color(0.30f, 0.58f, 0.94f, 0.56f);
                default: return new Color(0.66f, 0.34f, 0.92f, 0.68f);
            }
        }

        private GameObject Acquire(Stack<GameObject> pool, PrimitiveType primitive, string name)
        {
            if (pool.Count > 0) return pool.Pop();
            var view = GameObject.CreatePrimitive(primitive);
            view.name = name;
            view.hideFlags = HideFlags.DontSave;
            view.transform.SetParent(transform, false);
            var collider = view.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return view;
        }

        private GameObject AcquireDensity(DensityVisualKind kind)
        {
            var pool = DensityPool(kind);
            if (pool.Count > 0) return pool.Pop();
            var prefab = DensityPrefab(kind);
            if (prefab != null)
            {
                var instance = Instantiate(prefab, transform);
                instance.hideFlags = HideFlags.DontSave;
                return instance;
            }
            return Acquire(pool, PrimitiveType.Cube, "DensityStageView");
        }

        private Stack<GameObject> DensityPool(DensityVisualKind kind)
        {
            if (!_densityPools.TryGetValue(kind, out var pool))
            {
                pool = new Stack<GameObject>();
                _densityPools.Add(kind, pool);
            }
            return pool;
        }

        private GameObject DensityPrefab(DensityVisualKind kind)
        {
            switch (kind)
            {
                case DensityVisualKind.MediumOffice: return _densityMediumOfficePrefab;
                case DensityVisualKind.MediumApartment: return _densityMediumApartmentPrefab;
                case DensityVisualKind.HighOffice: return _densityHighOfficePrefab;
                case DensityVisualKind.HighApartment: return _densityHighApartmentPrefab;
                default: return null;
            }
        }

        private void RecycleDensity(DensityVisual visual)
        {
            visual.GameObject.SetActive(false);
            DensityPool(visual.Kind).Push(visual.GameObject);
        }

        private void ReleaseMissingDensity(ISet<StableId> desired)
        {
            var release = new List<StableId>();
            foreach (var pair in _densityViews)
                if (!desired.Contains(pair.Key)) release.Add(pair.Key);
            for (var index = 0; index < release.Count; index++)
            {
                var visual = _densityViews[release[index]];
                _densityViews.Remove(release[index]);
                RecycleDensity(visual);
            }
        }

        private static void ReleaseMissing(
            SortedDictionary<StableId, GameObject> active,
            Stack<GameObject> pool,
            ISet<StableId> desired)
        {
            var release = new List<StableId>();
            foreach (var pair in active)
            {
                if (!desired.Contains(pair.Key)) release.Add(pair.Key);
            }
            for (var index = 0; index < release.Count; index++)
            {
                var view = active[release[index]];
                active.Remove(release[index]);
                view.SetActive(false);
                pool.Push(view);
            }
        }

        private void ReleaseAll()
        {
            foreach (var pair in _pumpViews) { pair.Value.SetActive(false); _pumpPool.Push(pair.Value); }
            foreach (var pair in _densityViews) RecycleDensity(pair.Value);
            _pumpViews.Clear();
            _densityViews.Clear();
        }

        private void EnsureResources()
        {
            if (_meshFilter != null) return;
            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = "WaterDensityOverlayMesh",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
                bounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
            _pumpMaterial = CreateMaterial("WaterPumpMaterial", Color.white);
            _densityMaterial = CreateMaterial("DensityStageMaterial", Color.white);
            _overlayMaterial = CreateMaterial("WaterDensityOverlayMaterial", Color.white);
            _densityMediumOfficePrefab = Resources.Load<GameObject>(
                "OpenSimCity/Environment/DensityMediumOffice");
            _densityMediumApartmentPrefab = Resources.Load<GameObject>(
                "OpenSimCity/Environment/DensityMediumApartment");
            _densityHighOfficePrefab = Resources.Load<GameObject>(
                "OpenSimCity/Environment/DensityHighOffice");
            _densityHighApartmentPrefab = Resources.Load<GameObject>(
                "OpenSimCity/Environment/DensityHighApartment");
            _properties = new MaterialPropertyBlock();
            _meshRenderer.sharedMaterial = _overlayMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No water-density shader is available.");
            return new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
        }

        private void OnDestroy()
        {
            foreach (var pair in _pumpViews) Destroy(pair.Value);
            foreach (var pair in _densityViews) Destroy(pair.Value.GameObject);
            while (_pumpPool.Count > 0) Destroy(_pumpPool.Pop());
            foreach (var pair in _densityPools)
                while (pair.Value.Count > 0) Destroy(pair.Value.Pop());
            if (_mesh != null) Destroy(_mesh);
            if (_pumpMaterial != null) Destroy(_pumpMaterial);
            if (_densityMaterial != null) Destroy(_densityMaterial);
            if (_overlayMaterial != null) Destroy(_overlayMaterial);
        }

        private enum DensityVisualKind : byte
        {
            UpgradeMarker,
            MediumOffice,
            MediumApartment,
            HighOffice,
            HighApartment
        }

        private readonly struct DensityVisual
        {
            public DensityVisual(DensityVisualKind kind, GameObject gameObject)
            {
                Kind = kind;
                GameObject = gameObject;
            }

            public DensityVisualKind Kind { get; }
            public GameObject GameObject { get; }
        }
    }

    public static class WaterDensityPresentationBootstrap
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
            if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()) Install(scene);
        }
        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (UnityEngine.Object.FindFirstObjectByType<WaterDensityViewCoordinator>() != null) return;
            var root = new GameObject(WaterDensityViewCoordinator.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<WaterDensityViewCoordinator>();
        }
    }
}
