using System;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.Services;
using OpenSimCity.Application.World;
using OpenSimCity.Presentation.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.Services
{
    [DisallowMultipleComponent]
    public sealed class ServiceResponseView : MonoBehaviour
    {
        public const string RuntimeObjectName = "ServiceResponseViews";
        [SerializeField, Min(0.1f)] private float routeLoopSeconds = 8f;

        private CityRuntimeContext _context;
        private ServiceResponseProjection _projection;
        private VehicleViewScheduler _scheduler;
        private Material _material;
        private int _observedVersion = -1;
        private int _settingsVersion = -1;

        public int ActiveCount => _scheduler?.ActiveCount ?? 0;
        public int PooledCount => _scheduler?.PooledCount ?? 0;
        public ServiceResponseProjection Projection => _projection;

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                _context = current;
                _projection = null;
                _observedVersion = -1;
                _settingsVersion = -1;
                _scheduler?.ReleaseAll();
            }
            if (_context == null || !_context.HasWorldBounds) return;
            EnsureScheduler();
            if (_projection == null)
                _projection = new ServiceResponseProjection(PublicServiceRuntimeRegistry.GetOrCreate(_context.Runtime));

            var settings = ServiceResponseSettings.Current;
            if (_settingsVersion != settings.Version)
            {
                _settingsVersion = settings.Version;
                _projection.SetMaximumSamples(settings.MaximumSamples);
                _observedVersion = -1;
            }

            _projection.RefreshIfNeeded();
            if (_observedVersion != _projection.Version)
            {
                _observedVersion = _projection.Version;
                var source = _projection.Samples;
                var rows = new VehicleSampleData[source.Count];
                for (var index = 0; index < rows.Length; index++)
                {
                    var item = source[index];
                    rows[index] = new VehicleSampleData(
                        item.Id,
                        TrafficFlowKind.Goods,
                        item.RepresentedDemand,
                        item.PhaseOffsetPermille,
                        item.Path);
                }
                _scheduler.ApplySamples(Array.AsReadOnly(rows));
            }
            _scheduler.UpdatePositions(
                _context.WorldBounds,
                Time.unscaledTimeAsDouble,
                Math.Max(0.1f, routeLoopSeconds));
        }

        private void EnsureScheduler()
        {
            if (_scheduler != null) return;
            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default") ??
                             Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No runtime shader for service response view.");
                _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            _scheduler = new VehicleViewScheduler(transform, null, _material);
        }

        private void OnDestroy()
        {
            _scheduler?.Dispose();
            if (_material != null) Destroy(_material);
        }
    }

    public static class ServiceResponseViewBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ServiceResponseView>() != null) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            var root = new GameObject(ServiceResponseView.RuntimeObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<ServiceResponseView>();
        }
    }
}
