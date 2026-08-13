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
    /// Single scheduler for sampled buses, ordered line geometry and stop markers.
    /// Generated view objects have no scripts and are recycled centrally.
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
                _overlayRenderer.Rebuild(_overlay.Lines, _overlay.Stops, _context.WorldBounds);
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

    /// <summary>Central pool for behaviour-free line renderers and stop markers.</summary>
    public sealed class TransitOverlayRenderer : IDisposable
    {
        private readonly Transform _parent;
        private readonly Material _lineMaterial;
        private readonly Material _stopMaterial;
        private readonly List<LineRenderer> _activeLines = new List<LineRenderer>();
        private readonly Stack<LineRenderer> _pooledLines = new Stack<LineRenderer>();
        private readonly List<GameObject> _activeStops = new List<GameObject>();
        private readonly Stack<GameObject> _pooledStops = new Stack<GameObject>();
        private bool _visible = true;

        public TransitOverlayRenderer(Transform parent, Material lineMaterial, Material stopMaterial)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _lineMaterial = lineMaterial ?? throw new ArgumentNullException(nameof(lineMaterial));
            _stopMaterial = stopMaterial ?? throw new ArgumentNullException(nameof(stopMaterial));
        }

        public int ActiveLineCount => _activeLines.Count;
        public int ActiveStopCount => _activeStops.Count;

        public void Rebuild(
            IReadOnlyList<TransitOverlayLineData> lines,
            IReadOnlyList<TransitOverlayStopData> stops,
            WorldBoundsReadModel bounds)
        {
            ResizeLines(lines?.Count ?? 0);
            ResizeStops(stops?.Count ?? 0);
            for (var index = 0; index < _activeLines.Count; index++)
            {
                var source = lines[index];
                var renderer = _activeLines[index];
                renderer.positionCount = source.Cells.Count;
                renderer.widthMultiplier = Math.Max(0.03f, bounds.CellSize * 0.08f);
                for (var cellIndex = 0; cellIndex < source.Cells.Count; cellIndex++)
                    renderer.SetPosition(cellIndex, CellCenter(bounds, source.Cells[cellIndex], bounds.CellSize * 0.07f));
                renderer.gameObject.name = $"TransitLine-{source.LineId.Value}";
            }
            for (var index = 0; index < _activeStops.Count; index++)
            {
                var source = stops[index];
                var view = _activeStops[index];
                view.name = $"TransitStop-{source.Id.Value}";
                view.transform.localPosition = CellCenter(bounds, source.RoadCell, bounds.CellSize * 0.12f);
                view.transform.localScale = new Vector3(
                    bounds.CellSize * 0.22f,
                    bounds.CellSize * 0.12f,
                    bounds.CellSize * 0.22f);
            }
            SetVisible(_visible);
        }

        public void SetVisible(bool value)
        {
            _visible = value;
            for (var index = 0; index < _activeLines.Count; index++)
                _activeLines[index].enabled = value;
            for (var index = 0; index < _activeStops.Count; index++)
                _activeStops[index].SetActive(value);
        }

        public void Clear()
        {
            ResizeLines(0);
            ResizeStops(0);
        }

        public void Dispose()
        {
            Clear();
            while (_pooledLines.Count > 0)
                UnityEngine.Object.Destroy(_pooledLines.Pop().gameObject);
            while (_pooledStops.Count > 0)
                UnityEngine.Object.Destroy(_pooledStops.Pop());
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

        private void ResizeStops(int count)
        {
            while (_activeStops.Count > count)
            {
                var last = _activeStops[_activeStops.Count - 1];
                _activeStops.RemoveAt(_activeStops.Count - 1);
                last.SetActive(false);
                _pooledStops.Push(last);
            }
            while (_activeStops.Count < count)
            {
                var stop = _pooledStops.Count > 0 ? _pooledStops.Pop() : CreateStop();
                stop.SetActive(true);
                _activeStops.Add(stop);
            }
        }

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

        private GameObject CreateStop()
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            view.name = "TransitStop";
            view.hideFlags = HideFlags.DontSave;
            view.transform.SetParent(_parent, false);
            var collider = view.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _stopMaterial;
            return view;
        }

        private static Vector3 CellCenter(WorldBoundsReadModel bounds, OpenSimCity.Core.Mathematics.CellCoord cell, float height)
        {
            return new Vector3(
                bounds.MinX + (cell.X + 0.5f) * bounds.CellSize,
                height,
                bounds.MinZ + (cell.Y + 0.5f) * bounds.CellSize);
        }
    }
}
