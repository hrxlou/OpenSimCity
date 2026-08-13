using System;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Queries
{
    /// <summary>
    /// Public application boundary for the period in which an upgrade preview is
    /// evaluated and for the route reason that would become visible in that period.
    /// UI does not infer either value from Simulation state.
    /// </summary>
    [Serializable]
    public sealed class RoadUpgradeImpactPublication
    {
        internal RoadUpgradeImpactPublication(
            RoadUpgradePreviewSnapshot impact,
            int appliesToYear,
            int appliesToMonth,
            string newReachabilityFailureReason)
        {
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            AppliesToYear = appliesToYear;
            AppliesToMonth = appliesToMonth;
            NewReachabilityFailureReason = newReachabilityFailureReason ?? string.Empty;
        }

        public RoadUpgradePreviewSnapshot Impact { get; }
        public int AppliesToYear { get; }
        public int AppliesToMonth { get; }
        public string NewReachabilityFailureReason { get; }
        public string AppliesToPeriodLabel => $"{AppliesToYear:D4}-{AppliesToMonth:D2}";
    }

    public sealed class RoadUpgradeImpactPublicationReadModel
    {
        private readonly CityRuntime _runtime;

        public RoadUpgradeImpactPublicationReadModel(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public RoadUpgradeImpactPublication Publish(RoadUpgradePreviewSnapshot impact)
        {
            if (impact == null)
                throw new ArgumentNullException(nameof(impact));

            var month = _runtime.CurrentSimulationMonth;
            return new RoadUpgradeImpactPublication(
                impact,
                month.Year,
                month.Month,
                RoadUpgradePreviewFormatting.PrimaryFailureLabel(impact));
        }
    }
}
