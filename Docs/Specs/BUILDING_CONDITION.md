# Building Condition, Abandonment and Redevelopment Contract

## Purpose

P6-02 converts persistent spatial and infrastructure pressure into delayed building condition changes. A rendered damaged facade, warning marker or selected inspector never owns a timer or transition. Condition state is pure Simulation data keyed by the existing stable `BuildingState.Id`.

## Monthly order

The Application scheduler executes registered condition systems before the existing core monthly systems:

```text
spatial source/cache refresh
  -> building condition factors and stage transition
  -> effective housing/job capacity
  -> population/employment allocation
  -> economy, RCI, growth, trip demand, traffic and finance
```

A stage change therefore limits capacity in the same monthly period, but never mutates occupancy every frame. The default population resolver remains pass-through when no condition capability is registered.

## Stages and timing

```text
Healthy
  -> Stressed
  -> Declining
  -> Abandoned
  -> Redeveloping
  -> Healthy
```

Default timing:

- new-building occupancy grace: 2 processed months
- continuous adverse pressure before `Stressed`: 2 months
- continuous adverse pressure before `Declining`: 4 months total
- continuous adverse pressure before `Abandoned`: 7 months total
- recovery from `Declining` to `Stressed`: 2 recovered months
- recovery from `Stressed` to `Healthy`: 2 additional recovered months
- recovered abandoned-building eligibility delay: 2 months
- redevelopment duration after command commit: 3 months

Recovery is intentionally not immediate. Losing a required condition while redevelopment is in progress pauses progress and preserves the explicit block reason.

## Factors and ordering

Each building receives a public integer factor list from the same processed month. Positive contributions are:

```text
NoRoadAccess  = 500 when parcel frontage/external access is unavailable
NoPower       = 500 when the building is unpowered
Pollution     = max(0, pollution - 200)
Crime         = max(0, crime - 200)
Noise         = max(0, noise - 250)
LowLandValue  = max(0, 400 - land value)
LowOccupancy  = max(0, 500 - occupancy permille)
```

The aggregate adverse score is clamped to `0..2000`. Factors are ordered by contribution descending; ties use the declared reason enum order. The first row is the top reason shown by both the selected-building inspector and condition datamap.

Low occupancy is ignored during the initial grace period and while a building is abandoned or redeveloping, preventing the capacity reduction itself from becoming a self-reinforcing hidden cause.

Default thresholds:

- adverse/stress score: 250
- severe score: 650
- recovered score: below 180

## Effective capacity

Condition does not replace static building definitions. It supplies an `IBuildingCapacityResolver` that may only reduce definition capacity:

- `Healthy`: 1000 permille
- `Stressed`: 800 permille
- `Declining`: 500 permille
- `Abandoned`: 0
- `Redeveloping`: 0

Population validates that a resolver never returns a negative value or a value above the definition capacity. Forced departures are recorded in the aggregate migration result when effective housing capacity falls below current occupancy.

## Environment query and use tolerance

Application resolves each building footprint from:

- P6-01 land value, pollution, noise and crime fields
- parcel road frontage and external road accessibility
- deterministic component power allocation

Footprint values are averaged with integer division. Road/building field sources remain derived from authoritative state, while custom park, pollution, crime or civic sources use the P6-01 command and snapshot boundary.

Ordinary road noise uses a low per-cell contribution so a normal connected street does not by itself saturate every frontage. Explicit dense or high-volume traffic pressure can later replace that representative source through the existing field input contract.

Industrial emitters remain fully visible to surrounding cells. For the emitting industrial building only, the condition adapter applies the named industrial-use tolerance:

```text
land value used by condition = max(400, raw land value)
pollution used by condition = raw pollution / 3
noise used by condition = raw noise / 2
crime, road access and power = unchanged
```

This prevents a factory from abandoning itself solely because it produces its own defined field. It does not remove or weaken that field for nearby residential or commercial buildings.

## Abandonment and redevelopment

An abandoned building must satisfy all of the following before the command becomes eligible:

- parcel still has road frontage and external access
- building is powered
- environmental score is below the recovery threshold
- recovered conditions have remained valid for the configured delay

`RequestBuildingRedevelopmentCommand` previews eligibility, reason, cost and funds without changing state. Commit debits municipal funds atomically and starts the deterministic duration. A failed commit refunds funds and leaves the building abandoned.

The current P3 ledger has only road and power construction columns. Until P7 expands service/infrastructure categories, redevelopment cost is classified in the generic infrastructure-compatible road construction bucket and remains explicitly identified in the command result. This compatibility decision must be migrated, not silently reinterpreted, when a dedicated redevelopment ledger category is added.

Redevelopment preserves:

- stable building instance ID
- definition ID
- parcel ID
- anchor
- footprint

It resets condition age/timers on completion. It does not demolish and respawn the building through Presentation.

## Commands and read models

Application provides:

- `BuildingConditionCommandHandler`
- `BuildingConditionReadModel`
- `BuildingConditionOverlayReadModel`
- transient selected-building and datamap selections

UI and Presentation consume Application-only stage, reason and block enums. They do not inspect Simulation collections or recalculate factors.

The selected inspector displays:

- processed month
- stage and remaining transition/redevelopment time
- current occupancy or employment versus effective capacity
- ordered factor contributions
- top reason and actionable next step
- redevelopment eligibility or explicit block reason

The datamap uses the same read-model version and period. Pattern channels are:

- healthy: solid
- stressed: dots
- declining: hatch
- abandoned: cross-hatch
- redeveloping: pulse

## Presentation

`BuildingConditionViewCoordinator` centrally owns pooled stage markers and one dynamic datamap mesh. Generated building markers contain no `MonoBehaviour`. Stage-specific shape, rotation, inset and label/pattern metadata supplement color. Redevelopment pulse height is calculated from an absolute world-scale baseline each frame, so animation cannot accumulate transform drift.

Marker pools, datamap mesh, active selection, panel state and animation phase are transient. Their visibility or update count cannot change a condition checksum or monthly transition.

## Persistence

`BuildingConditionRuntimeSnapshotDto` schema 1 stores:

- last processed month
- stable building ID
- stage
- age, adverse and recovery counters
- redevelopment remaining months
- last adverse score
- ordered factors and top reason
- eligibility and block reason

Restore rejects unknown building references, unknown enums, duplicate/non-ordered IDs and unsupported schema versions. Environment fields, parcel/power queries, read models, overlay cells, marker pools and meshes are rebuilt.

## Determinism

- buildings are processed by stable building ID
- factors have explicit descending-contribution/tie ordering
- all thresholds, scores and capacities are integer-only
- scheduler registration order is the capability tie-breaker
- command sequence is shared with the city runtime
- save/load preserves timers and next-month outcome
- selected inspector, datamap and view observation are not simulation inputs

## Experience gate

The committed scenario follows one residential stable ID through:

```text
pollution source added
  -> seven delayed adverse months
  -> abandonment and zero effective capacity
  -> source removal
  -> two recovered months
  -> explicit redevelopment command
  -> three-month redevelopment
  -> healthy state with the same ID and footprint
```

The inspector and datamap must report the same stage, top reason and processed period throughout.

## Verification status

Committed EditMode fixtures cover transition/recovery timing, stable factor ordering, effective population capacity, snapshot continuation, command rollback and redevelopment identity. A committed PlayMode fixture covers the full problem-to-recovery experience, inspector/datamap agreement, pooled marker lifecycle and scene unload.

Unity import, compilation, test execution, visual QA, profiler measurements and Windows/macOS parity remain `unverified` while local and self-hosted Unity hardware is unavailable. No passing test count or runtime performance value is claimed.
