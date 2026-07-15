using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Gameplay.Selection;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class GridSelectionMathTests
    {
        private static readonly WorldBoundsReadModel Bounds = new WorldBoundsReadModel(64, 64, 1f);

        [Test]
        public void EveryCellCenterRoundTripsToItsCoordinate()
        {
            for (var y = 0; y < Bounds.Height; y++)
            {
                for (var x = 0; x < Bounds.Width; x++)
                {
                    var expected = new CellCoord(x, y);
                    var center = GridSelectionMath.CellCenter(expected, Bounds);

                    Assert.That(GridSelectionMath.TryWorldToCell(center, Bounds, out var actual), Is.True);
                    Assert.That(actual, Is.EqualTo(expected));
                }
            }
        }

        [Test]
        public void FarWorldEdgesAreOutsideWhileTheLastCellRemainsSelectable()
        {
            Assert.That(
                GridSelectionMath.TryWorldToCell(new Vector3(63.999f, 0f, 63.999f), Bounds, out var last),
                Is.True);
            Assert.That(last, Is.EqualTo(new CellCoord(63, 63)));

            Assert.That(GridSelectionMath.TryWorldToCell(new Vector3(64f, 0f, 0f), Bounds, out _), Is.False);
            Assert.That(GridSelectionMath.TryWorldToCell(new Vector3(0f, 0f, 64f), Bounds, out _), Is.False);
            Assert.That(GridSelectionMath.TryWorldToCell(new Vector3(-0.001f, 0f, 0f), Bounds, out _), Is.False);
        }

        [Test]
        public void NonFiniteWorldCoordinatesAreRejectedExplicitly()
        {
            Assert.That(
                () => GridSelectionMath.TryWorldToCell(new Vector3(float.NaN, 0f, 0f), Bounds, out _),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => GridSelectionMath.CellCenter(new CellCoord(64, 0), Bounds),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
