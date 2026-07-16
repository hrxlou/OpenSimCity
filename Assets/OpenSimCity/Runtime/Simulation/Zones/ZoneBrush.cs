using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Zones
{
    public enum ZoneBrushAxis
    {
        Point,
        Horizontal,
        Vertical
    }

    public enum ZoneBrushShape
    {
        Cells,
        Path,
        Area
    }

    /// <summary>
    /// Immutable brush cell set. All constructors normalize to unique
    /// row-major cells so preview and commit use the same ordered input.
    /// </summary>
    [Serializable]
    public sealed class ZoneBrush
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        private ZoneBrush(ZoneBrushShape shape, IEnumerable<CellCoord> cells)
        {
            Shape = shape;
            _cells = ZoneBrushCalculator.Normalize(cells);
        }

        public ZoneBrushShape Shape { get; }

        public IReadOnlyList<CellCoord> Cells => _cells;

        public IReadOnlyList<CellCoord> Coordinates => _cells;

        public int Count => _cells.Count;

        public bool IsEmpty => _cells.Count == 0;

        public static ZoneBrush FromCells(IEnumerable<CellCoord> cells)
        {
            return new ZoneBrush(ZoneBrushShape.Cells, cells);
        }

        public static ZoneBrush Path(CellCoord start, CellCoord end)
        {
            return new ZoneBrush(ZoneBrushShape.Path, ZoneBrushPath.Calculate(start, end).Cells);
        }

        public static ZoneBrush Area(CellCoord start, CellCoord end)
        {
            return new ZoneBrush(ZoneBrushShape.Area, ZoneBrushArea.Calculate(start, end).Cells);
        }
    }

    /// <summary>
    /// Dominant-axis brush path. A diagonal tie chooses horizontal to keep
    /// drag-to-command conversion platform independent.
    /// </summary>
    [Serializable]
    public sealed class ZoneBrushPath
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        private ZoneBrushPath(
            CellCoord requestedStart,
            CellCoord requestedEnd,
            CellCoord start,
            CellCoord end,
            ZoneBrushAxis axis,
            IReadOnlyList<CellCoord> cells)
        {
            RequestedStart = requestedStart;
            RequestedEnd = requestedEnd;
            Start = start;
            End = end;
            Axis = axis;
            _cells = cells;
        }

        public CellCoord RequestedStart { get; }

        public CellCoord RequestedEnd { get; }

        public CellCoord Start { get; }

        public CellCoord End { get; }

        public ZoneBrushAxis Axis { get; }

        public ZoneBrushAxis Direction => Axis;

        public IReadOnlyList<CellCoord> Cells => _cells;

        public int Count => _cells.Count;

        public static ZoneBrushPath Calculate(CellCoord start, CellCoord end)
        {
            var deltaX = Math.Abs((long)end.X - start.X);
            var deltaY = Math.Abs((long)end.Y - start.Y);
            var horizontal = deltaX >= deltaY;
            var distance = horizontal ? deltaX : deltaY;
            if (distance >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(end),
                    "A zone brush path is too long for a managed cell list.");
            }

            var count = checked((int)distance + 1);
            var axis = horizontal
                ? (distance == 0 ? ZoneBrushAxis.Point : ZoneBrushAxis.Horizontal)
                : ZoneBrushAxis.Vertical;
            var step = horizontal
                ? (end.X >= start.X ? 1 : -1)
                : (end.Y >= start.Y ? 1 : -1);
            var pathCells = new List<CellCoord>(count);
            for (var index = 0; index < count; index++)
            {
                var offset = (long)index * step;
                var coordinate = horizontal
                    ? new CellCoord(checked((int)(start.X + offset)), start.Y)
                    : new CellCoord(start.X, checked((int)(start.Y + offset)));
                pathCells.Add(coordinate);
            }

            var normalized = ZoneBrushCalculator.Normalize(pathCells);
            var snappedEnd = pathCells[pathCells.Count - 1];
            return new ZoneBrushPath(
                start,
                end,
                start,
                snappedEnd,
                axis,
                normalized);
        }
    }

    public static class ZoneBrushPathCalculator
    {
        public static ZoneBrushPath Calculate(CellCoord start, CellCoord end)
        {
            return ZoneBrushPath.Calculate(start, end);
        }
    }

    /// <summary>
    /// Inclusive rectangular brush area, normalized to row-major cell order.
    /// </summary>
    [Serializable]
    public sealed class ZoneBrushArea
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        private ZoneBrushArea(
            CellCoord requestedStart,
            CellCoord requestedEnd,
            CellCoord minimum,
            CellCoord maximum,
            IReadOnlyList<CellCoord> cells)
        {
            RequestedStart = requestedStart;
            RequestedEnd = requestedEnd;
            Minimum = minimum;
            Maximum = maximum;
            _cells = cells;
        }

        public CellCoord RequestedStart { get; }

        public CellCoord RequestedEnd { get; }

        public CellCoord Minimum { get; }

        public CellCoord Maximum { get; }

        public IReadOnlyList<CellCoord> Cells => _cells;

        public int Count => _cells.Count;

        public static ZoneBrushArea Calculate(CellCoord start, CellCoord end)
        {
            var minimumX = Math.Min((long)start.X, end.X);
            var maximumX = Math.Max((long)start.X, end.X);
            var minimumY = Math.Min((long)start.Y, end.Y);
            var maximumY = Math.Max((long)start.Y, end.Y);
            var width = maximumX - minimumX + 1L;
            var height = maximumY - minimumY + 1L;
            if (width <= 0 || height <= 0 || width > int.MaxValue / height)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(end),
                    "A zone brush area is too large for a managed cell list.");
            }

            var cellCount = width * height;

            var cells = new List<CellCoord>((int)cellCount);
            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                    cells.Add(new CellCoord(checked((int)x), checked((int)y)));
            }

            return new ZoneBrushArea(
                start,
                end,
                new CellCoord(checked((int)minimumX), checked((int)minimumY)),
                new CellCoord(checked((int)maximumX), checked((int)maximumY)),
                ZoneBrushCalculator.Normalize(cells));
        }
    }

    public static class ZoneBrushAreaCalculator
    {
        public static ZoneBrushArea Calculate(CellCoord start, CellCoord end)
        {
            return ZoneBrushArea.Calculate(start, end);
        }
    }

    public static class ZoneBrushCalculator
    {
        public static ZoneBrushPath CalculatePath(CellCoord start, CellCoord end)
        {
            return ZoneBrushPath.Calculate(start, end);
        }

        public static ZoneBrushArea CalculateArea(CellCoord start, CellCoord end)
        {
            return ZoneBrushArea.Calculate(start, end);
        }

        public static IReadOnlyList<CellCoord> Normalize(IEnumerable<CellCoord> cells)
        {
            var unique = new HashSet<CellCoord>();
            var normalized = new List<CellCoord>();
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    if (unique.Add(cell))
                        normalized.Add(cell);
                }
            }

            normalized.Sort((left, right) => left.CompareTo(right));
            return normalized.AsReadOnly();
        }
    }
}
