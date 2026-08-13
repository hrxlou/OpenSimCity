using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Transit;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;
using OpenSimCity.Simulation.Districts;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictPolicyRuntimeModule : IMonthlySimulationSystem
    {
        internal DistrictPolicyRuntimeModule(CityRuntime runtime, DistrictPolicyParameters parameters = null)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Districts = DistrictRuntimeRegistry.GetOrCreate(runtime);
            Parameters = parameters ?? DistrictPolicyParameters.Default;
            Policies = new DistrictPolicyCollection();
            Commands = new DistrictPolicyCommandHandler(runtime, Districts, Policies);
            RouteInput = new DistrictRouteInput(Districts.Districts, Policies, Parameters);
            TransitInput = new DistrictTransitInput(runtime, Districts.Districts, Policies, Parameters);
            runtime.TrafficRoutingSystem.RoutePolicy = RouteInput;
            runtime.MunicipalFinanceSystem.RegisterPostTripDemandSystem(this);
        }

        public CityRuntime Runtime { get; }
        public DistrictRuntimeModule Districts { get; }
        public DistrictPolicyParameters Parameters { get; }
        public DistrictPolicyCollection Policies { get; }
        public DistrictPolicyCommandHandler Commands { get; }
        public DistrictRouteInput RouteInput { get; }
        public DistrictTransitInput TransitInput { get; }
        public bool HasProcessedMonth { get; private set; }
        public SimulationDate LastProcessedMonth { get; private set; }
        public int Version { get; private set; }

        public CommandExecutionResult Execute(ICityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!Commands.CanHandle(command)) return CommandExecutionResult.Unsupported("Unsupported district policy command.");
            var sequence = Runtime.Execute(command);
            if (sequence.Status != CommandExecutionStatus.Unsupported) return sequence;
            return Commands.Execute(new CommandEnvelope(sequence.Stamp, command)).WithStamp(sequence.Stamp);
        }

        public void RunMonth(SimulationDate month)
        {
            if (HasProcessedMonth && month.CompareTo(LastProcessedMonth) <= 0)
                throw new InvalidOperationException("District policy months must be strictly increasing.");
            PruneMissingDistricts();
            var rows = Policies.Rows;
            var stateChanged = false;
            for (var index = 0; index < rows.Count; index++)
            {
                var beforePending = rows[index].HasPending;
                var beforeEnabled = rows[index].IsEnabled;
                rows[index].ApplyPending(month);
                stateChanged |= beforePending != rows[index].HasPending || beforeEnabled != rows[index].IsEnabled;
            }
            if (stateChanged) Policies.Touch();

            long total = 0;
            for (var index = 0; index < rows.Count; index++)
                if (rows[index].IsEnabled) total += Parameters.MonthlyCost(rows[index].Kind);
            if (total > int.MaxValue) throw new OverflowException("District policy expense exceeded Int32 range.");
            var projectedTotal = (int)total;
            var funded = projectedTotal == 0 || Runtime.Finance.CanAfford(projectedTotal);
            var paidTotal = 0;
            if (funded && projectedTotal > 0)
            {
                funded = Runtime.Finance.TrySpend(projectedTotal);
                if (funded)
                {
                    paidTotal = projectedTotal;
                    Runtime.Finance.RecordConstructionExpense(MunicipalConstructionCategory.Road, projectedTotal);
                }
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var projected = row.IsEnabled ? Parameters.MonthlyCost(row.Kind) : 0;
                var paid = row.IsEnabled && funded ? projected : 0;
                var reason = row.IsEnabled
                    ? funded ? DistrictPolicyOperatingReason.None : DistrictPolicyOperatingReason.InsufficientFunding
                    : row.HasPending ? DistrictPolicyOperatingReason.Pending : DistrictPolicyOperatingReason.Disabled;
                row.RecordOperatingPeriod(month, projected, paid, reason);
            }
            if (rows.Count > 0 || paidTotal > 0) Policies.Touch();
            LastProcessedMonth = month;
            HasProcessedMonth = true;
            Version = checked(Version + 1);
        }

        public DistrictPolicySaveData Capture()
        {
            PruneMissingDistricts();
            var source = Policies.Rows;
            var rows = new DistrictPolicySaveRow[source.Count];
            for (var index = 0; index < rows.Length; index++)
            {
                var state = source[index];
                rows[index] = new DistrictPolicySaveRow
                {
                    DistrictId = state.DistrictId.Value,
                    Kind = state.Kind,
                    Enabled = state.IsEnabled,
                    HasPending = state.HasPending,
                    PendingEnabled = state.PendingEnabled,
                    PendingYear = state.HasPending ? state.PendingEffectiveMonth.Year : 0,
                    PendingMonth = state.HasPending ? state.PendingEffectiveMonth.Month : 0,
                    HasOperatingPeriod = state.HasOperatingPeriod,
                    OperatingYear = state.HasOperatingPeriod ? state.LastOperatingMonth.Year : 0,
                    OperatingMonth = state.HasOperatingPeriod ? state.LastOperatingMonth.Month : 0,
                    ProjectedCost = state.LastProjectedCost,
                    PaidCost = state.LastPaidCost,
                    Reason = state.LastReason
                };
            }
            return new DistrictPolicySaveData
            {
                Format = 1,
                HasProcessedMonth = HasProcessedMonth,
                LastProcessedYear = HasProcessedMonth ? LastProcessedMonth.Year : 0,
                LastProcessedMonth = HasProcessedMonth ? LastProcessedMonth.Month : 0,
                Items = rows
            };
        }

        public void Restore(DistrictPolicySaveData data)
        {
            if (data == null || data.Format != 1) throw new InvalidOperationException("Unsupported district policy save format.");
            var source = data.Items ?? Array.Empty<DistrictPolicySaveRow>();
            var restored = new List<DistrictPolicyState>(source.Length);
            ulong previousId = 0;
            var previousKind = DistrictPolicyKind.HeavyVehicleRestriction;
            var hasPrevious = false;
            for (var index = 0; index < source.Length; index++)
            {
                var row = source[index] ?? throw new ArgumentException("District policy row cannot be null.", nameof(data));
                var id = new StableId(row.DistrictId);
                if (!Districts.Districts.TryGet(id, out _)) throw new ArgumentException("Policy references an unknown district.", nameof(data));
                if (!Enum.IsDefined(typeof(DistrictPolicyKind), row.Kind) || !Enum.IsDefined(typeof(DistrictPolicyOperatingReason), row.Reason))
                    throw new ArgumentException("Policy enum value is invalid.", nameof(data));
                if (hasPrevious && (row.DistrictId < previousId || (row.DistrictId == previousId && row.Kind <= previousKind)))
                    throw new ArgumentException("Policy rows require stable district/kind ordering.", nameof(data));
                var pending = row.HasPending ? new SimulationDate(row.PendingYear, row.PendingMonth) : default;
                var operating = row.HasOperatingPeriod ? new SimulationDate(row.OperatingYear, row.OperatingMonth) : default;
                var state = new DistrictPolicyState(id, row.Kind);
                state.Restore(row.Enabled, row.HasPending, row.PendingEnabled, pending, row.HasOperatingPeriod,
                    operating, row.ProjectedCost, row.PaidCost, row.Reason);
                restored.Add(state);
                previousId = row.DistrictId; previousKind = row.Kind; hasPrevious = true;
            }
            var processedMonth = data.HasProcessedMonth ? new SimulationDate(data.LastProcessedYear, data.LastProcessedMonth) : default;
            Policies.Replace(restored);
            HasProcessedMonth = data.HasProcessedMonth;
            LastProcessedMonth = processedMonth;
            Version = checked(Version + 1);
            Runtime.TrafficRoutingSystem.RefreshIfNeeded();
            if (TransitRuntimeRegistry.TryGet(Runtime, out var transit)) transit.RefreshDerived();
        }

        public void PruneMissingDistricts()
        {
            var removed = new HashSet<StableId>();
            var rows = Policies.Rows;
            for (var index = 0; index < rows.Count; index++)
                if (!Districts.Districts.TryGet(rows[index].DistrictId, out _)) removed.Add(rows[index].DistrictId);
            foreach (var id in removed) Policies.RemoveDistrict(id);
        }
    }

    public static class DistrictPolicyRuntimeRegistry
    {
        private static readonly ConditionalWeakTable<CityRuntime, DistrictPolicyRuntimeModule> Modules = new ConditionalWeakTable<CityRuntime, DistrictPolicyRuntimeModule>();
        public static DistrictPolicyRuntimeModule GetOrCreate(CityRuntime runtime, DistrictPolicyParameters parameters = null)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (Modules.TryGetValue(runtime, out var existing)) return existing;
            var created = new DistrictPolicyRuntimeModule(runtime, parameters); Modules.Add(runtime, created); return created;
        }
        public static bool TryGet(CityRuntime runtime, out DistrictPolicyRuntimeModule module)
        {
            if (runtime == null) { module = null; return false; }
            return Modules.TryGetValue(runtime, out module);
        }
    }
}
