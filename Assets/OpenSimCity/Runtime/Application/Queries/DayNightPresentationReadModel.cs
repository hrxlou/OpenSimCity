using System;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Application.Queries
{
    public sealed class DayNightPresentationSnapshot
    {
        public SimulationDate Month { get; internal set; }
        public int Day { get; internal set; }
        public int DayPhasePermille { get; internal set; }
        public bool IsNight { get; internal set; }
        public SimulationSpeed SimulationSpeed { get; internal set; }
        public long SourceTick { get; internal set; }
    }

    public sealed class DayNightPresentationReadModel
    {
        private const int DaysPerMonth = 30;
        private readonly CityRuntime _runtime;
        private long _sourceTick = long.MinValue;
        private SimulationSpeed _sourceSpeed = (SimulationSpeed)255;

        public DayNightPresentationReadModel(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            RefreshIfNeeded();
        }

        public int Version { get; private set; }
        public DayNightPresentationSnapshot Snapshot { get; private set; } = new DayNightPresentationSnapshot();

        public bool RefreshIfNeeded()
        {
            var tick = _runtime.Clock.CurrentTick.Value;
            var speed = _runtime.Clock.Speed;
            if (_sourceTick == tick && _sourceSpeed == speed) return false;
            var scheduler = _runtime.MonthlyScheduler;
            var periodStart = scheduler.NextRunTick.Value - (scheduler.TicksPerMonth - 1L);
            var progressed = tick - periodStart;
            if (progressed < 0L) progressed = 0L;
            if (progressed >= scheduler.TicksPerMonth) progressed = scheduler.TicksPerMonth - 1L;
            var scaled = progressed * DaysPerMonth;
            var day = (int)(scaled / scheduler.TicksPerMonth) + 1;
            var phase = (int)((scaled % scheduler.TicksPerMonth) * 1000L / scheduler.TicksPerMonth);
            Snapshot = new DayNightPresentationSnapshot
            {
                Month = _runtime.CurrentSimulationMonth,
                Day = day,
                DayPhasePermille = phase,
                IsNight = phase < 220 || phase >= 780,
                SimulationSpeed = speed,
                SourceTick = tick
            };
            _sourceTick = tick;
            _sourceSpeed = speed;
            Version = checked(Version + 1);
            return true;
        }
    }
}
