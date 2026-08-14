using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using UnityEngine;

namespace OpenSimCity.Presentation.Roads
{
    /// <summary>
    /// Central pooled placeholder renderer for occupied road cells. The first
    /// synchronization rebuilds from the complete read model; later graph
    /// versions consume only the exact dirty-cell set published by Application.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadViewRenderer : MonoBehaviour
    {
        public const string RoadPresentationKey = "road.street.placeholder";

        [SerializeField] private PlaceholderArtCatalog catalog;

        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private CityRuntimeContext _context;
        private int _version = -1;

        public int ActiveViewCount => _active.Count;
        public int PooledViewCount => _pool.Count;

        /// <summary>
        /// Number of read-model cells visited by the most recent synchronization.
        /// This is diagnostic evidence that steady-state road edits stay scoped to
        /// the graph's exact dirty-cell set instead of the complete map.
        /// </summary>
        public int LastSynchronizationCellCount { get; private set; }

        public void Configure(PlaceholderArtCatalog value)
        {
            catalog = value;
            RecycleAll();
            _version = -1;
        }

        private void OnDisable()
        {
            RecycleAll();
            _context = null;
            _version = -1;
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                RecycleAll();
                _context = current;
                _version = -1;
            }

            if (_context == null || !_context.HasWorldBounds || catalog == null)
                return;

            var model = _context.Runtime.RoadAccessibility;
            var graphVersion = model.GraphVersion;
            if (graphVersion == _version)
                return;

            if (_version < 0)
                SynchronizeAll(model);
            else
                SynchronizeDirty(model);

            _version = graphVersion;
        }

        private void SynchronizeAll(RoadAccessibilityReadModel model)
        {
            RecycleAll();
            var cells = model.Cells;
            LastSynchronizationCellCount = cells.Count;
            for (var index = 0; index < cells.Count; index++)
            {
                var row = cells[index];
                if (row.HasRoad)
                    UpdateOrCreate(index, row);
            }
        }

        private void SynchronizeDirty(RoadAccessibilityReadModel model)
        {
            var dirtyCells = model.DirtyCells;
            LastSynchronizationCellCount = dirtyCells.Count;
            for (var index = 0; index < dirtyCells.Count; index++)
            {
                var cell = dirtyCells[index];
                if (!model.TryGet(cell, out var row))
                    continue;

                var key = cell.Y * model.Width + cell.X;
                if (row.HasRoad)
                    UpdateOrCreate(key, row);
                else
                    Recycle(key);
            }
        }

        private void UpdateOrCreate(int key, RoadAccessibilityCellReadModel row)
        {
            if (!_active.TryGetValue(key, out var view))
            {
                view = Get();
                if (view == null)
                    return;
                _active.Add(key, view);
            }

            var bounds = _context.WorldBounds;
            var size = bounds.CellSize;
            view.transform.localPosition = new Vector3(
                bounds.MinX + (row.Cell.X + 0.5f) * size,
                0f,
                bounds.MinZ + (row.Cell.Y + 0.5f) * size);
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = new Vector3(size, size, size);
        }

        private void Recycle(int key)
        {
            if (!_active.TryGetValue(key, out var view))
                return;

            _active.Remove(key);
            view.SetActive(false);
            _pool.Push(view);
        }

        private GameObject Get()
        {
            if (_pool.Count > 0)
            {
                var reused = _pool.Pop();
                reused.SetActive(true);
                return reused;
            }

            if (!catalog.TryGetPrefab(RoadPresentationKey, out var prefab))
                return null;
            var instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            return instance;
        }

        private void RecycleAll()
        {
            foreach (var pair in _active)
            {
                pair.Value.SetActive(false);
                _pool.Push(pair.Value);
            }
            _active.Clear();
            LastSynchronizationCellCount = 0;
        }
    }
}
