# District Policy Contract

P7-06B owns exactly two policies: `HeavyVehicleRestriction` and `TransitSubsidy`.

Policy commands schedule a desired state for `MonthlyScheduler.NextMonth`; settled periods are never rewritten. Active policy rows are ordered by stable district ID and policy kind. Removing a district prunes its policy rows before query, capture, or monthly processing.

## Funding

Default tuning is explicit in `DistrictPolicyParameters`:

- HeavyVehicleRestriction: municipal cost 20/month and goods-route preference 1600 permille.
- TransitSubsidy: municipal cost 80/month and bus generalized-cost input 800 permille.

At the monthly boundary pending states are applied first. The municipality then evaluates the full active-policy cost. This slice uses deterministic all-or-none funding: if the full total cannot be paid, active rows report `InsufficientFunding` and none of their transport inputs change for that period. There is no free policy effect.

The current municipal ledger has no policy expense category, so paid policy cost is temporarily recorded in the generic infrastructure-compatible column while the policy query exposes the classified amount separately.

## Traffic input

Simulation exposes an optional `ITrafficRoutePolicy`. The default identity returns 1000. A funded HeavyVehicleRestriction returns 1600 for goods routes crossing that district; commute stays 1000. `TrafficRoutingSystem` uses only routes in the minimum preference tier. Edge volume, congestion, route travel time, and district traffic-noise pressure remain derived results rather than direct policy bonuses.

## Transit input

Simulation exposes an optional `ITransitGeneralizedCostPolicy`. The identity implementation returns the already-calculated bus generalized cost unchanged. A funded TransitSubsidy returns 800 permille for commute ODs whose origin or destination building belongs to that district. Mode share remains owned by `TransitModeChoiceSystem`, with `busTrips + privateVehicleTrips == requestedTrips` preserved for each OD.

## Query and UI

`DistrictPolicyReadModel` exposes same-period population, jobs, routed trips, traffic-noise pressure, bus/private split, service coverage, pollution, paid policy cost, pending effective month, direct input factor and previous operating reason. The public panel distinguishes next-period settings from last-period outcomes and does not recalculate simulation values.

## Persistence

Format 1 stores active and pending state, pending effective month, last operating month, projected/paid cost and reason. Restore requires known district IDs, stable district/kind ordering and valid periods before replacing runtime state. Traffic/transit state is derived and refreshed rather than stored as a policy result.

## Verification status

Source fixtures cover next-month activation, insufficient funding, save/delete cleanup, route preference, transit generalized-cost conservation, UI presence and observation independence. Unity import/C# compile, fixture execution, visual/input QA and Windows/macOS parity remain unverified; no passing count or performance number is claimed.
