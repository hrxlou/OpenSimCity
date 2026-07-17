using System.Collections.Generic;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;
using UnityEngine;

namespace OpenSimCity.Presentation.Overlays
{
    /// <summary>
    /// Draws the active placement overlay and the in-progress construction preview
    /// as flat colored cell quads on the ground plane. It consumes the ambient
    /// <see cref="CityRuntimeContext"/> read-model snapshots and rebuilds its two
    /// meshes only when a snapshot changes, so there is no per-cell GameObject and
    /// no per-cell MonoBehaviour Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementOverlayRenderer : MonoBehaviour
    {
        private const float OverlayElevation = 0.02f;
        private const float PreviewElevation = 0.05f;

        private CityRuntimeContext _boundContext;
        private MeshRenderInstance _overlay;
        private MeshRenderInstance _preview;
        private bool _overlayDirty;
        private bool _previewDirty;
        private PlacementOverlaySnapshot _overlaySnapshot;
        private ConstructionPreview _previewSnapshot = ConstructionPreview.Empty;

        public int OverlayCellCount => _overlaySnapshot?.CellCount ?? 0;

        public int PreviewCellCount => _previewSnapshot != null && _previewSnapshot.HasPreview
            ? _previewSnapshot.RequestedCells.Count
            : 0;

        public bool HasActiveOverlay => _overlaySnapshot != null;

        private void OnDisable()
        {
            Unbind();
            _overlay.Dispose();
            _preview.Dispose();
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _boundContext))
                Bind(current);

            if (_overlayDirty)
                RebuildOverlay();
            if (_previewDirty)
                RebuildPreview();
        }

        private void Bind(CityRuntimeContext context)
        {
            Unbind();
            _boundContext = context;
            if (_boundContext == null)
            {
                _overlaySnapshot = null;
                _previewSnapshot = ConstructionPreview.Empty;
                _overlayDirty = true;
                _previewDirty = true;
                return;
            }

            _boundContext.OverlayChanged += HandleOverlayChanged;
            _boundContext.PreviewChanged += HandlePreviewChanged;
            _overlaySnapshot = _boundContext.ActiveOverlaySnapshot;
            _previewSnapshot = _boundContext.Tool.Preview;
            _overlayDirty = true;
            _previewDirty = true;
        }

        private void Unbind()
        {
            if (_boundContext == null)
                return;

            _boundContext.OverlayChanged -= HandleOverlayChanged;
            _boundContext.PreviewChanged -= HandlePreviewChanged;
            _boundContext = null;
        }

        private void HandleOverlayChanged(PlacementOverlaySnapshot snapshot)
        {
            _overlaySnapshot = snapshot;
            _overlayDirty = true;
        }

        private void HandlePreviewChanged(ConstructionPreview preview)
        {
            _previewSnapshot = preview ?? ConstructionPreview.Empty;
            _previewDirty = true;
        }

        private void RebuildOverlay()
        {
            _overlayDirty = false;
            _overlay.EnsureCreated("PlacementOverlay", transform);

            if (_overlaySnapshot == null || _boundContext == null || !_boundContext.HasWorldBounds)
            {
                _overlay.Clear();
                return;
            }

            var bounds = _boundContext.WorldBounds;
            var builder = new QuadMeshBuilder(_overlaySnapshot.CellCount);
            foreach (var cell in _overlaySnapshot.Cells)
            {
                builder.AddCell(
                    cell.Cell,
                    bounds,
                    OverlayElevation,
                    PlacementOverlayColors.Resolve(cell.ColorRole));
            }

            builder.Apply(_overlay.Mesh);
        }

        private void RebuildPreview()
        {
            _previewDirty = false;
            _preview.EnsureCreated("PlacementPreview", transform);

            if (_previewSnapshot == null || !_previewSnapshot.HasPreview ||
                _boundContext == null || !_boundContext.HasWorldBounds)
            {
                _preview.Clear();
                return;
            }

            var bounds = _boundContext.WorldBounds;
            var validColor = PlacementOverlayColors.Resolve(
                _previewSnapshot.Operation == ConstructionOperation.DemolishRoad ||
                _previewSnapshot.Operation == ConstructionOperation.ClearZone ||
                _previewSnapshot.Operation == ConstructionOperation.RemovePowerProducer
                    ? OverlayColorRole.Warning
                    : OverlayColorRole.Accent,
                0.75f);
            var invalidColor = PlacementOverlayColors.Resolve(OverlayColorRole.Danger, 0.75f);

            var validCells = new HashSet<CellCoord>();
            for (var index = 0; index < _previewSnapshot.ValidCells.Count; index++)
                validCells.Add(_previewSnapshot.ValidCells[index]);

            var builder = new QuadMeshBuilder(_previewSnapshot.RequestedCells.Count);
            foreach (var cell in _previewSnapshot.RequestedCells)
            {
                var color = _previewSnapshot.IsValid && validCells.Contains(cell)
                    ? validColor
                    : invalidColor;
                builder.AddCell(cell, bounds, PreviewElevation, color);
            }

            builder.Apply(_preview.Mesh);
        }

        /// <summary>One MeshFilter/MeshRenderer child rebuilt in place per snapshot.</summary>
        private struct MeshRenderInstance
        {
            private GameObject _gameObject;

            public Mesh Mesh { get; private set; }

            public void EnsureCreated(string name, Transform parent)
            {
                if (_gameObject != null)
                    return;

                _gameObject = new GameObject(name);
                _gameObject.transform.SetParent(parent, worldPositionStays: false);
                var filter = _gameObject.AddComponent<MeshFilter>();
                var renderer = _gameObject.AddComponent<MeshRenderer>();
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
                filter.sharedMesh = Mesh;
                renderer.sharedMaterial = OverlayMaterial.Shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            public void Clear()
            {
                Mesh?.Clear();
            }

            public void Dispose()
            {
                if (Mesh != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Object.Destroy(Mesh);
                    else
                        Object.DestroyImmediate(Mesh);
                    Mesh = null;
                }

                if (_gameObject != null)
                {
                    if (UnityEngine.Application.isPlaying)
                        Object.Destroy(_gameObject);
                    else
                        Object.DestroyImmediate(_gameObject);
                    _gameObject = null;
                }
            }
        }

        private struct QuadMeshBuilder
        {
            private readonly List<Vector3> _vertices;
            private readonly List<Color> _colors;
            private readonly List<int> _triangles;

            public QuadMeshBuilder(int cellCapacity)
            {
                var capacity = Mathf.Max(0, cellCapacity) * 4;
                _vertices = new List<Vector3>(capacity);
                _colors = new List<Color>(capacity);
                _triangles = new List<int>(Mathf.Max(0, cellCapacity) * 6);
            }

            public void AddCell(CellCoord cell, WorldBoundsReadModel bounds, float elevation, Color color)
            {
                var size = bounds.CellSize;
                var minX = bounds.MinX + cell.X * size;
                var minZ = bounds.MinZ + cell.Y * size;
                var maxX = minX + size;
                var maxZ = minZ + size;

                var baseIndex = _vertices.Count;
                _vertices.Add(new Vector3(minX, elevation, minZ));
                _vertices.Add(new Vector3(minX, elevation, maxZ));
                _vertices.Add(new Vector3(maxX, elevation, maxZ));
                _vertices.Add(new Vector3(maxX, elevation, minZ));
                for (var index = 0; index < 4; index++)
                    _colors.Add(color);

                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 3);
            }

            public void Apply(Mesh mesh)
            {
                if (mesh == null)
                    return;

                mesh.Clear();
                if (_vertices.Count == 0)
                    return;

                mesh.SetVertices(_vertices);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
            }
        }

        private static class OverlayMaterial
        {
            private static Material _shared;

            public static Material Shared
            {
                get
                {
                    if (_shared != null)
                        return _shared;

                    var shader = Shader.Find("Sprites/Default") ??
                                 Shader.Find("Universal Render Pipeline/Unlit") ??
                                 Shader.Find("Unlit/Color");
                    _shared = new Material(shader) { name = "OpenSimCity Overlay" };
                    return _shared;
                }
            }
        }
    }
}
