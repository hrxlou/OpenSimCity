using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.Roads
{
    public enum RoadPathAxis
    {
        Point,
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Immutable row-major-independent path produced by a straight road drag.
    /// Diagonal drags snap to their dominant axis; ties choose horizontal so the
    /// same endpoints always produce the same command on every platform.
    /// </summary>
    public sealed class RoadPath
    {
        private readonly IReadOnlyList<CellCoord> _cells;

        private RoadPath(
            CellCoord requestedStart,
            CellCoord requestedEnd,
            RoadPathAxis axis,
            IReadOnlyList<CellCoord> cells)
        {
            RequestedStart = requestedStart;
            RequestedEnd = requestedEnd;
            Start = cells[0];
            End = cells[cells.Count - 1];
            Axis = axis;
            _cells = cells;
        }

        public CellCoord RequestedStart { get; }

        public CellCoord RequestedEnd { get; }

        public CellCoord Start { get; }

        public CellCoord End { get; }

        public RoadPathAxis Axis { get; }

        public RoadPathAxis Direction => Axis;

        public IReadOnlyList<CellCoord> Cells => _cells;

        public int Count => _cells.Count;

        public static RoadPath Calculate(CellCoord start, CellCoord end)
        {
            var deltaX = Math.Abs((long)end.X - start.X);
            var deltaY = Math.Abs((long)end.Y - start.Y);
            var horizontal = deltaX >= deltaY;
            var distance = horizontal ? deltaX : deltaY;
            if (distance >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(end),
                    "A straight road path is too long for a managed cell list.");
            }

            var count = checked((int)distance + 1);
            var cells = new List<CellCoord>(count);
            var axis = horizontal
                ? (distance == 0 ? RoadPathAxis.Point : RoadPathAxis.Horizontal)
                : RoadPathAxis.Vertical;
            var step = horizontal
                ? (end.X >= start.X ? 1 : -1)
                : (end.Y >= start.Y ? 1 : -1);

            for (var index = 0; index < count; index++)
            {
                var offset = (long)index * step;
                var coordinate = horizontal
                    ? new CellCoord(checked((int)(start.X + offset)), start.Y)
                    : new CellCoord(start.X, checked((int)(start.Y + offset)));
                cells.Add(coordinate);
            }

            return new RoadPath(
                start,
                end,
                axis,
                cells.AsReadOnly());
        }

        public static bool IsStraight(IReadOnlyList<CellCoord> cells)
        {
            return TryGetAxis(cells, out _);
        }

        /// <summary>
        /// Checks both axis alignment and unit-cell continuity. The input order
        /// is preserved, so a reverse drag remains a valid reverse path.
        /// </summary>
        public static bool TryGetAxis(IReadOnlyList<CellCoord> cells, out RoadPathAxis axis)
        {
            axis = RoadPathAxis.Point;
            if (cells == null || cells.Count == 0)
                return false;
            if (cells.Count == 1)
                return true;

            var first = cells[0];
            var second = cells[1];
            var horizontal = first.Y == second.Y && Math.Abs((long)first.X - second.X) == 1;
            var vertical = first.X == second.X && Math.Abs((long)first.Y - second.Y) == 1;
            if (!horizontal && !vertical)
                return false;

            axis = horizontal ? RoadPathAxis.Horizontal : RoadPathAxis.Vertical;
            var direction = horizontal
                ? Math.Sign((long)second.X - first.X)
                : Math.Sign((long)second.Y - first.Y);

            for (var index = 1; index < cells.Count; index++)
            {
                var previous = cells[index - 1];
                var current = cells[index];
                if (horizontal)
                {
                    if (current.Y != first.Y || Math.Abs((long)current.X - previous.X) != 1 ||
                        Math.Sign((long)current.X - previous.X) != direction)
                        return false;
                }
                else if (current.X != first.X || Math.Abs((long)current.Y - previous.Y) != 1 ||
                         Math.Sign((long)current.Y - previous.Y) != direction)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class StraightRoadPathCalculator
    {
        public static RoadPath Calculate(CellCoord start, CellCoord end)
        {
            return RoadPath.Calculate(start, end);
        }
    }

    public static class RoadPathCalculator
    {
        public static RoadPath Calculate(CellCoord start, CellCoord end)
        {
            return RoadPath.Calculate(start, end);
        }
    }
}
