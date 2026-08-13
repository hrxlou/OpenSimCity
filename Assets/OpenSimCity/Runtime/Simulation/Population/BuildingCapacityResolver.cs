using System;
using OpenSimCity.Simulation.Buildings;

namespace OpenSimCity.Simulation.Population
{
    /// <summary>
    /// Optional monthly capacity modifier used by condition, service or policy
    /// systems. Implementations receive static definition capacity and may only
    /// reduce it; the default resolver preserves existing P3 behaviour.
    /// </summary>
    public interface IBuildingCapacityResolver
    {
        int ResolveHousingCapacity(BuildingState building, int definitionCapacity);
        int ResolveJobCapacity(BuildingState building, int definitionCapacity);
    }

    public sealed class PassThroughBuildingCapacityResolver : IBuildingCapacityResolver
    {
        public static readonly PassThroughBuildingCapacityResolver Instance =
            new PassThroughBuildingCapacityResolver();

        private PassThroughBuildingCapacityResolver() { }

        public int ResolveHousingCapacity(BuildingState building, int definitionCapacity)
        {
            if (building == null)
                throw new ArgumentNullException(nameof(building));
            if (definitionCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(definitionCapacity));
            return definitionCapacity;
        }

        public int ResolveJobCapacity(BuildingState building, int definitionCapacity)
        {
            if (building == null)
                throw new ArgumentNullException(nameof(building));
            if (definitionCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(definitionCapacity));
            return definitionCapacity;
        }
    }
}
