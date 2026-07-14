using UnityEngine;
using OpenSimCity.Application.Queries;

namespace OpenSimCity.Gameplay.Camera
{
    /// <summary>
    /// Scene-side source for the world-bounds read model.
    /// Application bootstrap can replace the configured value at runtime without
    /// giving the camera access to WorldState or its cell arrays.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBoundsReadModelSource : MonoBehaviour, IWorldBoundsQuery
    {
        [SerializeField, Min(1)] private int width = 64;
        [SerializeField, Min(1)] private int height = 64;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        private WorldBoundsReadModel _runtimeReadModel;
        private bool _hasRuntimeReadModel;

        public int ConfiguredWidth => width;

        public int ConfiguredHeight => height;

        public float ConfiguredCellSize => cellSize;

        public WorldBoundsReadModel ReadWorldBounds()
        {
            return _hasRuntimeReadModel
                ? _runtimeReadModel
                : new WorldBoundsReadModel(width, height, cellSize);
        }

        /// <summary>
        /// Replaces the source with an application-owned read model for this run.
        /// The model is intentionally not copied into serialized scene state.
        /// </summary>
        public void SetReadModel(WorldBoundsReadModel readModel)
        {
            _runtimeReadModel = readModel;
            _hasRuntimeReadModel = true;
        }

        public void ClearRuntimeReadModel()
        {
            _runtimeReadModel = default;
            _hasRuntimeReadModel = false;
        }

        public void SetConfiguredDimensions(int newWidth, int newHeight, float newCellSize)
        {
            var readModel = new WorldBoundsReadModel(newWidth, newHeight, newCellSize);
            width = readModel.Width;
            height = readModel.Height;
            cellSize = readModel.CellSize;
            _runtimeReadModel = default;
            _hasRuntimeReadModel = false;
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.01f, cellSize);
        }
    }
}
