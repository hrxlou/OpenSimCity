using System;
using OpenSimCity.Application.Queries;
using UnityEngine;

namespace OpenSimCity.Gameplay.Camera
{
    public readonly struct IsometricCameraPose
    {
        public IsometricCameraPose(Quaternion rotation, Vector3 forward, Vector3 right, Vector3 up)
        {
            Rotation = rotation;
            Forward = forward;
            Right = right;
            Up = up;
        }

        public Quaternion Rotation { get; }

        public Vector3 Forward { get; }

        public Vector3 Right { get; }

        public Vector3 Up { get; }
    }

    /// <summary>
    /// Ground-plane rectangle visible through an orthographic isometric camera.
    /// Values are offsets from the camera target in world X/Z coordinates.
    /// </summary>
    public readonly struct CameraGroundFootprint
    {
        public CameraGroundFootprint(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float MinX { get; }

        public float MaxX { get; }

        public float MinZ { get; }

        public float MaxZ { get; }
    }

    /// <summary>
    /// Pure camera geometry and bounds calculations. No scene lookup or simulation mutation.
    /// </summary>
    public static class IsometricCameraMath
    {
        public const float DefaultYawDegrees = 45f;
        public const float DefaultPitchDegrees = 45f;

        public static int NormalizeQuarterTurn(int quarterTurn)
        {
            var normalized = quarterTurn % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        public static IsometricCameraPose CalculatePose(
            int quarterTurn,
            float pitchDegrees = DefaultPitchDegrees,
            float yawDegrees = DefaultYawDegrees)
        {
            if (pitchDegrees <= 0f || pitchDegrees >= 90f ||
                float.IsNaN(pitchDegrees) || float.IsInfinity(pitchDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(pitchDegrees), "Pitch must be between 0 and 90 degrees.");
            }

            if (float.IsNaN(yawDegrees) || float.IsInfinity(yawDegrees))
                throw new ArgumentOutOfRangeException(nameof(yawDegrees), "Yaw must be finite.");

            var yaw = (yawDegrees + NormalizeQuarterTurn(quarterTurn) * 90f) * Mathf.Deg2Rad;
            var pitch = pitchDegrees * Mathf.Deg2Rad;
            var horizontal = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var forward = new Vector3(
                horizontal.x * Mathf.Cos(pitch),
                -Mathf.Sin(pitch),
                horizontal.z * Mathf.Cos(pitch));
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            return new IsometricCameraPose(
                rotation,
                rotation * Vector3.forward,
                rotation * Vector3.right,
                rotation * Vector3.up);
        }

        public static Vector3 CalculateCameraPosition(Vector2 target, IsometricCameraPose pose, float distance)
        {
            if (distance <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance), "Camera distance must be finite and positive.");

            var targetWorld = new Vector3(target.x, 0f, target.y);
            return targetWorld - pose.Forward * distance;
        }

        public static CameraGroundFootprint CalculateGroundFootprint(
            float orthographicSize,
            float aspect,
            int quarterTurn,
            float pitchDegrees = DefaultPitchDegrees,
            float yawDegrees = DefaultYawDegrees)
        {
            if (orthographicSize <= 0f || float.IsNaN(orthographicSize) || float.IsInfinity(orthographicSize))
                throw new ArgumentOutOfRangeException(nameof(orthographicSize), "Orthographic size must be finite and positive.");
            if (aspect <= 0f || float.IsNaN(aspect) || float.IsInfinity(aspect))
                throw new ArgumentOutOfRangeException(nameof(aspect), "Aspect ratio must be finite and positive.");

            var pose = CalculatePose(quarterTurn, pitchDegrees, yawDegrees);
            var halfHeight = orthographicSize;
            var halfWidth = orthographicSize * aspect;
            const float cameraDistance = 1f;
            var cameraPosition = -pose.Forward * cameraDistance;

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;

            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    var screenOffset = pose.Right * (halfWidth * x) + pose.Up * (halfHeight * y);
                    var rayOrigin = cameraPosition + screenOffset;
                    var denominator = pose.Forward.y;
                    if (Mathf.Abs(denominator) < 0.0001f)
                    {
                        throw new InvalidOperationException(
                            "The camera pitch is too close to the horizon to intersect the ground plane.");
                    }

                    var rayDistance = -rayOrigin.y / denominator;
                    var groundPoint = rayOrigin + pose.Forward * rayDistance;
                    minX = Mathf.Min(minX, groundPoint.x);
                    maxX = Mathf.Max(maxX, groundPoint.x);
                    minZ = Mathf.Min(minZ, groundPoint.z);
                    maxZ = Mathf.Max(maxZ, groundPoint.z);
                }
            }

            return new CameraGroundFootprint(minX, maxX, minZ, maxZ);
        }

        public static Vector2 ClampTarget(
            Vector2 desiredTarget,
            WorldBoundsReadModel worldBounds,
            CameraGroundFootprint footprint)
        {
            return new Vector2(
                ClampAxis(
                    desiredTarget.x,
                    worldBounds.MinX,
                    worldBounds.MaxX,
                    footprint.MinX,
                    footprint.MaxX,
                    worldBounds.CenterX),
                ClampAxis(
                    desiredTarget.y,
                    worldBounds.MinZ,
                    worldBounds.MaxZ,
                    footprint.MinZ,
                    footprint.MaxZ,
                    worldBounds.CenterZ));
        }

        public static Vector2 ClampTarget(
            Vector2 desiredTarget,
            WorldBoundsReadModel worldBounds,
            float orthographicSize,
            float aspect,
            int quarterTurn,
            float pitchDegrees = DefaultPitchDegrees,
            float yawDegrees = DefaultYawDegrees)
        {
            var footprint = CalculateGroundFootprint(
                orthographicSize,
                aspect,
                quarterTurn,
                pitchDegrees,
                yawDegrees);
            return ClampTarget(desiredTarget, worldBounds, footprint);
        }

        public static Vector3 CalculatePlanarMovement(Vector2 input, IsometricCameraPose pose)
        {
            var forward = new Vector3(pose.Forward.x, 0f, pose.Forward.z);
            var right = new Vector3(pose.Right.x, 0f, pose.Right.z);
            if (forward.sqrMagnitude > 0f)
                forward.Normalize();
            if (right.sqrMagnitude > 0f)
                right.Normalize();

            var movement = right * input.x + forward * input.y;
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();
            return movement;
        }

        private static float ClampAxis(
            float desired,
            float worldMin,
            float worldMax,
            float footprintMin,
            float footprintMax,
            float worldCenter)
        {
            var minimumTarget = worldMin - footprintMin;
            var maximumTarget = worldMax - footprintMax;
            if (minimumTarget > maximumTarget)
                return worldCenter;

            return Mathf.Clamp(desired, minimumTarget, maximumTarget);
        }
    }
}
