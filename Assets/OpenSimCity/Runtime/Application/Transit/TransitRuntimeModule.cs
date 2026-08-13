using System;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Bootstrap;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Economy;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Transit
{
    /// <summary>
    /// Post-MVP capability module attached to a base CityRuntime without moving
    /// authoritative collections into Unity objects. It consumes the base runtime's
    /// command sequence and persistent municipal balance while owning transit state.
    /// The module registers in the deterministic post-TripDemand monthly seam.
    /// </summary>
    public sealed class TransitRuntimeModule : IMonthlySimulationSystem
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
            runtime.MunicipalFinanceSystem.RegisterPostTripDemandSystem(this);
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
        /// Runs after the same month's TripDemand and before municipal settlement.
        /// Operating expense is charged and classified exactly once per month.
        /// </summary>
        public void RunMonth(SimulationDate month)
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth ||
                _runtime.TripDemandSystem.State.LastProcessedMonth != month)
            {
                throw new InvalidOperationException(
                    "Transit requires aggregate trip demand from the same month.");
            }

            Network.ApplyBudgetForMonth(month);
            int effectiveBudget;
            bool affordable;
            if (Network.HasProcessedOperatingPeriod && Network.LastOperatingMonth == month)
            {
                affordable = Network.LastProjectedOperatingExpense == 0 ||
                             Network.LastPaidOperatingExpense > 0;
                effectiveBudget = affordable ? Network.ActiveBudgetPermille : 0;
            }
            else
            {
                var projected = Network.CalculateOperatingExpense(
                    Parameters,
                    Network.ActiveBudgetPermille);
                affordable = _runtime.Finance.CanAfford(projected);
                var paid = 0;
                if (affordable && projected > 0)
                {
                    if (!_runtime.Finance.TrySpend(projected))
                    {
                        affordable = false;
                    }
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

        public void RunCurrentMonth()
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth)
                return;
            RunMonth(_runtime.TripDemandSystem.State.LastProcessedMonth);
        }

        /// <summary>
        /// Rebuilds only derived transit caches for an already-recorded operating
        /// period. It never charges funds, advances a month or applies a pending
        /// budget, so UI/read-model observation cannot create simulation results.
        /// </summary>
        public bool RefreshDerived()
        {
            if (!_runtime.TripDemandSystem.State.HasProcessedMonth ||
                !Network.HasProcessedOperatingPeriod ||
                Network.LastOperatingMonth != _runtime.TripDemandSystem.State.LastProcessedMonth)
            {
                return false;
            }

            var month = Network.LastOperatingMonth;
            var affordable = Network.LastProjectedOperatingExpense == 0 ||
                             Network.LastPaidOperatingExpense > 0;
            var effectiveBudget = affordable ? Network.ActiveBudgetPermille : 0;
            var changed = false;
            if (!ModeChoice.State.HasProcessedMonth || ModeChoice.State.LastProcessedMonth != month)
            {
                ModeChoice.RunMonth(month, effectiveBudget, affordable);
                PrivateVehicleRouting.RunMonth(month);
                changed = true;
            }
            else
            {
                changed = ModeChoice.RefreshIfNeeded(effectiveBudget, affordable);
                changed |= PrivateVehicleRouting.RefreshIfNeeded();
            }

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
