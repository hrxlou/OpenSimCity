using System;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Static integer rates used by the municipal ledger. They intentionally live
    /// apart from <see cref="UrbanEconomyParameters"/>: market demand and the
    /// city's cash balance are different simulation domains.
    /// </summary>
    [Serializable]
    public sealed class MunicipalFinanceParameters
    {
        public MunicipalFinanceParameters(
            int residentialTaxBasePerResident = 10,
            int commercialTaxBasePerFilledJob = 15,
            int industrialTaxBasePerFilledJob = 12)
        {
            if (residentialTaxBasePerResident < 0)
                throw new ArgumentOutOfRangeException(nameof(residentialTaxBasePerResident));
            if (commercialTaxBasePerFilledJob < 0)
                throw new ArgumentOutOfRangeException(nameof(commercialTaxBasePerFilledJob));
            if (industrialTaxBasePerFilledJob < 0)
                throw new ArgumentOutOfRangeException(nameof(industrialTaxBasePerFilledJob));

            ResidentialTaxBasePerResident = residentialTaxBasePerResident;
            CommercialTaxBasePerFilledJob = commercialTaxBasePerFilledJob;
            IndustrialTaxBasePerFilledJob = industrialTaxBasePerFilledJob;
        }

        public int ResidentialTaxBasePerResident { get; }

        public int CommercialTaxBasePerFilledJob { get; }

        public int IndustrialTaxBasePerFilledJob { get; }

        public static MunicipalFinanceParameters Default => new MunicipalFinanceParameters();
    }
}
