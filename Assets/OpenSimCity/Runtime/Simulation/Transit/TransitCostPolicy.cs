using System;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Transit
{
    public interface ITransitGeneralizedCostPolicy
    {
        int Version { get; }
        int AdjustBusCostSeconds(TransitOdKey key, SimulationDate month, int baseCostSeconds);
    }

    public sealed class IdentityTransitGeneralizedCostPolicy : ITransitGeneralizedCostPolicy
    {
        public static readonly IdentityTransitGeneralizedCostPolicy Instance = new IdentityTransitGeneralizedCostPolicy();
        private IdentityTransitGeneralizedCostPolicy() { }
        public int Version => 0;
        public int AdjustBusCostSeconds(TransitOdKey key, SimulationDate month, int baseCostSeconds)
        {
            if (baseCostSeconds < 0) throw new ArgumentOutOfRangeException(nameof(baseCostSeconds));
            return baseCostSeconds;
        }
    }
}
