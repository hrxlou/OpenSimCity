# Public Services Coverage, Capacity and Budget Contract

## Scope

P7-01 implements one aggregate vertical slice for five public services:

- police
- fire
- school
- healthcare
- waste

A facility is persistent stable-ID state. Coverage, usage and warnings are deterministic monthly results. Facility GameObjects, icons and dispatched vehicles are never the source of truth.

## Coverage strategies

Two strategies remain distinct:

- `SpatialDistance`: school coverage uses Manhattan cell distance.
- `RoadTravelTime`: police, fire, healthcare and waste use the deterministic P5 representative road route and integer free-flow seconds.

A road-time demand requires a road-accessible building frontage, a road adjacent to the facility and the same directed road component. The public result preserves one of:

- `NoFacility`
- `NoRoadAccess`
- `DifferentRoadComponent`
- `OutOfRange`
- `CapacityExceeded`
- `BudgetTooLow`

UI and Presentation display these Application-projected reasons and do not repeat route or capacity calculations.

## Stable allocation

Facilities are ordered by `ServiceFacilityId`; building demand is ordered by stable `BuildingState.Id`. Candidate tie-breaks use travel cost then facility ID. Integer demand is allocated without creating citizens, pupils, patients, incidents or trucks.

For every building/service row:

```text
0 <= served <= requested
coveragePermille = served * 1000 / requested
```

Facility usage is the sum of assigned demand and cannot exceed effective capacity.

## Budget

Facility budget is `0..1500` permille and is scheduled for the next monthly boundary. It does not retrospectively change a settled month.

The active requested budget and currently affordable municipal funding combine as:

```text
effectiveBudget = activeBudget * availableFunding / 1000
effectiveCapacity = definitionCapacity * effectiveBudget / 1000
effectiveRange = definitionRange * max(250, effectiveBudget) / 1000
monthlyCost = definitionMonthlyCost * effectiveBudget / 1000
```

A budget reduction therefore changes capacity, range and cost in steps rather than toggling the facility off as a hidden binary. If the municipality cannot fund the requested total, all facilities receive a deterministic common funding scale and uncovered rows report `BudgetTooLow` when it reaches zero.

The current municipal ledger predates service expense categories. Construction and operating debits are temporarily accrued in the generic infrastructure-compatible road column so the opening/closing balance invariant remains valid. The service read model separately identifies the actual service cost. P9 release migration must not silently reinterpret historical rows when dedicated categories are introduced.

## Commands

- `PlaceServiceFacilityCommand`
- `RemoveServiceFacilityCommand`
- `SetServiceBudgetCommand`

Placement requires:

- known stable definition ID
- in-bounds, unoccupied cell
- adjacent road
- unique facility ID
- sufficient municipal funds

Preview does not spend money or add state. Commit uses `TrySpend`, adds one stable facility, records the classified expense and refunds on unexpected failure.

## Monthly composition

The Application service monthly system is registered in the post-trip-demand seam of `MunicipalFinanceMonthlySystem`:

```text
population/employment
  -> economy/growth
  -> trip demand
  -> public service demand/coverage/budget
  -> traffic/finance settlement
```

Service demand therefore observes current aggregate occupancy and employment. No facility view or coverage mesh is needed for the calculation.

## Persistence

`PublicServiceRuntimeSnapshotDto` schema 1 stores:

- facility stable ID
- definition ID
- cell
- active budget
- optional pending budget and effective month

Coverage rows, route costs, usage, effective funding, UI selection, pooled views and meshes are rebuilt from current buildings, roads, finance and facility state.

Restore requires unique ascending facility IDs, known definitions, valid budgets and in-bounds cells.

## Read models and presentation

`PublicServiceReadModelSnapshot` exposes:

- processed month
- requested/served total
- monthly service cost
- facility budget, capacity, usage, range and cost
- per-building coverage and explicit reason
- checksum

`PublicServiceViewCoordinator` owns all facility placeholders and one dynamic coverage mesh. Generated facility objects have no service `MonoBehaviour`. Coverage visibility and active service kind are transient Application selection state.

## Verification status

Committed fixtures cover stable allocation, conservation, capacity overflow, budget boundary scaling, zero-funding diagnostics and route-access failures. The CityPrototype fixture covers public placement, budget command, coverage overlay, pooled views, view/checksum independence and scene cleanup.

Local and self-hosted Unity execution is intentionally disabled while hardware is unavailable. Unity compilation, EditMode/PlayMode execution, actual rendering, UI scaling, profiler data and Windows/macOS parity remain `unverified`; no passing count or measured performance value is claimed.
