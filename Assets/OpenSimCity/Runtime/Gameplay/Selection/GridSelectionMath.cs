using System;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core.Mathematics;
using UnityEngine;

namespace OpenSimCity.Gameplay.Selection
{
    /// <summary>
    /// Pure world-plane to cell conversion. World edges are half-open so a point
    /// exactly at the far edge is outside the city rather than selecting a phantom cell.
    /// </summary>
    public static class GridSelectionMath
    {
        public static bool TryWorldToCell(
            Vector3 worldPosition,
            WorldBoundsReadModel worldBounds,
            out CellCoord cell)
        {
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.z))
                throw new ArgumentOutOfRangeException(nameof(worldPosition), "World position must be finite.");

            var relativeX = worldPosition.x - worldBounds.MinX;
            var relativeZ = worldPosition.z - worldBounds.MinZ;
            if (relativeX < 0f || relativeZ < 0f ||
                relativeX >= worldBounds.WorldWidth || relativeZ >= worldBounds.WorldHeight)
            {
                cell = default;
                return false;
            }

            var x = Mathf.FloorToInt(relativeX / worldBounds.CellSize);
            var y = Mathf.FloorToInt(relativeZ / worldBounds.CellSize);
            if (x < 0 || x >= worldBounds.Width || y < 0 || y >= worldBounds.Height)
            {
                cell = default;
                return false;
            }

            cell = new CellCoord(x, y);
            return true;
        }

        public static Vector3 CellCenter(CellCoord cell, WorldBoundsReadModel worldBounds, float elevation = 0f)
        {
            if (cell.X < 0 || cell.X >= worldBounds.Width || cell.Y < 0 || cell.Y >= worldBounds.Height)
                throw new ArgumentOutOfRangeException(nameof(cell), "Cell must be inside the supplied world bounds.");
            if (!IsFinite(elevation))
                throw new ArgumentOutOfRangeException(nameof(elevation), "Elevation must be finite.");

            return new Vector3(
                worldBounds.MinX + (cell.X + 0.5f) * worldBounds.CellSize,
                elevation,
                worldBounds.MinZ + (cell.Y + 0.5f) * worldBounds.CellSize);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
