using System;
using OpenSimCity.Simulation.Districts;

namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictPolicySaveData
    {
        public int Format { get; set; } = 1;
        public bool HasProcessedMonth { get; set; }
        public int LastProcessedYear { get; set; }
        public int LastProcessedMonth { get; set; }
        public DistrictPolicySaveRow[] Items { get; set; } = Array.Empty<DistrictPolicySaveRow>();
    }

    public sealed class DistrictPolicySaveRow
    {
        public ulong DistrictId { get; set; }
        public DistrictPolicyKind Kind { get; set; }
        public bool Enabled { get; set; }
        public bool HasPending { get; set; }
        public bool PendingEnabled { get; set; }
        public int PendingYear { get; set; }
        public int PendingMonth { get; set; }
        public bool HasOperatingPeriod { get; set; }
        public int OperatingYear { get; set; }
        public int OperatingMonth { get; set; }
        public int ProjectedCost { get; set; }
        public int PaidCost { get; set; }
        public DistrictPolicyOperatingReason Reason { get; set; }
    }
}
