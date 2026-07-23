using System;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using UnityEngine;

namespace OpenSimCity.Presentation.Buildings
{
    /// <summary>
    /// Single scheduler for all construction and building views. Views have no
    /// per-building behaviours and are recycled in presentation-key-specific pools.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingViewRenderer : MonoBehaviour
    {
        [SerializeField] private PlaceholderArtCatalog catalog;

        private readonly Dictionary<StableId, ActiveView> _active =
            new Dictionary<StableId, ActiveView>();
        private readonly Dictionary<string, Stack<GameObject>> _pools =
            new Dictionary<string, Stack<GameObject>>(StringComparer.Ordinal);
        private CityRuntimeContext _context;
        private int _version = -1;

        public int ActiveViewCount => _active.Count;
        public int PooledViewCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _pools)
                    count += pair.Value.Count;
                return count;
            }
        }

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

            var model = _context.Runtime.BuildingViews;
            if (model.Version == _version)
                return;

            _version = model.Version;
            Synchronize(model.Views);
        }

        private void Synchronize(IReadOnlyList<BuildingViewData> rows)
        {
            var keep = new HashSet<StableId>();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                keep.Add(row.Id);
                if (!_active.TryGetValue(row.Id, out var active) ||
                    !string.Equals(active.PresentationKey, row.PresentationKey, StringComparison.Ordinal))
                {
                    if (active.GameObject != null)
                        Recycle(active);
                    active = new ActiveView(row.PresentationKey, Get(row.PresentationKey));
                    _active[row.Id] = active;
                }

                Position(active.GameObject, row);
            }

            var removed = new List<StableId>();
            foreach (var pair in _active)
            {
                if (!keep.Contains(pair.Key))
                    removed.Add(pair.Key);
            }

            for (var index = 0; index < removed.Count; index++)
            {
                var id = removed[index];
                Recycle(_active[id]);
                _active.Remove(id);
            }
        }

        private void Position(GameObject view, BuildingViewData row)
        {
            if (view == null)
                return;

            var bounds = _context.WorldBounds;
            var size = bounds.CellSize;
            view.transform.localPosition = new Vector3(
                bounds.MinX + (row.Anchor.X + row.FootprintWidth * 0.5f) * size,
                0f,
                bounds.MinZ + (row.Anchor.Y + row.FootprintHeight * 0.5f) * size);
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = new Vector3(
                row.FootprintWidth * size,
                size,
                row.FootprintHeight * size);
        }

        private GameObject Get(string presentationKey)
        {
            if (_pools.TryGetValue(presentationKey, out var pool) && pool.Count > 0)
            {
                var reused = pool.Pop();
                reused.SetActive(true);
                return reused;
            }

            if (!catalog.TryGetPrefab(presentationKey, out var prefab))
                return null;
            var instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            return instance;
        }

        private void Recycle(ActiveView active)
        {
            if (active.GameObject == null)
                return;
            active.GameObject.SetActive(false);
            if (!_pools.TryGetValue(active.PresentationKey, out var pool))
            {
                pool = new Stack<GameObject>();
                _pools.Add(active.PresentationKey, pool);
            }

            pool.Push(active.GameObject);
        }

        private void RecycleAll()
        {
            foreach (var pair in _active)
                Recycle(pair.Value);
            _active.Clear();
        }

        private readonly struct ActiveView
        {
            public ActiveView(string presentationKey, GameObject gameObject)
            {
                PresentationKey = presentationKey;
                GameObject = gameObject;
            }

            public string PresentationKey { get; }
            public GameObject GameObject { get; }
        }
    }
}
