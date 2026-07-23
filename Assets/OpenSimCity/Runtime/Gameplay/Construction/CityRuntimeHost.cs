using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Gameplay.Camera;
using UnityEngine;

namespace OpenSimCity.Gameplay.Construction
{
    /// <summary>
    /// Scene owner of the P2 <see cref="CityRuntime"/>. It builds the runtime and
    /// its ambient <see cref="CityRuntimeContext"/> on enable, publishes the world
    /// bounds so the camera and overlays agree on cell geometry, and clears the
    /// ambient context on disable so a leaked simulation cannot survive a domain
    /// reload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityRuntimeHost : MonoBehaviour
    {
        [SerializeField] private WorldBoundsReadModelSource worldBoundsSource;
        [SerializeField] private IsometricCameraRig cameraRig;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        private CityRuntimeContext _context;

        public CityRuntimeContext Context => _context;

        public CityRuntime Runtime => _context?.Runtime;

        public WorldBoundsReadModel WorldBounds { get; private set; }

        /// <summary>
        /// Clears the ambient context before the first scene loads. The context is
        /// static, so with domain reload disabled a leak from one play session
        /// would otherwise survive into the next.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetContext()
        {
            CityRuntimeContext.Reset();
        }

        public void ConfigureReferences(
            WorldBoundsReadModelSource boundsSource,
            IsometricCameraRig rig)
        {
            worldBoundsSource = boundsSource;
            cameraRig = rig;
        }

        private void OnEnable()
        {
            ReplaceRuntime(new CityRuntime(CityRuntimeOptions.Default));
        }

        private void OnDisable()
        {
            if (_context != null)
            {
                CityRuntimeContext.ClearCurrent(_context);
                _context.Dispose();
            }
            _context = null;
        }

        /// <summary>
        /// Atomically publishes a fully validated runtime. Presentation systems
        /// observe the new ambient context on their next centralized refresh and
        /// rebuild derived views from stable IDs rather than restoring Unity
        /// instance IDs.
        /// </summary>
        public void ReplaceRuntime(CityRuntime runtime)
        {
            if (runtime == null)
                throw new System.ArgumentNullException(nameof(runtime));

            runtime.RefreshReadModels();
            var previous = _context;
            if (previous != null)
            {
                CityRuntimeContext.ClearCurrent(previous);
                previous.Dispose();
            }

            _context = new CityRuntimeContext(runtime);
            CityRuntimeContext.SetCurrent(_context);

            // Built from the runtime's cell dimensions rather than WorldState so
            // Gameplay never needs a reference to the Simulation assembly.
            WorldBounds = new WorldBoundsReadModel(runtime.Width, runtime.Height, cellSize);
            _context.SetWorldBounds(WorldBounds);
            if (worldBoundsSource != null)
            {
                worldBoundsSource.SetConfiguredDimensions(runtime.Width, runtime.Height, cellSize);
                worldBoundsSource.SetReadModel(WorldBounds);
            }

            if (cameraRig != null)
                cameraRig.SetWorldBounds(WorldBounds);
        }

        private void Update()
        {
            // The host owns presentation time only. CityRuntime's application
            // clock turns it into deterministic fixed ticks and the ordered
            // monthly scheduler remains Unity-independent.
            _context?.Runtime.AdvanceSimulation(Time.unscaledDeltaTime);
        }
    }
}
