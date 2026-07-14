using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class SimulationClockTests
    {
        [Test]
        public void FixedTickConsumptionIsIndependentOfDeltaPartitioning()
        {
            var singleFrameClock = new SimulationClock(0.1d, 8);
            var splitFrameClock = new SimulationClock(0.1d, 8);
            var singleFrameTicks = new List<SimulationTick>();
            var splitFrameTicks = new List<SimulationTick>();

            Assert.That(singleFrameClock.Advance(0.2d, singleFrameTicks.Add), Is.EqualTo(2));
            Assert.That(splitFrameClock.Advance(0.07d, splitFrameTicks.Add), Is.EqualTo(0));
            Assert.That(splitFrameClock.Advance(0.03d, splitFrameTicks.Add), Is.EqualTo(1));
            Assert.That(splitFrameClock.Advance(0.1d, splitFrameTicks.Add), Is.EqualTo(1));

            Assert.That(singleFrameTicks, Is.EqualTo(splitFrameTicks));
            Assert.That(singleFrameClock.CurrentTick, Is.EqualTo(splitFrameClock.CurrentTick));
            Assert.That(singleFrameClock.AccumulatedSeconds, Is.EqualTo(splitFrameClock.AccumulatedSeconds).Within(1e-12d));
        }

        [Test]
        public void SpeedControlsChangeTickRateWithoutChangingTickOrder()
        {
            var clock = new SimulationClock(0.1d, 8);
            var ticks = new List<SimulationTick>();

            clock.Speed = SimulationSpeed.Two;
            Assert.That(clock.Advance(0.1d, ticks.Add), Is.EqualTo(2));
            clock.Speed = SimulationSpeed.Four;
            Assert.That(clock.Advance(0.1d, ticks.Add), Is.EqualTo(4));

            Assert.That(ticks, Is.EqualTo(new[]
            {
                new SimulationTick(0),
                new SimulationTick(1),
                new SimulationTick(2),
                new SimulationTick(3),
                new SimulationTick(4),
                new SimulationTick(5)
            }));
        }

        [Test]
        public void PausedClockDoesNotConsumeOrAccumulateFrameTime()
        {
            var clock = new SimulationClock(0.1d, 4);
            clock.Speed = SimulationSpeed.Paused;

            Assert.That(clock.Advance(10d), Is.EqualTo(0));
            Assert.That(clock.CurrentTick, Is.EqualTo(SimulationTick.Zero));
            Assert.That(clock.AccumulatedSeconds, Is.EqualTo(0d));

            clock.Speed = SimulationSpeed.One;
            Assert.That(clock.Advance(0.1d), Is.EqualTo(1));
            Assert.That(clock.CurrentTick, Is.EqualTo(new SimulationTick(1)));
        }

        [Test]
        public void CatchUpIsBoundedAndExcessBacklogIsDiscarded()
        {
            var clock = new SimulationClock(0.1d, 3);

            Assert.That(clock.Advance(10d), Is.EqualTo(3));
            Assert.That(clock.WasCatchUpClamped, Is.True);
            Assert.That(clock.CurrentTick, Is.EqualTo(new SimulationTick(3)));
            Assert.That(clock.AccumulatedSeconds, Is.EqualTo(0d));
            Assert.That(clock.Advance(0d), Is.EqualTo(0));
        }

        [Test]
        public void InvalidClockInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(0d, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(0.1d, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(double.MaxValue, 2));

            var clock = new SimulationClock();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed((SimulationSpeed)3));
        }

        [Test]
        public void TickOverflowIsReportedBeforeAPartialAdvance()
        {
            var clock = new SimulationClock(0.1d, 4, new SimulationTick(long.MaxValue - 1));
            var ticks = new List<SimulationTick>();

            Assert.Throws<OverflowException>(() => clock.Advance(0.2d, ticks.Add));
            Assert.That(ticks, Is.Empty);
            Assert.That(clock.CurrentTick, Is.EqualTo(new SimulationTick(long.MaxValue - 1)));
        }
    }
}
