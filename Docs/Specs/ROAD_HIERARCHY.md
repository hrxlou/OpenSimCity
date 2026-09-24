# Road hierarchy, upgrade, direction, and persistence contract

This document is the P5-04A contract for authoritative road state. UI and Presentation consume the application read model; they do not own road tier, direction, routing, or cost rules.

## Stable definitions

| Tier | Stable definition ID | Build cost | Monthly maintenance | Capacity/month | Free-flow speed (cells/hour) | Noise |
|---|---:|---:|---:|---:|---:|---:|
| Small road | `0x0100000000000002` | 6 | 0 | 500 | 20 | 15 |
| Street | `0x0100000000000001` | 10 | 1 | 1,000 | 30 | 30 |
| Avenue | `0x0100000000000003` | 25 | 5 | 2,400 | 45 | 70 |
| Boulevard | `0x0100000000000004` | 38 | 8 | 3,800 | 50 | 95 |

The pre-P5 street keeps stable ID `0x0100000000000001` and its existing cost, maintenance, capacity, speed, noise, and presentation key. New tiers use new IDs. The boulevard is a higher-capacity six-lane arterial above the four-lane avenue. A definition ID must not silently acquire different historical semantics.

Road tiers affect simulation only through the listed inputs. They do not grant direct residential, commercial, industrial, land-value, or demand bonuses.

## Authoritative road-cell state

Every occupied road cell stores:

- cell coordinate and row-major cell index,
- stable road definition ID,
- `RoadTravelDirection`.

Road occupancy remains the same authoritative cell identity when definition or direction changes. Upgrade does not demolish and recreate the cell.

`RoadHierarchyReadModel` projects occupied cells in deterministic row-major order and copies cost, maintenance, capacity, speed, noise, presentation key, and direction from the immutable definition catalog. UI and Presentation must not recompute those values.

## Direction semantics

`Bidirectional` permits exits to every occupied cardinal neighbor. `North`, `East`, `South`, and `West` permit leaving the cell only in the named direction.

Direction is an **exit rule**. A directed cell may still be entered from another cell if the source cell permits that move. This rule is used consistently by deterministic routing and by later direction visualization.

Forward and reverse upgrade modes derive cardinal direction from an axis-aligned path:

- forward: start toward end,
- reverse: end toward start,
- bidirectional: no cardinal restriction.

A one-way upgrade requires distinct axis-aligned endpoints. Undefined enum values are invalid snapshot or command state.

## Upgrade command

`UpgradeRoadCommand` is the only P5-04A write path for changing definition or direction on an existing road path.

Validation requires:

- a non-empty, duplicate-free, in-bounds, straight cardinal path,
- an occupied road at every target cell,
- a registered target definition,
- a valid direction mode,
- at least one resulting definition or direction change,
- sufficient municipal funds for the complete batch.

The command replaces definition and direction atomically. A failure changes no road cell, road version, funds, or derived cache state.

## Cost and finance

Upgrade cost is calculated per cell and summed with checked integer arithmetic:

```text
cell cost = max(0, target build cost - current build cost)
command cost = sum(cell cost)
```

Downgrade and direction-only changes receive no refund. A successful command records the charged amount as road construction expense. If state changes between preview and commit, spent funds are refunded and no partial replacement remains.

## Directed routing and cache invalidation

The deterministic route planner runs directed Dijkstra over cardinal road-cell exits. Stable tie-breaking remains based on complete cell sequence and deterministic cell ordering.

A route request that cannot follow directed exits returns an explicit no-path result rather than a stale or reverse-invalid route.

Road mutation increments the authoritative road version. The route planner invalidates cached entries whose prior source/destination component intersects the changed undirected road component. Cache entries for unrelated components remain reusable. Route cache contents and invalidation bookkeeping are derived state and are not persisted.

## Snapshot v2 and migration

Snapshot format v2 adds `RoadCellSnapshotDto.TravelDirection` after `DefinitionId`. Definition ID and direction round-trip for every occupied cell.

Version 1 did not contain direction. The v1-to-v2 migration therefore assigns `Bidirectional` to every occupied road cell and updates the header to version 2. The migration:

- validates codec decode and encode results,
- rejects invalid magic or source version,
- returns the application migration failure contract instead of leaking codec exceptions,
- does not rewrite the source save automatically during load.

The standard `CityPersistenceService` composition includes the v1-to-v2 step. Product platform composition uses the same application migration contract.

Snapshots persist authoritative road cells, stable definition IDs, direction, command sequence, and timeline state. Connectivity graphs, route caches, overlays, meshes, pooled vehicles, and other rebuildable presentation or query state are reconstructed after restore.

## Verification boundary

P5-04A is verified by:

- stable definition and monotonic trade-off tests,
- no-demolition upgrade, cost, insufficient-funds, no-change, and atomic rollback tests,
- forward/reverse reachability and explicit no-path tests,
- affected-component invalidation and unrelated-component retention tests,
- v1 golden migration and v2 definition/direction round-trip tests,
- schema declaration-order checksum and canonical JSON checksum tests,
- 128×128 upgrade and directed-route benchmark,
- Simulation assembly Unity-reference guard.

Preview/commit/cancel interaction, direction arrows/patterns, metric comparison UI, congestion-overlay integration, and the playable bottleneck-recovery fixture belong to P5-04B.
