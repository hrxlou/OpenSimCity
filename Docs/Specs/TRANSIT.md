# Aggregate Bus Transit Contract

## Scope

P5-05 adds one aggregate bus mode. A `TransitStopId` and `TransitLineId` are stable runtime identities; a line stores an ordered, directional stop sequence. Citizens, passengers and rendered buses are never authoritative state.

## Monthly order

```text
Population / Economy / Growth
  -> TripDemand
  -> Transit budget effective boundary
  -> aggregate bus mode choice
  -> private-vehicle remainder routing
  -> transit operating expense evidence
```

The existing P5 traffic routing state remains a no-transit baseline. `TransitPrivateVehicleRoutingSystem` rebuilds the road volume from the conserved private-vehicle remainder. For every matched commute row:

```text
requested trips = bus trips + private-vehicle trips
```

Goods trips are not converted to bus travel.

## Mode choice

A line is eligible only when:

- an origin stop and a later destination stop are both within the integer walking radius;
- every consecutive stop pair has a directed road route;
- generalized bus cost is inside the configured ratio to the car free-flow cost;
- active budget and residual line capacity are positive.

Generalized cost is integer-only: walking + budget-derived waiting + line free-flow travel + stop dwell. OD rows and line capacity are consumed in stable OD/line order. The result exposes explicit reasons such as walking range, reverse direction, broken path, indirect service, disabled/unaffordable budget and exhausted capacity.

## Commands and finance

Stop placement/removal, line upsert/deletion and next-month budget changes use application commands. Transit construction and operating costs debit `MunicipalFinanceState`. Until the broader service ledger is introduced, the existing P3 ledger records these debits in its generic infrastructure/road construction compatibility bucket; `TransitNetworkState` separately preserves exact transit construction and operating amounts for explanation and migration.

## Player construction flow

`TransitConstructionTool` is an Application-owned, non-destructive state machine.

```text
select stop or line tool
  -> screen position projected to CellCoord by Gameplay
  -> command preview and explicit failure reason
  -> commit through TransitRuntimeModule
  -> stable stop/line state change
```

Stop placement uses a deterministic cell-derived stable ID. Line editing preserves the selected stop order. Duplicate stops, missing stops, broken directed road paths, invalid service units and insufficient funds are rejected before commit. Cancel discards only local draft state.

The keyboard defaults are `B` for stop placement, `N` for line editing, `Enter` to commit a line, `Backspace` to remove the last drafted stop and `Escape` to cancel. UI pointer/focus capture uses the shared `UiInputBoundary` contract.

## UI and overlay

UI and Presentation consume only Application DTOs:

- `TransitReadModel` provides stop/line counts, OD mode split, operating expense and baseline versus transit-aware road volume;
- `TransitOverlayReadModel` provides ordered route cells, stop markers, capacity/utilization and projected reasons;
- `TransitVehicleSamplingReadModel` selects a deterministic display-only subset of aggregate bus trips.

Simulation enums are projected to `TransitModeChoiceReasonView` before crossing the Application boundary. UI does not recalculate generalized cost or mode share.

## Presentation and pooling

`TransitViewCoordinator` owns one central update for every sampled bus. It reuses the existing behaviour-free `VehicleViewScheduler`; individual bus GameObjects contain no `MonoBehaviour`. Route lines and stop markers are pooled by `TransitOverlayRenderer` and rebuilt when source versions change. Scene roots are transient and never enter snapshots.

Changing the bus sample limit between 0 and 200 affects only presentation objects. It cannot change transit network, mode-choice or private-routing checksums.

## Persistence

`TransitRuntimeSnapshotDto` schema 1 stores only persistent stop, line, budget and transit-finance state. Route paths, mode-choice rows, overlays, pooled views and edge volumes are rebuilt from roads, OD demand and stable IDs after restore. The base `CitySnapshotDto` remains independently loadable; the release save envelope can combine both schemas without serializing Unity objects.

## Determinism

- stop and line collections are serialized by stable ID;
- stop order inside a line is preserved;
- all calculations use integers and declared truncation points;
- route tie-breaks use the existing deterministic route planner;
- read-model observation does not mutate state;
- display bus count cannot affect mode share or traffic volume;
- runtime bootstrap and scene observation are not simulation inputs.

## Experience gate

Two strategies remain viable and materially different:

- road upgrade keeps the same OD on faster/higher-capacity infrastructure and pays construction/maintenance costs;
- bus investment pays stop/line/operating costs and converts an accessible, capacity-limited subset of commute OD away from private vehicles.

A bus line is not a fixed-percentage traffic deletion. Low ridership exposes one of the named access, direction, cost, capacity or budget reasons in the public panel and overlay.

## Verification status

Committed EditMode fixtures cover OD conservation, budget boundaries, command rollback, non-destructive construction preview, display-sample independence, overlay projection, strategy comparison and capability snapshot round-trip. Committed PlayMode fixtures cover CityPrototype bootstrap, panel/input capture, pooled buses and scene unload. Unity compile and test execution remain `unverified` while local and self-hosted Unity hardware is unavailable.
