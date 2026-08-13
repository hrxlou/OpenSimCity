using System;

namespace OpenSimCity.Simulation.Traffic
{
    public interface ITrafficRoutePolicy
    {
        int Version { get; }
        int RoutePreferencePermille(TripDemandKind kind, TrafficRoute route);
    }

    public sealed class IdentityTrafficRoutePolicy : ITrafficRoutePolicy
    {
        public static readonly IdentityTrafficRoutePolicy Instance = new IdentityTrafficRoutePolicy();
        private IdentityTrafficRoutePolicy() { }
        public int Version => 0;
        public int RoutePreferencePermille(TripDemandKind kind, TrafficRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            return 1000;
        }
    }
}
