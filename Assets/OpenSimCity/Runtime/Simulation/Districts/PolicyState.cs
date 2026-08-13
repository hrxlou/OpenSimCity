using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Districts
{
    [Serializable]
    public sealed class DistrictPolicyState
    {
        public DistrictPolicyState(StableId districtId, DistrictPolicyKind kind)
        {
            if (!districtId.IsValid) throw new ArgumentException("Policy requires a district ID.", nameof(districtId));
            DistrictId = districtId;
            Kind = kind;
        }

        public StableId DistrictId { get; }
        public DistrictPolicyKind Kind { get; }
        public bool IsEnabled { get; private set; }
        public bool HasPending { get; private set; }
        public bool PendingEnabled { get; private set; }
        public SimulationDate PendingEffectiveMonth { get; private set; }
        public bool HasOperatingPeriod { get; private set; }
        public SimulationDate LastOperatingMonth { get; private set; }
        public int LastProjectedCost { get; private set; }
        public int LastPaidCost { get; private set; }
        public DistrictPolicyOperatingReason LastReason { get; private set; } = DistrictPolicyOperatingReason.Disabled;

        public bool IsFundedAndEffective =>
            IsEnabled && HasOperatingPeriod && LastReason == DistrictPolicyOperatingReason.None && LastPaidCost == LastProjectedCost;

        public void Schedule(bool enabled, SimulationDate effectiveMonth)
        {
            HasPending = true;
            PendingEnabled = enabled;
            PendingEffectiveMonth = effectiveMonth;
        }

        public void ApplyPending(SimulationDate month)
        {
            if (!HasPending || month.CompareTo(PendingEffectiveMonth) < 0) return;
            IsEnabled = PendingEnabled;
            HasPending = false;
            PendingEnabled = false;
            PendingEffectiveMonth = default;
        }

        public void RecordOperatingPeriod(SimulationDate month, int projectedCost, int paidCost, DistrictPolicyOperatingReason reason)
        {
            if (projectedCost < 0 || paidCost < 0 || paidCost > projectedCost) throw new ArgumentOutOfRangeException(nameof(projectedCost));
            LastOperatingMonth = month;
            HasOperatingPeriod = true;
            LastProjectedCost = projectedCost;
            LastPaidCost = paidCost;
            LastReason = reason;
        }

        public void Restore(bool enabled, bool hasPending, bool pendingEnabled, SimulationDate pendingMonth,
            bool hasOperatingPeriod, SimulationDate operatingMonth, int projected, int paid, DistrictPolicyOperatingReason reason)
        {
            if (projected < 0 || paid < 0 || paid > projected) throw new ArgumentOutOfRangeException(nameof(projected));
            IsEnabled = enabled;
            HasPending = hasPending;
            PendingEnabled = hasPending && pendingEnabled;
            PendingEffectiveMonth = hasPending ? pendingMonth : default;
            HasOperatingPeriod = hasOperatingPeriod;
            LastOperatingMonth = hasOperatingPeriod ? operatingMonth : default;
            LastProjectedCost = hasOperatingPeriod ? projected : 0;
            LastPaidCost = hasOperatingPeriod ? paid : 0;
            LastReason = hasOperatingPeriod ? reason : DistrictPolicyOperatingReason.Disabled;
        }
    }
}
