using System.Collections.Generic;
using NUnit.Framework;
using OpenSimCity.Application.Input;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class UiInputBoundaryStateTests
    {
        [Test]
        public void FreshStateDoesNotBlockWorldInput()
        {
            var state = new UiInputBoundaryState();
            Assert.That(state.BlocksWorldInput, Is.False);
        }

        [Test]
        public void PointerCaptureBlocksWorldInputUntilReleased()
        {
            var state = new UiInputBoundaryState();

            state.CapturePointer();
            Assert.That(state.IsPointerOverUi, Is.True);
            Assert.That(state.BlocksWorldInput, Is.True);

            state.ReleasePointer();
            Assert.That(state.BlocksWorldInput, Is.False);
        }

        [Test]
        public void OverlappingPanelsEachHoldTheirOwnCapture()
        {
            var state = new UiInputBoundaryState();

            state.CapturePointer();
            state.CapturePointer();
            state.ReleasePointer();

            // The first release must not re-open world input while a second panel
            // is still under the pointer.
            Assert.That(state.BlocksWorldInput, Is.True);

            state.ReleasePointer();
            Assert.That(state.BlocksWorldInput, Is.False);
        }

        [Test]
        public void UnbalancedReleaseDoesNotDriveTheCountNegative()
        {
            var state = new UiInputBoundaryState();

            state.ReleasePointer();
            state.ReleasePointer();
            state.CapturePointer();

            Assert.That(state.BlocksWorldInput, Is.True);
        }

        [Test]
        public void KeyboardFocusAloneBlocksWorldInput()
        {
            var state = new UiInputBoundaryState();

            state.CaptureKeyboardFocus();
            Assert.That(state.IsPointerOverUi, Is.False);
            Assert.That(state.BlocksWorldInput, Is.True);

            state.ReleaseKeyboardFocus();
            Assert.That(state.BlocksWorldInput, Is.False);
        }

        [Test]
        public void ReleaseAllClearsPointerAndFocus()
        {
            var state = new UiInputBoundaryState();
            state.CapturePointer();
            state.CapturePointer();
            state.CaptureKeyboardFocus();

            state.ReleaseAll();

            Assert.That(state.BlocksWorldInput, Is.False);
        }

        [Test]
        public void ChangedEventFiresOnlyOnTransitions()
        {
            var state = new UiInputBoundaryState();
            var observed = new List<bool>();
            state.BlocksWorldInputChanged += observed.Add;

            state.CapturePointer();
            state.CapturePointer();
            state.CaptureKeyboardFocus();
            state.ReleasePointer();
            state.ReleasePointer();
            state.ReleaseKeyboardFocus();

            Assert.That(observed, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ResetInstallsAFreshStateSoLeaksDoNotCrossTests()
        {
            UiInputBoundary.Reset();
            UiInputBoundary.Current.CapturePointer();

            var reset = UiInputBoundary.Reset();

            Assert.That(reset.BlocksWorldInput, Is.False);
            Assert.That(UiInputBoundary.Current, Is.SameAs(reset));
        }
    }
}
