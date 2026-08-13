using System;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Specialization;

namespace OpenSimCity.Application.Specialization
{
    [Serializable]
    public sealed class SpecializationRuntimeSnapshotDto
    {
        public int SchemaVersion = 1;
        public UrbanManufacturingStateDto State;
    }

    public sealed class SpecializationRuntimeModule
    {
        private sealed class MonthlyAdapter : IMonthlySimulationSystem
        {
            private readonly SpecializationRuntimeModule _owner;
            public MonthlyAdapter(SpecializationRuntimeModule owner) { _owner = owner; }
            public void RunMonth(SimulationDate month) { _owner.System.RunMonth(month); }
        }

        internal SpecializationRuntimeModule(CityRuntime runtime, ISpecializationEnvironmentProvider environment, UrbanManufacturingParameters parameters = null)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            System = new UrbanManufacturingSpecializationSystem(Environment, parameters);
            Runtime.MunicipalFinanceSystem.RegisterPostTripDemandSystem(new MonthlyAdapter(this));
        }

        public CityRuntime Runtime { get; }
        public ISpecializationEnvironmentProvider Environment { get; }
        public UrbanManufacturingSpecializationSystem System { get; }

        public SpecializationRuntimeSnapshotDto Capture()
        {
            return new SpecializationRuntimeSnapshotDto { SchemaVersion = 1, State = System.State.ToDto() };
        }

        public void Restore(SpecializationRuntimeSnapshotDto snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.State == null)
                throw new InvalidOperationException("Unsupported specialization snapshot schema.");
            System.RestoreFromDto(snapshot.State);
        }
    }

    public static class SpecializationRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, SpecializationRuntimeModule> Modules = new ConditionalWeakTable<CityRuntime, SpecializationRuntimeModule>();

        public static SpecializationRuntimeModule GetOrCreate(CityRuntime runtime, ISpecializationEnvironmentProvider environment, UrbanManufacturingParameters parameters = null)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new SpecializationRuntimeModule(runtime, environment, parameters);
            Modules.Add(runtime, created);
            return created;
        }
    }
}
