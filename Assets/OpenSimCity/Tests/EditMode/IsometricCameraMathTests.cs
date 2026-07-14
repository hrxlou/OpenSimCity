using NUnit.Framework;
using OpenSimCity.Application.Queries;
using OpenSimCity.Gameplay.Camera;
using UnityEngine;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class IsometricCameraMathTests
    {
        [Test]
        public void FourQuarterTurnsReturnToTheOriginalPose()
        {
            var original = IsometricCameraMath.CalculatePose(0);
            var rotated = IsometricCameraMath.CalculatePose(4);

            Assert.That(Quaternion.Angle(original.Rotation, rotated.Rotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(original.Forward, rotated.Forward), Is.LessThan(0.001f));
        }

        [TestCase(1.7777778f)]
        [TestCase(2.3333333f)]
        [TestCase(1.3333333f)]
        public void ClampingKeepsVisibleGroundFootprintInsideWorldWhenItFits(float aspect)
        {
            var bounds = new WorldBoundsReadModel(64, 64, 1f);

            for (var quarterTurn = 0; quarterTurn < 4; quarterTurn++)
            {
                var footprint = IsometricCameraMath.CalculateGroundFootprint(6f, aspect, quarterTurn);
                var clamped = IsometricCameraMath.ClampTarget(
                    new Vector2(-1000f, 1000f),
                    bounds,
                    footprint);

                if (footprint.MaxX - footprint.MinX <= bounds.WorldWidth)
                {
                    Assert.That(clamped.x + footprint.MinX, Is.GreaterThanOrEqualTo(bounds.MinX - 0.001f));
                    Assert.That(clamped.x + footprint.MaxX, Is.LessThanOrEqualTo(bounds.MaxX + 0.001f));
                }

                if (footprint.MaxZ - footprint.MinZ <= bounds.WorldHeight)
                {
                    Assert.That(clamped.y + footprint.MinZ, Is.GreaterThanOrEqualTo(bounds.MinZ - 0.001f));
                    Assert.That(clamped.y + footprint.MaxZ, Is.LessThanOrEqualTo(bounds.MaxZ + 0.001f));
                }
            }
        }

        [Test]
        public void OversizedViewUsesWorldCenterInsteadOfLeavingTheBounds()
        {
            var bounds = new WorldBoundsReadModel(64, 64, 1f);
            var clamped = IsometricCameraMath.ClampTarget(
                new Vector2(-100f, 100f),
                bounds,
                IsometricCameraMath.CalculateGroundFootprint(100f, 2.3333333f, 0));

            Assert.That(clamped.x, Is.EqualTo(bounds.CenterX).Within(0.001f));
            Assert.That(clamped.y, Is.EqualTo(bounds.CenterZ).Within(0.001f));
        }

        [Test]
        public void MovementIsRelativeToTheCurrentCameraQuarterTurn()
        {
            var first = IsometricCameraMath.CalculatePlanarMovement(
                Vector2.up,
                IsometricCameraMath.CalculatePose(0));
            var rotated = IsometricCameraMath.CalculatePlanarMovement(
                Vector2.up,
                IsometricCameraMath.CalculatePose(1));

            Assert.That(first.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rotated.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Vector3.Dot(first, rotated), Is.LessThan(0.01f));
        }
    }
}
