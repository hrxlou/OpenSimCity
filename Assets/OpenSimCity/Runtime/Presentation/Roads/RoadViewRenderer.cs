using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using UnityEngine;

namespace OpenSimCity.Presentation.Roads
{
    /// <summary>Central pooled placeholder renderer for occupied road cells.</summary>
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

        public void Configure(PlaceholderArtCatalog value)
        {
            catalog = value;
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
            if (model.GraphVersion == _version)
                return;
            _version = model.GraphVersion;
            Synchronize(model);
        }

        private void Synchronize(RoadAccessibilityReadModel model)
        {
            var keep = new HashSet<int>();
            var cells = model.Cells;
            for (var index = 0; index < cells.Count; index++)
            {
                var row = cells[index];
                if (!row.HasRoad)
                    continue;

                keep.Add(index);
                if (!_active.TryGetValue(index, out var view))
                {
                    view = Get();
                    if (view == null)
                        continue;
                    _active.Add(index, view);
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

            var removed = new List<int>();
            foreach (var pair in _active)
            {
                if (!keep.Contains(pair.Key))
                    removed.Add(pair.Key);
            }

            for (var index = 0; index < removed.Count; index++)
            {
                var key = removed[index];
                var view = _active[key];
                _active.Remove(key);
                view.SetActive(false);
                _pool.Push(view);
            }
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
        }
    }
}
