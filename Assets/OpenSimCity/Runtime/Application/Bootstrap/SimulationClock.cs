using System;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Accumulates frame time and consumes fixed simulation ticks independently of
    /// render frame pacing. The current tick is the next tick to execute; tick zero
    /// is therefore the first tick emitted by a new clock.
    /// </summary>
    public sealed class SimulationClock
    {
        public const double DefaultFixedDeltaSeconds = 0.1d;
        public const int DefaultMaxCatchUpTicks = 4;

        private const double ComparisonEpsilon = 1e-9d;
        private readonly double _fixedDeltaSeconds;
        private readonly int _maxCatchUpTicks;
        private double _accumulatedSeconds;
        private SimulationSpeed _speed;

        public SimulationClock(
            double fixedDeltaSeconds = DefaultFixedDeltaSeconds,
            int maxCatchUpTicks = DefaultMaxCatchUpTicks,
            SimulationTick firstTick = default(SimulationTick))
        {
            if (double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds) || fixedDeltaSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
            if (maxCatchUpTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCatchUpTicks));
            if (fixedDeltaSeconds > double.MaxValue / maxCatchUpTicks)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));

            _fixedDeltaSeconds = fixedDeltaSeconds;
            _maxCatchUpTicks = maxCatchUpTicks;
            CurrentTick = firstTick;
            _speed = SimulationSpeed.One;
        }

        public double FixedDeltaSeconds => _fixedDeltaSeconds;

        public int MaxCatchUpTicks => _maxCatchUpTicks;

        /// <summary>
        /// The next simulation tick to execute.
        /// </summary>
        public SimulationTick CurrentTick { get; private set; }

        public SimulationTick NextTick => CurrentTick;

        public SimulationSpeed Speed
        {
            get => _speed;
            set => _speed = ValidateSpeed(value);
        }

        public double AccumulatedSeconds => _accumulatedSeconds;

        /// <summary>
        /// True when the most recent advance discarded time above the catch-up window.
        /// </summary>
        public bool WasCatchUpClamped { get; private set; }

        public bool LastAdvanceWasCatchUpLimited => WasCatchUpClamped;

        public void SetSpeed(SimulationSpeed speed)
        {
            Speed = speed;
        }

        public int Advance(double realDeltaSeconds)
        {
            return Advance(realDeltaSeconds, null);
        }

        public int Advance(double realDeltaSeconds, Action<SimulationTick> tickConsumer)
        {
            ValidateDelta(realDeltaSeconds);
            WasCatchUpClamped = false;

            if (_speed == SimulationSpeed.Paused)
                return 0;

            var scaledDelta = realDeltaSeconds * GetMultiplier(_speed);
            if (double.IsInfinity(scaledDelta))
                throw new ArgumentOutOfRangeException(nameof(realDeltaSeconds));

            _accumulatedSeconds += scaledDelta;
            if (double.IsInfinity(_accumulatedSeconds))
                throw new ArgumentOutOfRangeException(nameof(realDeltaSeconds));

            var catchUpWindow = _fixedDeltaSeconds * _maxCatchUpTicks;
            if (_accumulatedSeconds > catchUpWindow)
            {
                _accumulatedSeconds = catchUpWindow;
                WasCatchUpClamped = true;
            }

            var availableTicks = (int)Math.Floor(
                (_accumulatedSeconds / _fixedDeltaSeconds) + ComparisonEpsilon);
            if (availableTicks > _maxCatchUpTicks)
            {
                availableTicks = _maxCatchUpTicks;
                _accumulatedSeconds = catchUpWindow;
                WasCatchUpClamped = true;
            }

            if (availableTicks > 0 &&
                CurrentTick.Value > long.MaxValue - availableTicks)
            {
                throw new OverflowException(
                    "The simulation clock cannot advance past Int64.MaxValue.");
            }

            _accumulatedSeconds -= availableTicks * _fixedDeltaSeconds;
            if (Math.Abs(_accumulatedSeconds) < ComparisonEpsilon)
                _accumulatedSeconds = 0d;

            for (var index = 0; index < availableTicks; index++)
            {
                var tick = CurrentTick;
                if (tickConsumer != null)
                    tickConsumer(tick);
                CurrentTick = tick.Next();
            }

            return availableTicks;
        }

        internal void RestoreState(
            SimulationTick currentTick,
            SimulationSpeed speed,
            double accumulatedSeconds,
            bool wasCatchUpClamped)
        {
            if (double.IsNaN(accumulatedSeconds) ||
                double.IsInfinity(accumulatedSeconds) ||
                accumulatedSeconds < 0d ||
                accumulatedSeconds >= _fixedDeltaSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(accumulatedSeconds),
                    "Saved clock accumulation must be within one fixed-tick interval.");
            }

            CurrentTick = currentTick;
            _speed = ValidateSpeed(speed);
            _accumulatedSeconds = accumulatedSeconds;
            WasCatchUpClamped = wasCatchUpClamped;
        }

        private static void ValidateDelta(double realDeltaSeconds)
        {
            if (double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds) || realDeltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(realDeltaSeconds));
        }

        private static SimulationSpeed ValidateSpeed(SimulationSpeed speed)
        {
            if (speed != SimulationSpeed.Paused &&
                speed != SimulationSpeed.One &&
                speed != SimulationSpeed.Two &&
                speed != SimulationSpeed.Four)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            return speed;
        }

        private static double GetMultiplier(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.One:
                    return 1d;
                case SimulationSpeed.Two:
                    return 2d;
                case SimulationSpeed.Four:
                    return 4d;
                case SimulationSpeed.Paused:
                    return 0d;
                default:
                    throw new ArgumentOutOfRangeException(nameof(speed));
            }
        }
    }
}
