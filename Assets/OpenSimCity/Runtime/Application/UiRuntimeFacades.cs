using System;
using OpenSimCity.Application.Conditions;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.Specialization;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application
{
    /// <summary>
    /// UI-safe composition seams. Public signatures intentionally expose only
    /// Application/Core types even when the underlying capability uses Simulation
    /// parameters internally. This keeps the UI assembly independent of Simulation.
    /// </summary>
    public static class UiRuntimeFacades
    {
        public static BuildingConditionRuntimeModule GetBuildingConditions(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return BuildingConditionRuntimeRegistry.GetOrCreate(runtime);
        }

        public static DistrictPolicyRuntimeModule GetDistrictPolicies(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return DistrictPolicyRuntimeRegistry.GetOrCreate(runtime);
        }

        public static SpecializationRuntimeModule GetSpecialization(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return SpecializationRuntimeRegistry.GetOrCreate(
                runtime,
                new CitySpecializationEnvironment(runtime));
        }

        public static TransitRuntimeModule GetTransit(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return TransitRuntimeRegistry.GetOrCreate(runtime);
        }
    }
}
