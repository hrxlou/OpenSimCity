using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using UnityEngine;

namespace OpenSimCity.Presentation.Vehicles
{
    /// <summary>
    /// Single presentation scheduler for every sampled vehicle and the congestion
    /// datamap. Vehicle GameObjects have no behaviours and are recycled through one
    /// pool. Presentation sampling never writes to aggregate Simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficViewCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "TrafficViews";

        [SerializeField, Range(0, TrafficVisualizationReadModel.MaximumAllowedVehicleSamples)]
        private int visibleVehicleLimit = TrafficVisualizationReadModel.DefaultMaximumVehicleSamples;
        [SerializeField, Min(0.1f)] private float routeLoopSeconds = 12f;
        [SerializeField] private bool congestionMapVisible = true;
        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField] private Material vehicleMaterial;
        [SerializeField] private Material congestionMaterial;

        private CityRuntimeContext _context;
        private TrafficVisualizationReadModel _model;
        private VehicleViewScheduler _vehicleScheduler;
        private TrafficCongestionMeshRenderer _congestionRenderer;
        private Material _ownedVehicleMaterial;
        private Material _ownedCongestionMaterial;
        private int _observedModelVersion = -1;

        public int VisibleVehicleLimit => visibleVehicleLimit;
        public int ActiveVehicleCount => _vehicleScheduler?.ActiveCount ?? 0;
        public int PooledVehicleCount => _vehicleScheduler?.PooledCount ?? 0;
        public int RenderedCongestionEdgeCount => _congestionRenderer?.RenderedEdgeCount ?? 0;
        public int RenderedUnroutedMarkerCount => _congestionRenderer?.RenderedUnroutedCount ?? 0;
        public bool CongestionMapVisible => congestionMapVisible;
        public TrafficVisualizationReadModel Model => _model;

        public void SetVisibleVehicleLimit(int value)
        {
            if (value < 0 || value > TrafficVisualizationReadModel.MaximumAllowedVehicleSamples)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (visibleVehicleLimit == value)
                return;
            visibleVehicleLimit = value;
            _observedModelVersion = -1;
        }

        public void SetCongestionMapVisible(bool value)
        {
            congestionMapVisible = value;
            _congestionRenderer?.SetVisible(value);
        }

        public void Configure(
            GameObject sampleVehiclePrefab,
            Material sampleVehicleMaterial,
            Material dataMapMaterial)
        {
            vehiclePrefab = sampleVehiclePrefab;
            vehicleMaterial = sampleVehicleMaterial;
            congestionMaterial = dataMapMaterial;
            ResetPresentationObjects();
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current;
                ResetModel();
            }

            if (_context == null || !_context.HasWorldBounds)
                return;

            EnsurePresentationObjects();
            if (_model == null)
                _model = new TrafficVisualizationReadModel(_context.Runtime.TrafficRoutingModel);

            _model.SetMaximumVehicleSamples(visibleVehicleLimit);
            _model.RefreshIfNeeded();
            if (_observedModelVersion != _model.Version)
            {
                _observedModelVersion = _model.Version;
                _vehicleScheduler.ApplySamples(_model.VehicleSamples);
                _congestionRenderer.Rebuild(
                    _model.Edges,
                    _model.Unrouted,
                    _context.WorldBounds);
                _congestionRenderer.SetVisible(congestionMapVisible);
            }

            _vehicleScheduler.UpdatePositions(
                _context.WorldBounds,
                Time.unscaledTimeAsDouble,
                Math.Max(0.1f, routeLoopSeconds));
        }

        private void OnDestroy()
        {
            DisposePresentationObjects();
        }

        private void ResetModel()
        {
            _model = null;
            _observedModelVersion = -1;
            _vehicleScheduler?.ReleaseAll();
            _congestionRenderer?.Clear();
        }

        private void ResetPresentationObjects()
        {
            DisposePresentationObjects();
            if (isActiveAndEnabled)
                EnsurePresentationObjects();
            _observedModelVersion = -1;
        }

        private void EnsurePresentationObjects()
        {
            if (_vehicleScheduler == null)
            {
                _vehicleScheduler = new VehicleViewScheduler(
                    transform,
                    vehiclePrefab,
                    ResolveVehicleMaterial());
            }

            if (_congestionRenderer == null)
            {
                _congestionRenderer = new TrafficCongestionMeshRenderer(
                    transform,
                    ResolveCongestionMaterial());
            }
        }

        private Material ResolveVehicleMaterial()
        {
            if (vehicleMaterial != null)
                return vehicleMaterial;
            if (_ownedVehicleMaterial == null)
            {
                _ownedVehicleMaterial = CreateRuntimeMaterial(
                    "TrafficVehicleRuntimeMaterial",
                    new Color(0.93f, 0.93f, 0.96f, 1f));
            }
            return _ownedVehicleMaterial;
        }

        private Material ResolveCongestionMaterial()
        {
            if (congestionMaterial != null)
                return congestionMaterial;
            if (_ownedCongestionMaterial == null)
            {
                _ownedCongestionMaterial = CreateRuntimeMaterial(
                    "TrafficCongestionRuntimeMaterial",
                    Color.white);
            }
            return _ownedCongestionMaterial;
        }

        private static Material CreateRuntimeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        private void DisposePresentationObjects()
        {
            _vehicleScheduler?.Dispose();
            _vehicleScheduler = null;
            _congestionRenderer?.Dispose();
            _congestionRenderer = null;
            if (_ownedVehicleMaterial != null)
                UnityEngine.Object.Destroy(_ownedVehicleMaterial);
            if (_ownedCongestionMaterial != null)
                UnityEngine.Object.Destroy(_ownedCongestionMaterial);
            _ownedVehicleMaterial = null;
            _ownedCongestionMaterial = null;
        }
    }

    /// <summary>
    /// Central route interpolation scheduler. It owns all active vehicle transforms;
    /// individual vehicle GameObjects contain no MonoBehaviour and never call Update.
    /// </summary>
    public sealed class VehicleViewScheduler : IDisposable
    {
        private readonly VehicleViewPool _pool;
        private IReadOnlyList<VehicleSampleData> _samples =
            Array.AsReadOnly(Array.Empty<VehicleSampleData>());

        public VehicleViewScheduler(
            Transform parent,
            GameObject prefab = null,
            Material fallbackMaterial = null)
        {
            _pool = new VehicleViewPool(parent, prefab, fallbackMaterial);
        }

        public int ActiveCount => _pool.ActiveCount;
        public int PooledCount => _pool.PooledCount;
        public IReadOnlyList<VehicleSampleData> Samples => _samples;

        public void ApplySamples(IReadOnlyList<VehicleSampleData> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            _samples = samples;
            _pool.Resize(samples.Count);
        }

        public void UpdatePositions(
            WorldBoundsReadModel bounds,
            double elapsedSeconds,
            double routeLoopSeconds)
        {
            if (routeLoopSeconds <= 0d || double.IsNaN(routeLoopSeconds) || double.IsInfinity(routeLoopSeconds))
                throw new ArgumentOutOfRangeException(nameof(routeLoopSeconds));

            for (var index = 0; index < _samples.Count; index++)
            {
                var sample = _samples[index];
                var normalized = elapsedSeconds / routeLoopSeconds +
                                 sample.PhaseOffsetPermille / 1000d;
                normalized -= Math.Floor(normalized);
                var progressPermille = Math.Min(999, (int)(normalized * 1000d));
                var position = sample.Path.Evaluate(progressPermille);
                var from = CellCenter(bounds, position.From);
                var to = CellCenter(bounds, position.To);
                var transform = _pool.GetActiveTransform(index);
                transform.localPosition = Vector3.Lerp(
                    from,
                    to,
                    position.SegmentProgressPermille / 1000f);

                var direction = to - from;
                if (direction.sqrMagnitude > 0.0001f)
                    transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.localScale = new Vector3(
                    bounds.CellSize * 0.22f,
                    bounds.CellSize * 0.14f,
                    bounds.CellSize * 0.40f);
            }
        }

        public GameObject GetActiveView(int index) => _pool.GetActiveView(index);
        public void ReleaseAll() => _pool.Resize(0);
        public void Dispose() => _pool.Dispose();

        private static Vector3 CellCenter(WorldBoundsReadModel bounds, OpenSimCity.Core.Mathematics.CellCoord cell)
        {
            return new Vector3(
                bounds.MinX + (cell.X + 0.5f) * bounds.CellSize,
                bounds.CellSize * 0.16f,
                bounds.MinZ + (cell.Y + 0.5f) * bounds.CellSize);
        }
    }

    /// <summary>Reusable pool for behaviour-free vehicle view GameObjects.</summary>
    public sealed class VehicleViewPool : IDisposable
    {
        private readonly Transform _parent;
        private readonly GameObject _prefab;
        private readonly Material _fallbackMaterial;
        private readonly List<GameObject> _active = new List<GameObject>();
        private readonly Stack<GameObject> _pooled = new Stack<GameObject>();

        public VehicleViewPool(
            Transform parent,
            GameObject prefab = null,
            Material fallbackMaterial = null)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _prefab = prefab;
            _fallbackMaterial = fallbackMaterial;
        }

        public int ActiveCount => _active.Count;
        public int PooledCount => _pooled.Count;

        public void Resize(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (_active.Count > count)
            {
                var lastIndex = _active.Count - 1;
                var view = _active[lastIndex];
                _active.RemoveAt(lastIndex);
                view.SetActive(false);
                _pooled.Push(view);
            }

            while (_active.Count < count)
            {
                var view = _pooled.Count > 0 ? _pooled.Pop() : CreateView();
                view.SetActive(true);
                _active.Add(view);
            }
        }

        public Transform GetActiveTransform(int index)
        {
            return GetActiveView(index).transform;
        }

        public GameObject GetActiveView(int index)
        {
            if ((uint)index >= (uint)_active.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _active[index];
        }

        public void Dispose()
        {
            for (var index = 0; index < _active.Count; index++)
                DestroyView(_active[index]);
            _active.Clear();
            while (_pooled.Count > 0)
                DestroyView(_pooled.Pop());
        }

        private GameObject CreateView()
        {
            GameObject view;
            if (_prefab != null)
            {
                view = UnityEngine.Object.Instantiate(_prefab, _parent);
            }
            else
            {
                view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.transform.SetParent(_parent, false);
                var collider = view.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.Destroy(collider);
                var renderer = view.GetComponent<Renderer>();
                if (renderer != null && _fallbackMaterial != null)
                    renderer.sharedMaterial = _fallbackMaterial;
            }

            view.name = "TrafficVehicle";
            if (view.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            {
                DestroyView(view);
                throw new InvalidOperationException(
                    "A pooled vehicle prefab must not contain per-vehicle MonoBehaviours.");
            }
            return view;
        }

        private static void DestroyView(GameObject view)
        {
            if (view != null)
                UnityEngine.Object.Destroy(view);
        }
    }

    /// <summary>
    /// One mesh for all congestion edges and unrouted markers. It has no Update;
    /// the coordinator rebuilds it only when the versioned traffic query changes.
    /// </summary>
    public sealed class TrafficCongestionMeshRenderer : IDisposable
    {
        private readonly GameObject _root;
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;

        public TrafficCongestionMeshRenderer(Transform parent, Material material)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            _root = new GameObject("TrafficCongestionDataMap");
            _root.transform.SetParent(parent, false);
            var filter = _root.AddComponent<MeshFilter>();
            _renderer = _root.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = "TrafficCongestionDataMapMesh",
                hideFlags = HideFlags.DontSave
            };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;
            _renderer.sharedMaterial = material;
        }

        public int RenderedEdgeCount { get; private set; }
        public int RenderedUnroutedCount { get; private set; }

        public void SetVisible(bool value)
        {
            if (_root != null)
                _root.SetActive(value);
        }

        public void Rebuild(
            IReadOnlyList<TrafficEdgeViewData> edges,
            IReadOnlyList<TrafficUnroutedViewData> unrouted,
            WorldBoundsReadModel bounds)
        {
            if (edges == null)
                throw new ArgumentNullException(nameof(edges));
            if (unrouted == null)
                throw new ArgumentNullException(nameof(unrouted));

            var quadCount = edges.Count + unrouted.Count;
            var vertices = new Vector3[quadCount * 4];
            var colors = new Color[vertices.Length];
            var triangles = new int[quadCount * 6];
            var quad = 0;
            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                var a = CellCenter(bounds, edge.Edge.A, 0.055f);
                var b = CellCenter(bounds, edge.Edge.B, 0.055f);
                var direction = b - a;
                var perpendicular = new Vector3(-direction.z, 0f, direction.x).normalized;
                var width = bounds.CellSize * (edge.IsCongested ? 0.17f : 0.10f);
                AddQuad(
                    vertices,
                    colors,
                    triangles,
                    quad++,
                    a - perpendicular * width,
                    a + perpendicular * width,
                    b + perpendicular * width,
                    b - perpendicular * width,
                    CongestionColor(edge.VolumeCapacityPermille));
            }

            for (var index = 0; index < unrouted.Count; index++)
            {
                var row = unrouted[index];
                var center = CellCenter(bounds, row.OriginRoadCell, 0.075f);
                var half = bounds.CellSize * 0.22f;
                AddQuad(
                    vertices,
                    colors,
                    triangles,
                    quad++,
                    center + new Vector3(-half, 0f, -half),
                    center + new Vector3(-half, 0f, half),
                    center + new Vector3(half, 0f, half),
                    center + new Vector3(half, 0f, -half),
                    new Color(0.82f, 0.18f, 0.92f, 0.90f));
            }

            _mesh.Clear();
            if (vertices.Length > 0)
            {
                _mesh.vertices = vertices;
                _mesh.colors = colors;
                _mesh.triangles = triangles;
                _mesh.RecalculateBounds();
            }
            RenderedEdgeCount = edges.Count;
            RenderedUnroutedCount = unrouted.Count;
        }

        public void Clear()
        {
            _mesh.Clear();
            RenderedEdgeCount = 0;
            RenderedUnroutedCount = 0;
        }

        public void Dispose()
        {
            if (_mesh != null)
                UnityEngine.Object.Destroy(_mesh);
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        private static void AddQuad(
            Vector3[] vertices,
            Color[] colors,
            int[] triangles,
            int quadIndex,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Color color)
        {
            var vertex = quadIndex * 4;
            vertices[vertex] = a;
            vertices[vertex + 1] = b;
            vertices[vertex + 2] = c;
            vertices[vertex + 3] = d;
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            var triangle = quadIndex * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        private static Vector3 CellCenter(
            WorldBoundsReadModel bounds,
            OpenSimCity.Core.Mathematics.CellCoord cell,
            float heightScale)
        {
            return new Vector3(
                bounds.MinX + (cell.X + 0.5f) * bounds.CellSize,
                bounds.CellSize * heightScale,
                bounds.MinZ + (cell.Y + 0.5f) * bounds.CellSize);
        }

        private static Color CongestionColor(int ratioPermille)
        {
            if (ratioPermille <= 500)
                return new Color(0.17f, 0.78f, 0.38f, 0.78f);
            if (ratioPermille <= 1000)
            {
                var t = (ratioPermille - 500) / 500f;
                return Color.Lerp(
                    new Color(0.17f, 0.78f, 0.38f, 0.78f),
                    new Color(0.96f, 0.76f, 0.15f, 0.86f),
                    t);
            }

            var overload = Mathf.Clamp01((ratioPermille - 1000) / 1500f);
            return Color.Lerp(
                new Color(0.96f, 0.76f, 0.15f, 0.86f),
                new Color(0.90f, 0.18f, 0.15f, 0.94f),
                overload);
        }
    }
}
