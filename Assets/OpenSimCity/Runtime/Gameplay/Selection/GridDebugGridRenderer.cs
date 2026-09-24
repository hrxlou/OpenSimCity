using OpenSimCity.Application.Queries;
using OpenSimCity.Gameplay.Camera;
using UnityEngine;

namespace OpenSimCity.Gameplay.Selection
{
    /// <summary>
    /// Renders the debug grid as one mesh. It reads dimensions through the
    /// presentation-safe bounds source and has no simulation-state access.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class GridDebugGridRenderer : MonoBehaviour
    {
        [SerializeField] private WorldBoundsReadModelSource worldBoundsSource;
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.001f)] private float elevation = 0.01f;
        [SerializeField] private bool showGrid;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        public void Configure(WorldBoundsReadModelSource boundsSource, Material material)
        {
            worldBoundsSource = boundsSource;
            lineMaterial = material;
            Rebuild();
        }

        public bool IsVisible => showGrid;

        public void SetVisible(bool value)
        {
            showGrid = value;
            EnsureComponents();
            _meshRenderer.enabled = value;
        }

        public void Rebuild()
        {
            if (worldBoundsSource == null)
                return;

            EnsureComponents();
            var bounds = worldBoundsSource.ReadWorldBounds();
            var lineCount = bounds.Width + bounds.Height + 2;
            var vertices = new Vector3[lineCount * 2];
            var indices = new int[vertices.Length];
            var index = 0;

            for (var x = 0; x <= bounds.Width; x++)
            {
                var worldX = bounds.MinX + x * bounds.CellSize;
                vertices[index] = new Vector3(worldX, elevation, bounds.MinZ);
                vertices[index + 1] = new Vector3(worldX, elevation, bounds.MaxZ);
                indices[index] = index;
                indices[index + 1] = index + 1;
                index += 2;
            }

            for (var y = 0; y <= bounds.Height; y++)
            {
                var worldZ = bounds.MinZ + y * bounds.CellSize;
                vertices[index] = new Vector3(bounds.MinX, elevation, worldZ);
                vertices[index + 1] = new Vector3(bounds.MaxX, elevation, worldZ);
                indices[index] = index;
                indices[index + 1] = index + 1;
                index += 2;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.SetIndices(indices, MeshTopology.Lines, 0, calculateBounds: true);
            _meshFilter.sharedMesh = _mesh;
            if (lineMaterial != null)
                _meshRenderer.sharedMaterial = lineMaterial;
            _meshRenderer.enabled = showGrid;
        }

        private void Awake() => EnsureComponents();

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            elevation = Mathf.Max(0.001f, elevation);
            if (isActiveAndEnabled)
                Rebuild();
        }

        private void OnDestroy()
        {
            if (_mesh == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(_mesh);
            else
                DestroyImmediate(_mesh);
        }

        private void EnsureComponents()
        {
            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "OpenSimCity Debug Grid" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
        }
    }
}
