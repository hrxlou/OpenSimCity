using System;

namespace OpenSimCity.Simulation.Roads
{
    /// <summary>
    /// Minimal construction balance used by P2 road commands. It is deliberately
    /// separate from the road network so later municipal finance systems can
    /// replace the source without changing road occupancy rules.
    /// </summary>
    [Serializable]
    public sealed class RoadConstructionBudget
    {
        public RoadConstructionBudget(int initialFunds)
        {
            if (initialFunds < 0)
                throw new ArgumentOutOfRangeException(nameof(initialFunds), "Funds cannot be negative.");

            Funds = initialFunds;
        }

        public int Funds { get; private set; }

        public int Balance => Funds;

        public int AvailableFunds => Funds;

        public bool CanAfford(int cost)
        {
            return cost >= 0 && Funds >= cost;
        }

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost))
                return false;

            Funds -= cost;
            return true;
        }

        /// <summary>
        /// Restores a charge when a validated state application unexpectedly
        /// fails. The handler uses this to keep a failed commit atomic.
        /// </summary>
        public bool TryRefund(int amount)
        {
            if (amount < 0 || (long)Funds + amount > int.MaxValue)
                return false;

            Funds += amount;
            return true;
        }
    }
}
