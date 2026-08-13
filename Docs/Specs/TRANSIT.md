# Aggregate Bus Transit Contract

## Scope

P5-05A adds one aggregate bus mode. A `TransitStopId` and `TransitLineId` are stable runtime identities; a line stores an ordered, directional stop sequence. Citizens, passengers and rendered buses are never authoritative state.

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

## Persistence

`TransitRuntimeSnapshotDto` schema 1 stores only persistent stop, line, budget and transit-finance state. Route paths, mode-choice rows and edge volumes are rebuilt from roads, OD demand and stable IDs after restore. The base `CitySnapshotDto` remains independently loadable; the release save envelope can combine both schemas without serializing Unity objects.

## Determinism

- stop and line collections are serialized by stable ID;
- stop order inside a line is preserved;
- all calculations use integers and declared truncation points;
- route tie-breaks use the existing deterministic route planner;
- read-model observation does not mutate state;
- display bus count cannot affect mode share or traffic volume.

## Verification status

The committed EditMode fixtures cover OD conservation, budget boundaries, command rollback, observation independence and capability snapshot round-trip. Unity compile and test execution are `unverified` while local and self-hosted Unity hardware is unavailable.
