using UnityEngine;

namespace OpenSimCity.Presentation
{
    public enum BenchmarkArtMode : byte
    {
        Placeholder = 0,
        ProductionLike = 1
    }

    /// <summary>
    /// Central Benchmark-scene fixture that repeats either the P3 placeholder or
    /// the P3-08 representative prefab set. It does not read or mutate city state.
    /// </summary>
    public sealed class RepresentativeArtBenchmarkFixture : MonoBehaviour
    {
        [SerializeField] private RepresentativeArtCatalog representativeCatalog;
        [SerializeField] private PlaceholderArtCatalog placeholderCatalog;
        [SerializeField] private BenchmarkArtMode initialMode = BenchmarkArtMode.ProductionLike;
        [SerializeField, Min(1)] private int repeatsPerAsset = 4;
        [SerializeField, Min(1f)] private float assetSpacingMeters = 32f;
        [SerializeField, Min(1f)] private float repeatSpacingMeters = 36f;

        private Transform _instancesRoot;

        public RepresentativeArtCatalog RepresentativeCatalog => representativeCatalog;
        public PlaceholderArtCatalog PlaceholderCatalog => placeholderCatalog;
        public BenchmarkArtMode CurrentMode { get; private set; }
        public int RepeatsPerAsset => repeatsPerAsset;
        public Transform InstancesRoot => _instancesRoot;
        public int InstanceCount => _instancesRoot == null ? 0 : _instancesRoot.childCount;

        private void Awake()
        {
            Rebuild(initialMode);
        }

        public void Rebuild(BenchmarkArtMode mode)
        {
            ClearInstances();
            CurrentMode = mode;
            if (representativeCatalog == null)
                return;

            var root = new GameObject("RepresentativeArtInstances");
            root.transform.SetParent(transform, false);
            _instancesRoot = root.transform;

            var bindings = representativeCatalog.Bindings;
            var assetOffset = (bindings.Count - 1) * 0.5f;
            var repeatOffset = (repeatsPerAsset - 1) * 0.5f;
            for (var assetIndex = 0; assetIndex < bindings.Count; assetIndex++)
            {
                var binding = bindings[assetIndex];
                var prefab = ResolvePrefab(binding, mode);
                if (prefab == null)
                    continue;

                for (var repeatIndex = 0; repeatIndex < repeatsPerAsset; repeatIndex++)
                {
                    var instance = Instantiate(prefab, _instancesRoot);
                    instance.name = $"{binding.Definition.PrefabKey}_{repeatIndex:D2}";
                    instance.transform.localPosition = new Vector3(
                        (assetIndex - assetOffset) * assetSpacingMeters,
                        0f,
                        (repeatIndex - repeatOffset) * repeatSpacingMeters);
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                }
            }
        }

        private GameObject ResolvePrefab(
            RepresentativeArtBinding binding,
            BenchmarkArtMode mode)
        {
            if (binding == null)
                return null;
            if (mode == BenchmarkArtMode.ProductionLike)
                return binding.Prefab;
            if (placeholderCatalog == null)
                return null;
            return placeholderCatalog.TryGetPrefab(
                binding.PlaceholderPresentationKey,
                out var placeholder)
                ? placeholder
                : null;
        }

        private void ClearInstances()
        {
            if (_instancesRoot == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(_instancesRoot.gameObject);
            else
                DestroyImmediate(_instancesRoot.gameObject);
            _instancesRoot = null;
        }
    }
}
