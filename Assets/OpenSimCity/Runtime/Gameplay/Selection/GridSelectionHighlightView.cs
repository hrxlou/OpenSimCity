using OpenSimCity.Application.Queries;
using OpenSimCity.Core.Mathematics;
using UnityEngine;

namespace OpenSimCity.Gameplay.Selection
{
    /// <summary>
    /// One reusable view for the currently selected cell. It never instantiates
    /// a new object while the player changes selection.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class GridSelectionHighlightView : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.001f)] private float elevation = 0.025f;
        [SerializeField, Range(0.1f, 1f)] private float inset = 0.88f;

        public bool IsVisible => lineRenderer != null && lineRenderer.enabled;

        public void Configure(Material material)
        {
            lineMaterial = material;
            EnsureRenderer();
            ApplyRendererSettings();
        }

        public void SetSelectedCell(CellCoord cell, WorldBoundsReadModel worldBounds)
        {
            EnsureRenderer();
            var center = GridSelectionMath.CellCenter(cell, worldBounds, elevation);
            var halfExtent = worldBounds.CellSize * inset * 0.5f;
            lineRenderer.positionCount = 5;
            lineRenderer.SetPositions(new[]
            {
                center + new Vector3(-halfExtent, 0f, -halfExtent),
                center + new Vector3(-halfExtent, 0f, halfExtent),
                center + new Vector3(halfExtent, 0f, halfExtent),
                center + new Vector3(halfExtent, 0f, -halfExtent),
                center + new Vector3(-halfExtent, 0f, -halfExtent)
            });
            lineRenderer.enabled = true;
        }

        public void Clear()
        {
            EnsureRenderer();
            lineRenderer.enabled = false;
        }

        private void Awake()
        {
            EnsureRenderer();
            ApplyRendererSettings();
            Clear();
        }

        private void OnValidate()
        {
            elevation = Mathf.Max(0.001f, elevation);
            inset = Mathf.Clamp(inset, 0.1f, 1f);
            if (lineRenderer != null)
                ApplyRendererSettings();
        }

        private void EnsureRenderer()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
        }

        private void ApplyRendererSettings()
        {
            if (lineRenderer == null)
                return;

            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.widthMultiplier = 0.05f;
            lineRenderer.numCornerVertices = 2;
            if (lineMaterial != null)
                lineRenderer.sharedMaterial = lineMaterial;
        }
    }
}
