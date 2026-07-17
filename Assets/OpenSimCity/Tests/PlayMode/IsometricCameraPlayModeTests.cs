using System.Collections;
using NUnit.Framework;
using OpenSimCity.Gameplay.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityCamera = UnityEngine.Camera;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class IsometricCameraPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator CityPrototypeContainsConfiguredCameraRig()
        {
            yield return SceneManager.LoadSceneAsync("CityPrototype", LoadSceneMode.Single);

            var rig = Object.FindAnyObjectByType<IsometricCameraRig>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.ControlledCamera, Is.Not.Null);
            Assert.That(rig.ControlledCamera.orthographic, Is.True);
            Assert.That(rig.WorldBounds.Width, Is.EqualTo(64));
            Assert.That(rig.WorldBounds.Height, Is.EqualTo(64));

            rig.SetOrthographicSize(-100f);
            Assert.That(rig.OrthographicSize, Is.EqualTo(6f).Within(0.001f));
            rig.SetOrthographicSize(100f);
            Assert.That(rig.OrthographicSize, Is.EqualTo(32f).Within(0.001f));

            rig.SetOrthographicSize(18f);
            rig.ApplyZoomDelta(0.05f);
            Assert.That(rig.OrthographicSize, Is.EqualTo(17.8f).Within(0.001f),
                "Smooth-scroll input should use the responsive 4x zoom sensitivity.");

            rig.SetOrthographicSize(18f);
            rig.ApplyZoomDelta(6f);
            Assert.That(rig.OrthographicSize, Is.EqualTo(12f).Within(0.001f),
                "A large wheel delta should be capped to one six-unit zoom step.");

            for (var i = 0; i < 4; i++)
            {
                rig.SetQuarterTurn(i);
                rig.SetTarget(new Vector2(-1000f, 1000f));
                Assert.That(rig.Target.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rig.Target.x, Is.LessThanOrEqualTo(rig.WorldBounds.WorldWidth));
                Assert.That(rig.Target.y, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rig.Target.y, Is.LessThanOrEqualTo(rig.WorldBounds.WorldHeight));
            }
        }

        [UnityTest]
        public IEnumerator CameraDragDoesNotProduceAForwardableClick()
        {
            var rigObject = new GameObject("CameraRigTest");
            var source = rigObject.AddComponent<WorldBoundsReadModelSource>();
            var cameraObject = new GameObject("Test Camera", typeof(UnityCamera));
            cameraObject.transform.SetParent(rigObject.transform, worldPositionStays: false);
            var rig = rigObject.AddComponent<IsometricCameraRig>();
            Assert.That(source, Is.Not.Null);

            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(100f, 100f));
            Press(mouse.middleButton);
            yield return null;

            Set(mouse.position, new Vector2(140f, 100f));
            yield return null;
            Assert.That(rig.IsCameraDragActive, Is.True);
            Assert.That(rig.PointerInteraction.ShouldSuppressClick, Is.True);

            Release(mouse.middleButton);
            yield return null;
            Assert.That(rig.PointerInteraction.DragCompletedThisFrame, Is.True);
            Assert.That(rig.PointerInteraction.ClickReleasedThisFrame, Is.False);
            Assert.That(rig.PointerInteraction.ShouldSuppressClick, Is.True);

            yield return null;
            Assert.That(rig.PointerInteraction.ShouldSuppressClick, Is.False);

            Object.Destroy(rigObject);
            Object.Destroy(cameraObject);
        }
    }
}
