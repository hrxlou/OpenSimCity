using System;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Transit
{
    /// <summary>
    /// Post-MVP capability module attached to a base CityRuntime without moving
    /// authoritative collections into Unity objects. It consumes the base runtime's
    /// command sequence and persistent municipal balance while owning transit state.
    /// </summary>
    public sealed class TransitRuntimeModule
    {
        private readonly CityRuntime _runtime;

        internal TransitRuntimeModule(
            CityRuntime runtime,
            TransitModeChoiceParameters parameters = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Parameters = parameters ?? TransitModeChoiceParameters.Default;
            Network = new TransitNetworkState();
            Commands = new TransitCommandHandler(
                Network,
                runtime.Roads,
                runtime.RoadGraph,
                runtime.RoadDefinitions,
                runtime.Finance,
                () => runtime.CurrentSimulationMonth,
                Parameters);
            ModeChoice = new TransitModeChoiceSystem(
                Network,
                runtime.Roads,
                runtime.RoadGraph,
                runtime.RoadDefinitions,
                runtime.TripDemandSystem,
                Parameters);
            PrivateVehicleRouting = new TransitPrivateVehicleRoutingSystem(
                runtime.Roads,
                runtime.RoadGraph,
                runtime.RoadDefinitions,
                runtime.TripDemandSystem,
                ModeChoice);
            ReadModel = new TransitReadModel(this);
        }

        public CityRuntime Runtime => _runtime;
        public TransitModeChoiceParameters Parameters { get; }
        public TransitNetworkState Network { get; }
        public TransitCommandHandler Commands { get; }
        public TransitModeChoiceSystem ModeChoice { get; }
        public TransitPrivateVehicleRoutingSystem PrivateVehicleRouting { get; }
        public TransitReadModel ReadModel { get; }

        /// <summary>
        /// Uses CityRuntime.Execute once to consume the shared deterministic command
        /// sequence. The base dispatcher intentionally returns Unsupported; the
        /// transit handler then applies the command with the same stamp.
        /// </summary>
        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command))
                return CommandExecutionResult.Unsupported("The transit module cannot execute this command.");

            var sequenceResult = _runtime.Execute(command);
            if (sequenceResult.Status != CommandExecutionStatus.Unsupported)
                return sequenceResult;

            var result = Commands.Execute(new CommandEnvelope(sequenceResult.Stamp, command))
                .WithStamp(sequenceResult.Stamp);
            RefreshDerived();
            return result;
        }

        /// <summary>
        /// Runs after the base runtime has produced TripDemand for a month. The
        /// operating charge is accrued once and classified as infrastructure in the
        /// current municipal ledger compatibility seam.
        /// </summary>
        public void RunCurrentMonth()
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth)
                return;
            var month = _runtime.TripDemandSystem.State.LastProcessedMonth;
            Network.ApplyBudgetForMonth(month);

            int effectiveBudget;
            bool affordable;
            if (Network.HasProcessedOperatingPeriod && Network.LastOperatingMonth == month)
            {
                affordable = Network.LastProjectedOperatingExpense == 0 || Network.LastPaidOperatingExpense > 0;
                effectiveBudget = affordable ? Network.ActiveBudgetPermille : 0;
            }
            else
            {
                var projected = Network.CalculateOperatingExpense(Parameters, Network.ActiveBudgetPermille);
                affordable = _runtime.Finance.CanAfford(projected);
                var paid = 0;
                if (affordable && projected > 0)
                {
                    if (!_runtime.Finance.TrySpend(projected))
                        affordable = false;
                    else
                    {
                        _runtime.Finance.RecordConstructionExpense(
                            MunicipalConstructionCategory.Road,
                            projected);
                        paid = projected;
                    }
                }
                Network.RecordOperatingPeriod(month, projected, paid);
                effectiveBudget = affordable ? Network.ActiveBudgetPermille : 0;
            }

            ModeChoice.RunMonth(month, effectiveBudget, affordable);
            PrivateVehicleRouting.RunMonth(month);
            ReadModel.RefreshIfNeeded();
        }

        public bool RefreshDerived()
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth)
                return false;
            var affordable = !Network.HasProcessedOperatingPeriod ||
                             Network.LastProjectedOperatingExpense == 0 ||
                             Network.LastPaidOperatingExpense > 0;
            var effectiveBudget = affordable ? Network.ActiveBudgetPermille : 0;
            var changed = ModeChoice.RefreshIfNeeded(effectiveBudget, affordable);
            changed |= PrivateVehicleRouting.RefreshIfNeeded();
            changed |= ReadModel.RefreshIfNeeded();
            return changed;
        }

        public TransitRuntimeSnapshotDto Capture()
        {
            return new TransitRuntimeSnapshotDto
            {
                SchemaVersion = 1,
                Network = Network.ToDto()
            };
        }

        public void Restore(TransitRuntimeSnapshotDto snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion != 1)
                throw new InvalidOperationException("Unsupported transit snapshot schema version.");
            Network.RestoreFromDto(snapshot.Network ?? new TransitNetworkStateDto());
            RefreshDerived();
        }
    }

    public static class TransitRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, TransitRuntimeModule> Modules =
            new ConditionalWeakTable<CityRuntime, TransitRuntimeModule>();

        public static TransitRuntimeModule GetOrCreate(
            CityRuntime runtime,
            TransitModeChoiceParameters parameters = null)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing))
                return existing;
            var created = new TransitRuntimeModule(runtime, parameters);
            Modules.Add(runtime, created);
            return created;
        }

        public static bool TryGet(CityRuntime runtime, out TransitRuntimeModule module)
        {
            if (runtime == null)
            {
                module = null;
                return false;
            }
            return Modules.TryGetValue(runtime, out module);
        }
    }
}
