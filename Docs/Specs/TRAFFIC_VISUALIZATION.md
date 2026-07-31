# Traffic Visualization, Vehicle Sampling and Congestion Datamap Contract

This document records the P5-03 presentation contract built on top of the aggregate
routing state defined in `TRAFFIC_ROUTING.md`.

## Boundary

The source of truth remains `TrafficRoutingSystem` and its route assignments, edge
volumes, capacities and travel times. Vehicle GameObjects and datamap meshes are
strictly derived views.

```text
TrafficRoutingSystem
    ↓ TrafficRoutingReadModel
TrafficVisualizationReadModel
    ├─ immutable edge / bottleneck / OD impact rows
    ├─ deterministic vehicle samples
    └─ unrouted reason rows
        ↓
TrafficViewCoordinator
    ├─ VehicleViewScheduler + VehicleViewPool
    └─ one TrafficCongestionDataMap mesh
```

Presentation code does not reference private Simulation collections and never writes
vehicle counts, positions or datamap values back into routing state.

## Vehicle sample limits

`TrafficVisualizationReadModel` accepts a visible-sample limit in the inclusive range
0 through 500. The default is 100.

The target sample count is:

```text
min(configured sample limit, total routed aggregate trip amount)
```

Changing this limit rebuilds only the presentation read model. The source routing
version and `TrafficRoutingState.ComputeChecksum()` remain unchanged.

## Deterministic sample allocation

Samples are distributed across route assignments by weighted largest remainder:

1. `assignment amount * target samples / total routed amount` gives the base count;
2. remaining samples are assigned by descending fractional remainder;
3. equal remainders use stable route-assignment order;
4. each sample receives a positive represented-trip weight;
5. represented-trip weights sum exactly to routed aggregate demand.

Sample IDs use a presentation-only stable namespace and an FNV-1a hash of flow kind,
origin ID, destination ID, route index and sample ordinal. They are reproducible but
are not persistent simulation entity IDs.

A phase offset is assigned in sample order so views are spread over their routes.
Phase offsets affect only visual position.

## Congestion-weighted motion

Each sampled path copies immutable route cells and the current congested seconds for
every edge. `VehicleSamplePathData.Evaluate()` uses cumulative integer segment times,
so visually slower edges consume a larger fraction of the loop. This is a view of the
aggregate congestion result; it does not create a new vehicle travel-time model.

## Central scheduler and pool

`TrafficViewCoordinator` is the only traffic presentation MonoBehaviour with an
`Update()` loop. It owns:

- one `TrafficVisualizationReadModel`;
- one `VehicleViewScheduler`;
- one `VehicleViewPool`;
- one `TrafficCongestionMeshRenderer`.

Pooled vehicle GameObjects must contain no MonoBehaviour. They do not own pathfinding,
OD identity, economy state or per-vehicle `Update()` methods. The pool deactivates and
reuses views when the sample limit or source route set shrinks. Scene unload destroys
the transient root and every active or pooled view.

## Datamap and diagnostics

The application projection exposes:

- edge volume, capacity and volume/capacity permille;
- free-flow and effective speed;
- free-flow, congested and delay seconds;
- bottlenecks ordered by ratio, delay and canonical edge key;
- OD amount, route count and weighted average free-flow/congested time;
- explicit unrouted reasons and endpoint cells.

The presentation renderer combines all edge quads and unrouted markers into one
dynamic mesh with 32-bit indices. It does not create a GameObject per edge.

Colors are only a view convention. Numerical values and reasons remain available in
the read model so UI and accessibility work do not depend on color alone.

## Cache and road-edit behavior

A public road edit refreshes routing first. On the next coordinator update, the
visualization model observes the new routing version, resizes the pool, rebuilds the
single datamap mesh and removes every sampled path containing a stale demolished road
cell.

The experience fixture verifies that adding a second connection lowers the top
volume/capacity ratio and weighted OD travel time without changing total routed
demand.

## Persistence

The following are not written to snapshot schema v1:

- vehicle sample IDs and phase;
- sample paths and visual progress;
- active or pooled GameObjects;
- datamap mesh, materials and visibility;
- presentation configuration such as visible sample limit.

After restore, identical road, building, economy and trip-demand sources rebuild an
identical routing checksum and deterministic sample sequence.

## Profiler markers

The production presentation path exposes these markers:

- `OpenSimCity.TrafficViews.Update`
- `OpenSimCity.TrafficVehicles.UpdatePositions`
- `OpenSimCity.TrafficVehicles.PoolResize`
- `OpenSimCity.TrafficDataMap.Rebuild`

The Benchmark Scene fixture records the 0/100/500 view cases. A graphics-enabled
Windows profiler run may use the markers for CPU, allocation and draw-call analysis.
The CI runner uses a null graphics device, so an unavailable draw-call counter is
reported explicitly rather than inferred.
