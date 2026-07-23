using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenSimCity.Presentation
{
    [Serializable]
    public sealed class PlaceholderPrefabBinding
    {
        [SerializeField] private string presentationKey = string.Empty;
        [SerializeField] private GameObject prefab;

        public string PresentationKey => presentationKey ?? string.Empty;
        public GameObject Prefab => prefab;
    }

    /// <summary>
    /// Static presentation-only mapping from stable definition presentation keys to
    /// primitive placeholder prefabs. It never contains runtime city state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlaceholderArtCatalog",
        menuName = "OpenSimCity/Presentation/Placeholder Art Catalog")]
    public sealed class PlaceholderArtCatalog : ScriptableObject
    {
        [SerializeField] private PlaceholderPrefabBinding[] bindings =
            Array.Empty<PlaceholderPrefabBinding>();

        private Dictionary<string, GameObject> _lookup;

        public IReadOnlyList<PlaceholderPrefabBinding> Bindings => bindings;

        public bool TryGetPrefab(string presentationKey, out GameObject prefab)
        {
            EnsureLookup();
            return _lookup.TryGetValue(presentationKey ?? string.Empty, out prefab) && prefab != null;
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.PresentationKey))
                    continue;
                if (!_lookup.ContainsKey(binding.PresentationKey))
                    _lookup.Add(binding.PresentationKey, binding.Prefab);
            }
        }
    }
}
