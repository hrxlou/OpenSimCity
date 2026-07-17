using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using UnityEngine;

namespace OpenSimCity.Presentation.Utilities
{
    /// <summary>Central, pooled primitive placeholder renderer for P3 power producers.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerProducerViewRenderer : MonoBehaviour
    {
        private readonly Dictionary<StableId, GameObject> _active = new Dictionary<StableId, GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private CityRuntimeContext _context;
        private int _version = -1;
        public int ActiveViewCount => _active.Count;

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
                { view = GetView(); _active.Add(producer.Id, view); }
                var bounds = _context.WorldBounds;
                var size = bounds.CellSize;
                view.transform.localPosition = new Vector3(bounds.MinX + (producer.Cell.X + 0.5f) * size,
                    size * 0.35f, bounds.MinZ + (producer.Cell.Y + 0.5f) * size);
                view.transform.localScale = new Vector3(size * 0.75f, size * 0.7f, size * 0.75f);
                view.GetComponent<Renderer>().sharedMaterial = producer.IsConnected ? Materials.Connected : Materials.Disconnected;
            }
            var remove = new List<StableId>();
            foreach (var pair in _active) if (!keep.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var id in remove)
            { var view = _active[id]; _active.Remove(id); view.SetActive(false); _pool.Push(view); }
        }

        private GameObject GetView()
        {
            if (_pool.Count > 0) { var pooled = _pool.Pop(); pooled.SetActive(true); return pooled; }
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = "PowerPlantPlaceholder"; view.transform.SetParent(transform, false);
            var collider = view.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return view;
        }

        private static class Materials
        {
            private static Material _connected, _disconnected;
            public static Material Connected => _connected != null ? _connected : (_connected = Create(new Color(0.69f, 0.58f, 0.96f)));
            public static Material Disconnected => _disconnected != null ? _disconnected : (_disconnected = Create(new Color(0.94f, 0.44f, 0.44f)));
            private static Material Create(Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader) { color = color, name = "OpenSimCity Power Placeholder" };
                return material;
            }
        }
    }
}
