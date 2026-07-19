using System;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Commands
{
    /// <summary>Application command boundary for tax policy; the next-month rule is supplied by the scheduler owner.</summary>
    public sealed class MunicipalFinanceCommandHandler : ICityCommandHandler
    {
        private readonly MunicipalFinanceState _finance;
        private readonly Func<SimulationDate> _nextMonth;

        public MunicipalFinanceCommandHandler(MunicipalFinanceState finance, Func<SimulationDate> nextMonth)
        {
            _finance = finance ?? throw new ArgumentNullException(nameof(finance));
            _nextMonth = nextMonth ?? throw new ArgumentNullException(nameof(nextMonth));
        }

        public bool CanHandle(ICityCommand command) => command is SetMunicipalTaxRatesCommand;

        public CommandValidationResult Validate(CommandEnvelope command)
        {
            return command.Command is SetMunicipalTaxRatesCommand
                ? CommandValidationResult.Valid
                : CommandValidationResult.Unsupported("No municipal finance handler can execute this command.");
        }

        public CommandExecutionResult Execute(CommandEnvelope command)
        {
            if (!(command.Command is SetMunicipalTaxRatesCommand taxRates))
                return CommandExecutionResult.Unsupported("No municipal finance handler can execute this command.");

            // The month exposed by the scheduler is the current in-progress
            // period, so a policy requested now must skip it and take effect at
            // the following monthly boundary.
            var effectiveMonth = _nextMonth().AddMonths(1);
            _finance.ScheduleTaxRates(taxRates.Rates, effectiveMonth);
            var result = new MunicipalTaxRateCommandResult(
                true,
                taxRates.Rates,
                effectiveMonth,
                $"Tax rates will apply in {effectiveMonth.Year}-{effectiveMonth.Month:D2}.");
            return CommandExecutionResult.Applied(result);
        }
    }
}
