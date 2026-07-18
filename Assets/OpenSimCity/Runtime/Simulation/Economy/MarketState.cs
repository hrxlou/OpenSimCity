using System;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Aggregate single-commodity ("goods") market for one month.
    /// <para>
    /// Conservation and loss policy: goods are produced and consumed within the
    /// same month and there is no inventory carryover between months. The matched
    /// flow (<see cref="GoodsSold"/>) is conserved; unsold industrial output
    /// (<see cref="GoodsSurplus"/>) is lost at month end and unmet consumer demand
    /// (<see cref="GoodsShortage"/>) is not carried forward. All quantities are in
    /// abstract goods units and every value is an integer.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class MarketState
    {
        /// <summary>Goods produced by filled industrial jobs this month.</summary>
        public int GoodsSupply { get; private set; }

        /// <summary>Consumption power: goods demanded by residents this month.</summary>
        public int ConsumptionPower { get; private set; }

        /// <summary>Matched (conserved) flow = min(supply, consumption power).</summary>
        public int GoodsSold { get; private set; }

        /// <summary>Unmet consumer demand = consumption power - goods sold.</summary>
        public int GoodsShortage { get; private set; }

        /// <summary>Unsold industrial output = supply - goods sold (lost at month end).</summary>
        public int GoodsSurplus { get; private set; }

        /// <summary>Goods commercial businesses can move to consumers this month.</summary>
        public int CommercialThroughputCapacity { get; private set; }

        /// <summary>Goods commercial businesses actually moved = min(goods sold, capacity).</summary>
        public int CommercialThroughput { get; private set; }

        /// <summary>Business utilisation in permille = throughput * 1000 / capacity (0 when no capacity).</summary>
        public int BusinessUtilizationPermille { get; private set; }

        internal void Set(
            int goodsSupply,
            int consumptionPower,
            int commercialThroughputCapacity)
        {
            if (goodsSupply < 0)
                throw new ArgumentOutOfRangeException(nameof(goodsSupply));
            if (consumptionPower < 0)
                throw new ArgumentOutOfRangeException(nameof(consumptionPower));
            if (commercialThroughputCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(commercialThroughputCapacity));

            GoodsSupply = goodsSupply;
            ConsumptionPower = consumptionPower;
            GoodsSold = Math.Min(goodsSupply, consumptionPower);
            GoodsShortage = consumptionPower - GoodsSold;
            GoodsSurplus = goodsSupply - GoodsSold;
            CommercialThroughputCapacity = commercialThroughputCapacity;
            CommercialThroughput = Math.Min(GoodsSold, commercialThroughputCapacity);
            BusinessUtilizationPermille = commercialThroughputCapacity > 0
                ? CommercialThroughput * 1000 / commercialThroughputCapacity
                : 0;
        }

        public MarketStateDto ToDto()
        {
            return new MarketStateDto
            {
                GoodsSupply = GoodsSupply,
                ConsumptionPower = ConsumptionPower,
                GoodsSold = GoodsSold,
                GoodsShortage = GoodsShortage,
                GoodsSurplus = GoodsSurplus,
                CommercialThroughputCapacity = CommercialThroughputCapacity,
                CommercialThroughput = CommercialThroughput,
                BusinessUtilizationPermille = BusinessUtilizationPermille
            };
        }
    }

    [Serializable]
    public sealed class MarketStateDto
    {
        public int GoodsSupply;
        public int ConsumptionPower;
        public int GoodsSold;
        public int GoodsShortage;
        public int GoodsSurplus;
        public int CommercialThroughputCapacity;
        public int CommercialThroughput;
        public int BusinessUtilizationPermille;
    }
}
