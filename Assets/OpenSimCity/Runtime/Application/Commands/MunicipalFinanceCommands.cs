using System;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Commands
{
    /// <summary>Requests one city-wide R/C/I tax-rate set; it never mutates market state directly.</summary>
    public sealed class SetMunicipalTaxRatesCommand : ICityCommand
    {
        public SetMunicipalTaxRatesCommand(int residentialPermille, int commercialPermille, int industrialPermille)
        {
            Rates = new UrbanEconomyTaxRates(residentialPermille, commercialPermille, industrialPermille);
        }

        public UrbanEconomyTaxRates Rates { get; }
    }

    [Serializable]
    public sealed class MunicipalTaxRateCommandResult
    {
        internal MunicipalTaxRateCommandResult(
            bool isApplied,
            UrbanEconomyTaxRates rates,
            SimulationDate effectiveMonth,
            string message)
        {
            IsApplied = isApplied;
            Rates = rates;
            EffectiveMonth = effectiveMonth;
            Message = message ?? string.Empty;
        }

        public bool IsApplied { get; }
        public UrbanEconomyTaxRates Rates { get; }
        public SimulationDate EffectiveMonth { get; }
        public string Message { get; }
    }
}
