using System;
using System.Collections.Generic;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSimCity.Presentation.Districts
{
    [DisallowMultipleComponent]
    public sealed class DistrictBoundaryView : MonoBehaviour
    {
        private CityRuntimeContext _context;
        private DistrictRuntimeModule _module;
        private Mesh _mesh;
        private Material _material;
        private int _version = -1;
        private int _selectionVersion = -1;
        public int RenderedSegmentCount { get; private set; }

        private void Awake()
        {
            var filter = gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "DistrictBoundaryMesh", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic(); filter.sharedMesh = _mesh;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            if (shader != null) { _material = new Material(shader) { hideFlags = HideFlags.DontSave }; renderer.sharedMaterial = _material; }
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current; _module = current == null ? null : DistrictRuntimeRegistry.GetOrCreate(current.Runtime); _version = -1;
            }
            if (_module == null || _context == null || !_context.HasWorldBounds) return;
            _module.ReadModel.RefreshIfNeeded();
            var selection = DistrictSelection.Current;
            if (_version == _module.ReadModel.Snapshot.Version && _selectionVersion == selection.Version) return;
            _version = _module.ReadModel.Snapshot.Version; _selectionVersion = selection.Version;
            Rebuild(selection.OverlayVisible);
        }

        private void Rebuild(bool visible)
        {
            _mesh.Clear(); RenderedSegmentCount = 0;
            if (!visible) return;
            var snapshot = _module.ReadModel.Snapshot;
            var owners = new Dictionary<CellCoord, StableId>();
            for (var i = 0; i < snapshot.Districts.Count; i++)
                for (var c = 0; c < snapshot.Districts[i].Cells.Count; c++) owners[snapshot.Districts[i].Cells[c]] = snapshot.Districts[i].DistrictId;
            var vertices = new List<Vector3>(); var colors = new List<Color32>(); var indices = new List<int>(); var edges = new HashSet<long>();
            for (var i = 0; i < snapshot.Districts.Count; i++)
            {
                var row = snapshot.Districts[i]; var color = ToColor(row.ColorRgba);
                for (var c = 0; c < row.Cells.Count; c++) AddCellEdges(row.Cells[c], row.DistrictId, color, owners, edges, vertices, colors, indices);
            }
            _mesh.SetVertices(vertices); _mesh.SetColors(colors); _mesh.SetIndices(indices, MeshTopology.Lines, 0); RenderedSegmentCount = indices.Count / 2;
        }

        private void AddCellEdges(CellCoord cell, StableId owner, Color32 color, Dictionary<CellCoord, StableId> owners, HashSet<long> edges, List<Vector3> vertices, List<Color32> colors, List<int> indices)
        {
            TryEdge(cell, new CellCoord(cell.X, cell.Y - 1), cell.X, cell.Y, false, owner, color, owners, edges, vertices, colors, indices);
            TryEdge(cell, new CellCoord(cell.X, cell.Y + 1), cell.X, cell.Y + 1, false, owner, color, owners, edges, vertices, colors, indices);
            TryEdge(cell, new CellCoord(cell.X - 1, cell.Y), cell.X, cell.Y, true, owner, color, owners, edges, vertices, colors, indices);
            TryEdge(cell, new CellCoord(cell.X + 1, cell.Y), cell.X + 1, cell.Y, true, owner, color, owners, edges, vertices, colors, indices);
        }

        private void TryEdge(CellCoord cell, CellCoord neighbor, int gridX, int gridY, bool vertical, StableId owner, Color32 color, Dictionary<CellCoord, StableId> owners, HashSet<long> edges, List<Vector3> vertices, List<Color32> colors, List<int> indices)
        {
            if (owners.TryGetValue(neighbor, out var neighborOwner) && neighborOwner == owner) return;
            var key = ((long)gridY << 33) | ((long)gridX << 1) | (vertical ? 1L : 0L);
            if (!edges.Add(key)) return;
            var b = _context.WorldBounds; var y = b.CellSize * 0.08f;
            var a = new Vector3(b.MinX + gridX * b.CellSize, y, b.MinZ + gridY * b.CellSize);
            var z = vertical ? a + new Vector3(0f, 0f, b.CellSize) : a + new Vector3(b.CellSize, 0f, 0f);
            var start = vertices.Count; vertices.Add(a); vertices.Add(z); colors.Add(color); colors.Add(color); indices.Add(start); indices.Add(start + 1);
        }

        private static Color32 ToColor(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); if (_material != null) Destroy(_material); }
    }
}
