using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Presentation.Vehicles;
using Unity.Profiling;
using UnityEngine;

namespace OpenSimCity.Presentation.Transit
{
    /// <summary>
    /// Single scheduler for sampled buses, ordered line geometry, direction marks,
    /// stop markers and blocked-OD warnings. Generated view objects have no scripts
    /// and are recycled centrally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransitViewCoordinator : MonoBehaviour
    {
        public const string RuntimeObjectName = "TransitViews";
        public const string UpdateProfilerMarkerName = "OpenSimCity.TransitViews.Update";
        private static readonly ProfilerMarker UpdateProfilerMarker =
            new ProfilerMarker(UpdateProfilerMarkerName);

        [SerializeField, Range(0, TransitVehicleSamplingReadModel.MaximumAllowedSamples)]
        private int visibleBusLimit = TransitVehicleSamplingReadModel.DefaultMaximumSamples;
        [SerializeField, Min(0.1f)] private float routeLoopSeconds = 18f;
        [SerializeField] private bool overlayVisible = true;
        [SerializeField] private GameObject busPrefab;
        [SerializeField] private Material busMaterial;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material stopMaterial;

        private CityRuntimeContext _context;
        private TransitRuntimeModule _module;
        private TransitOverlayReadModel _overlay;
        private TransitVehicleSamplingReadModel _sampling;
        private VehicleViewScheduler _busScheduler;
        private TransitOverlayRenderer _overlayRenderer;
        private Material _ownedBusMaterial;
        private Material _ownedLineMaterial;
        private Material _ownedStopMaterial;
        private int _observedSamplingVersion = -1;
        private int _observedOverlayVersion = -1;

        public int VisibleBusLimit => visibleBusLimit;
        public int ActiveBusCount => _busScheduler?.ActiveCount ?? 0;
        public int PooledBusCount => _busScheduler?.PooledCount ?? 0;
        public int RenderedLineCount => _overlayRenderer?.ActiveLineCount ?? 0;
        public int RenderedStopCount => _overlayRenderer?.ActiveStopCount ?? 0;
        public int RenderedDirectionCount => _overlayRenderer?.ActiveDirectionCount ?? 0;
        public int RenderedIssueCount => _overlayRenderer?.ActiveIssueCount ?? 0;
        public TransitOverlayReadModel OverlayModel => _overlay;
        public TransitVehicleSamplingReadModel SamplingModel => _sampling;

        public void SetVisibleBusLimit(int value)
        {
            if (value < 0 || value > TransitVehicleSamplingReadModel.MaximumAllowedSamples)
                throw new ArgumentOutOfRangeException(nameof(value));
            visibleBusLimit = value;
            _sampling?.SetMaximumSamples(value);
            _observedSamplingVersion = -1;
        }

        public void SetOverlayVisible(bool value)
        {
            overlayVisible = value;
            _overlayRenderer?.SetVisible(value);
        }

        private void Update()
        {
            using (UpdateProfilerMarker.Auto())
                UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current;
                ResetModels();
            }
            if (_context == null || !_context.HasWorldBounds)
                return;

            EnsureObjects();
            if (_module == null)
                _module = TransitRuntimeRegistry.GetOrCreate(_context.Runtime);
            if (_overlay == null)
                _overlay = new TransitOverlayReadModel(_module);
            if (_sampling == null)
            {
                _sampling = new TransitVehicleSamplingReadModel(_overlay);
                _sampling.SetMaximumSamples(visibleBusLimit);
            }

            _overlay.RefreshIfNeeded();
            _sampling.RefreshIfNeeded();
            if (_observedSamplingVersion != _sampling.Version)
            {
                _observedSamplingVersion = _sampling.Version;
                _busScheduler.ApplySamples(_sampling.Samples);
            }
            if (_observedOverlayVersion != _overlay.Version)
            {
                _observedOverlayVersion = _overlay.Version;
                _overlayRenderer.Rebuild(
                    _overlay.Lines,
                    _overlay.Stops,
                    _overlay.Issues,
                    _context.WorldBounds);
                _overlayRenderer.SetVisible(overlayVisible);
            }

            _busScheduler.UpdatePositions(
                _context.WorldBounds,
                Time.unscaledTimeAsDouble,
                Math.Max(0.1f, routeLoopSeconds));
        }

        private void OnDestroy()
        {
            DisposeObjects();
        }

        private void ResetModels()
        {
            _module = null;
            _overlay = null;
            _sampling = null;
            _observedSamplingVersion = -1;
            _observedOverlayVersion = -1;
            _busScheduler?.ReleaseAll();
            _overlayRenderer?.Clear();
        }

        private void EnsureObjects()
        {
            if (_busScheduler == null)
                _busScheduler = new VehicleViewScheduler(transform, busPrefab, ResolveBusMaterial());
            if (_overlayRenderer == null)
                _overlayRenderer = new TransitOverlayRenderer(
                    transform,
                    ResolveLineMaterial(),
                    ResolveStopMaterial());
        }

        private Material ResolveBusMaterial()
        {
            if (busMaterial != null) return busMaterial;
            return _ownedBusMaterial ?? (_ownedBusMaterial = CreateMaterial(
                "TransitBusRuntimeMaterial", new Color(0.18f, 0.62f, 0.88f, 1f)));
        }

        private Material ResolveLineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            return _ownedLineMaterial ?? (_ownedLineMaterial = CreateMaterial(
                "TransitLineRuntimeMaterial", new Color(0.12f, 0.72f, 0.82f, 0.85f)));
        }

        private Material ResolveStopMaterial()
        {
            if (stopMaterial != null) return stopMaterial;
            return _ownedStopMaterial ?? (_ownedStopMaterial = CreateMaterial(
                "TransitStopRuntimeMaterial", new Color(0.98f, 0.86f, 0.18f, 1f)));
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No runtime shader is available for transit views.");
            return new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }

        private void DisposeObjects()
        {
            _busScheduler?.Dispose();
            _overlayRenderer?.Dispose();
            _busScheduler = null;
            _overlayRenderer = null;
            DestroyOwned(_ownedBusMaterial);
            DestroyOwned(_ownedLineMaterial);
            DestroyOwned(_ownedStopMaterial);
            _ownedBusMaterial = null;
            _ownedLineMaterial = null;
            _ownedStopMaterial = null;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value != null)
                UnityEngine.Object.Destroy(value);
        }
    }

    /// <summary>
    /// Central pool for behaviour-free line renderers, stop markers, direction marks
    /// and warning marks. Shape and orientation provide non-color state channels.
    /// </summary>
    public sealed class TransitOverlayRenderer : IDisposable
    {
        private readonly Transform _parent;
        private readonly Material _lineMaterial;
        private readonly Material _markerMaterial;
        private readonly List<LineRenderer> _activeLines = new List<LineRenderer>();
        private readonly Stack<LineRenderer> _pooledLines = new Stack<LineRenderer>();
        private readonly List<GameObject> _activeStops = new List<GameObject>();
        private readonly Stack<GameObject> _pooledStops = new Stack<GameObject>();
        private readonly List<GameObject> _activeDirections = new List<GameObject>();
        private readonly Stack<GameObject> _pooledDirections = new Stack<GameObject>();
        private readonly List<GameObject> _activeIssues = new List<GameObject>();
        private readonly Stack<GameObject> _pooledIssues = new Stack<GameObject>();
        private bool _visible = true;

        public TransitOverlayRenderer(Transform parent, Material lineMaterial, Material markerMaterial)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _lineMaterial = lineMaterial ?? throw new ArgumentNullException(nameof(lineMaterial));
            _markerMaterial = markerMaterial ?? throw new ArgumentNullException(nameof(markerMaterial));
        }

        public int ActiveLineCount => _activeLines.Count;
        public int ActiveStopCount => _activeStops.Count;
        public int ActiveDirectionCount => _activeDirections.Count;
        public int ActiveIssueCount => _activeIssues.Count;

        public void Rebuild(
            IReadOnlyList<TransitOverlayLineData> lines,
            IReadOnlyList<TransitOverlayStopData> stops,
            IReadOnlyList<TransitOverlayIssueData> issues,
            WorldBoundsReadModel bounds)
        {
            var lineCount = lines?.Count ?? 0;
            var stopCount = stops?.Count ?? 0;
            var issueCount = issues?.Count ?? 0;
            ResizeLines(lineCount);
            ResizeStops(stopCount);
            ResizeDirections(lineCount);
            ResizeIssues(issueCount);

            for (var index = 0; index < _activeLines.Count; index++)
            {
                var source = lines[index];
                var renderer = _activeLines[index];
                renderer.positionCount = source.Cells.Count;
                renderer.widthMultiplier = Math.Max(0.03f, bounds.CellSize * 0.08f);
                for (var cellIndex = 0; cellIndex < source.Cells.Count; cellIndex++)
                    renderer.SetPosition(cellIndex, CellCenter(bounds, source.Cells[cellIndex], bounds.CellSize * 0.07f));
                renderer.gameObject.name = $"TransitLine-{source.LineId.Value}";

                var direction = _activeDirections[index];
                direction.name = $"TransitDirection-{source.LineId.Value}";
                ConfigureDirection(direction.transform, source.Cells, bounds);
            }
            for (var index = 0; index < _activeStops.Count; index++)
            {
                var source = stops[index];
                var view = _activeStops[index];
                view.name = $"TransitStop-{source.Id.Value}";
                view.transform.localPosition = CellCenter(bounds, source.RoadCell, bounds.CellSize * 0.12f);
                view.transform.localRotation = Quaternion.identity;
                view.transform.localScale = new Vector3(
                    bounds.CellSize * 0.22f,
                    bounds.CellSize * 0.12f,
                    bounds.CellSize * 0.22f);
            }
            for (var index = 0; index < _activeIssues.Count; index++)
            {
                var source = issues[index];
                var view = _activeIssues[index];
                view.name = $"TransitIssue-{source.Reason}-{source.OriginId.Value}";
                view.transform.localPosition = CellCenter(bounds, source.OriginRoadCell, bounds.CellSize * 0.25f);
                view.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                view.transform.localScale = new Vector3(
                    bounds.CellSize * 0.16f,
                    bounds.CellSize * 0.34f,
                    bounds.CellSize * 0.16f);
            }
            SetVisible(_visible);
        }

        public void SetVisible(bool value)
        {
            _visible = value;
            for (var index = 0; index < _activeLines.Count; index++)
                _activeLines[index].enabled = value;
            SetActive(_activeStops, value);
            SetActive(_activeDirections, value);
            SetActive(_activeIssues, value);
        }

        public void Clear()
        {
            ResizeLines(0);
            ResizeStops(0);
            ResizeDirections(0);
            ResizeIssues(0);
        }

        public void Dispose()
        {
            Clear();
            while (_pooledLines.Count > 0)
                UnityEngine.Object.Destroy(_pooledLines.Pop().gameObject);
            DestroyPool(_pooledStops);
            DestroyPool(_pooledDirections);
            DestroyPool(_pooledIssues);
        }

        private void ResizeLines(int count)
        {
            while (_activeLines.Count > count)
            {
                var last = _activeLines[_activeLines.Count - 1];
                _activeLines.RemoveAt(_activeLines.Count - 1);
                last.gameObject.SetActive(false);
                _pooledLines.Push(last);
            }
            while (_activeLines.Count < count)
            {
                var line = _pooledLines.Count > 0 ? _pooledLines.Pop() : CreateLine();
                line.gameObject.SetActive(true);
                _activeLines.Add(line);
            }
        }

        private void ResizeStops(int count) => ResizeViews(
            _activeStops,
            _pooledStops,
            count,
            () => CreatePrimitive("TransitStop", PrimitiveType.Cylinder));

        private void ResizeDirections(int count) => ResizeViews(
            _activeDirections,
            _pooledDirections,
            count,
            () => CreatePrimitive("TransitDirection", PrimitiveType.Cube));

        private void ResizeIssues(int count) => ResizeViews(
            _activeIssues,
            _pooledIssues,
            count,
            () => CreatePrimitive("TransitIssue", PrimitiveType.Cube));

        private LineRenderer CreateLine()
        {
            var view = new GameObject("TransitLine") { hideFlags = HideFlags.DontSave };
            view.transform.SetParent(_parent, false);
            var line = view.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = false;
            line.loop = false;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private GameObject CreatePrimitive(string name, PrimitiveType type)
        {
            var view = GameObject.CreatePrimitive(type);
            view.name = name;
            view.hideFlags = HideFlags.DontSave;
            view.transform.SetParent(_parent, false);
            var collider = view.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _markerMaterial;
            return view;
        }

        private static void ConfigureDirection(
            Transform marker,
            IReadOnlyList<OpenSimCity.Core.Mathematics.CellCoord> cells,
            WorldBoundsReadModel bounds)
        {
            if (cells == null || cells.Count == 0)
            {
                marker.localScale = Vector3.zero;
                return;
            }
            var fromIndex = Math.Max(0, (cells.Count - 1) / 2);
            var toIndex = Math.Min(cells.Count - 1, fromIndex + 1);
            if (fromIndex == toIndex && fromIndex > 0)
                fromIndex--;
            var from = CellCenter(bounds, cells[fromIndex], bounds.CellSize * 0.15f);
            var to = CellCenter(bounds, cells[toIndex], bounds.CellSize * 0.15f);
            var delta = to - from;
            marker.localPosition = Vector3.Lerp(from, to, 0.5f);
            marker.localRotation = delta.sqrMagnitude <= 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(delta.normalized, Vector3.up);
            marker.localScale = new Vector3(
                bounds.CellSize * 0.11f,
                bounds.CellSize * 0.07f,
                bounds.CellSize * 0.42f);
        }

        private static void ResizeViews(
            IList<GameObject> active,
            Stack<GameObject> pooled,
            int count,
            Func<GameObject> create)
        {
            while (active.Count > count)
            {
                var lastIndex = active.Count - 1;
                var view = active[lastIndex];
                active.RemoveAt(lastIndex);
                view.SetActive(false);
                pooled.Push(view);
            }
            while (active.Count < count)
            {
                var view = pooled.Count > 0 ? pooled.Pop() : create();
                view.SetActive(true);
                active.Add(view);
            }
        }

        private static void SetActive(IReadOnlyList<GameObject> views, bool value)
        {
            for (var index = 0; index < views.Count; index++)
                views[index].SetActive(value);
        }

        private static void DestroyPool(Stack<GameObject> pool)
        {
            while (pool.Count > 0)
                UnityEngine.Object.Destroy(pool.Pop());
        }

        private static Vector3 CellCenter(
            WorldBoundsReadModel bounds,
            OpenSimCity.Core.Mathematics.CellCoord cell,
            float height)
        {
            return new Vector3(
                bounds.MinX + (cell.X + 0.5f) * bounds.CellSize,
                height,
                bounds.MinZ + (cell.Y + 0.5f) * bounds.CellSize);
        }
    }
}
