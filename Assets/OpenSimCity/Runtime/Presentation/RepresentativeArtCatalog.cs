using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenSimCity.Presentation
{
    public enum RepresentativeArtRole : byte
    {
        BuildingSmall = 0,
        BuildingMedium = 1,
        BuildingLarge = 2,
        Road = 3,
        Facade = 4,
        Prop = 5
    }

    [Serializable]
    public sealed class RepresentativeArtBinding
    {
        [SerializeField] private RepresentativeArtDefinition definition;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string placeholderPresentationKey = string.Empty;

        public RepresentativeArtDefinition Definition => definition;
        public GameObject Prefab => prefab;
        public string PlaceholderPresentationKey => placeholderPresentationKey ?? string.Empty;
    }

    /// <summary>
    /// Benchmark-only catalog for the pinned production-like representative slice.
    /// It stores static definitions and prefab references, never simulation state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RepresentativeArtCatalog",
        menuName = "OpenSimCity/Presentation/Representative Art Catalog")]
    public sealed class RepresentativeArtCatalog : ScriptableObject
    {
        [SerializeField] private RepresentativeArtBinding[] bindings =
            Array.Empty<RepresentativeArtBinding>();

        private Dictionary<string, RepresentativeArtBinding> _byPrefabKey;

        public IReadOnlyList<RepresentativeArtBinding> Bindings => bindings;

        public bool TryGetBinding(string prefabKey, out RepresentativeArtBinding binding)
        {
            EnsureLookup();
            return _byPrefabKey.TryGetValue(prefabKey ?? string.Empty, out binding) &&
                   binding != null;
        }

        private void OnEnable()
        {
            _byPrefabKey = null;
        }

        private void EnsureLookup()
        {
            if (_byPrefabKey != null)
                return;

            _byPrefabKey =
                new Dictionary<string, RepresentativeArtBinding>(StringComparer.Ordinal);
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var key = binding?.Definition?.PrefabKey;
                if (string.IsNullOrWhiteSpace(key) || _byPrefabKey.ContainsKey(key))
                    continue;
                _byPrefabKey.Add(key, binding);
            }
        }
    }
}
