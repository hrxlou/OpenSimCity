# District State Contract

P7-06A introduces persistent administrative districts without changing roads, zones, parcels, buildings, or simulation outputs by itself.

## Source state

Each district owns a stable ID, a validated player-facing name, an RGBA display value, and a row-major ordered set of cells. A cell can belong to at most one district. District IDs are stored in ascending order.

Paint commands validate the complete normalized cell set before mutation. If any cell is out of bounds or owned by another district, the entire paint is rejected. Erase only removes membership owned by the target district. Deleting a district removes all of its memberships.

## Application boundary

Create, rename, color, paint, erase, and delete are Application commands. `DistrictRuntimeModule.Execute` shares the base `CityRuntime` command sequence. UI and Presentation consume `DistrictReadModelSnapshot`; they do not own or mutate Simulation collections.

`DistrictSelection` is transient edit state. Selected cell, active district, and overlay visibility are not persisted and do not change district source state.

## Persistence

District save format 1 stores only stable district rows and cell membership. Restore validates format, ascending non-zero IDs, names, unique cells, world bounds, and cross-district overlap before replacing the current collection. Boundary meshes and UI selection are rebuilt.

## Presentation

One central line mesh renders district boundaries. Internal edges between cells of the same district are omitted and shared boundary segments are emitted once. There is no per-cell or per-district `MonoBehaviour`.

## Verification status

Committed EditMode and PlayMode fixtures cover atomic overlap rejection, membership release on delete, save/restore validation, the 2x2 eight-segment boundary, panel presence, and overlay/view independence. Unity import/compile, fixture execution, visual QA, input feel, and cross-platform parity remain unverified until run on the required Unity environments.
