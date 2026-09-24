using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Presentation;
using UnityEngine;

namespace OpenSimCity.Presentation.Utilities
{
    /// <summary>Central, pooled primitive placeholder renderer for P3 power producers.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerProducerViewRenderer : MonoBehaviour
    {
        public const string DefaultPresentationKey = "utility.power.plant.placeholder";

        [SerializeField] private PlaceholderArtCatalog catalog;
        private readonly Dictionary<StableId, GameObject> _active = new Dictionary<StableId, GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private CityRuntimeContext _context;
        private int _version = -1;
        public int ActiveViewCount => _active.Count;

        public void Configure(PlaceholderArtCatalog value)
        {
            catalog = value;
            _version = -1;
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context)) { _context = current; _version = -1; }
            if (_context == null || !_context.HasWorldBounds) return;
            var model = _context.Runtime.PowerModel;
            if (model.Version == _version) return;
            _version = model.Version; Synchronize(model);
        }

        private void Synchronize(PowerReadModel model)
        {
            var keep = new HashSet<StableId>();
            foreach (var producer in model.ProducerViews)
            {
                keep.Add(producer.Id);
                if (!_active.TryGetValue(producer.Id, out var view))
                { view = GetView(producer.PresentationKey); if (view == null) continue; _active.Add(producer.Id, view); }
                var bounds = _context.WorldBounds;
                var size = bounds.CellSize;
                view.transform.localPosition = new Vector3(bounds.MinX + (producer.Cell.X + 0.5f) * size,
                    0f, bounds.MinZ + (producer.Cell.Y + 0.5f) * size);
                view.transform.localScale = Vector3.one * size;
                var renderers = view.GetComponentsInChildren<Renderer>();
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    renderers[rendererIndex].enabled = true;
            }
            var remove = new List<StableId>();
            foreach (var pair in _active) if (!keep.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var id in remove)
            { var view = _active[id]; _active.Remove(id); view.SetActive(false); _pool.Push(view); }
        }

        private GameObject GetView(string presentationKey)
        {
            if (_pool.Count > 0) { var pooled = _pool.Pop(); pooled.SetActive(true); return pooled; }
            var key = string.IsNullOrWhiteSpace(presentationKey) ? DefaultPresentationKey : presentationKey;
            if (catalog == null || !catalog.TryGetPrefab(key, out var prefab))
                return null;
            var view = Instantiate(prefab, transform);
            view.name = prefab.name;
            return view;
        }
    }
}
