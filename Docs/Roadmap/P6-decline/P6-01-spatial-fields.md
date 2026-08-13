# P6-01 - Land value, pollution, noise and crime spatial fields

Issue: #28

## Implemented capability

- 0..1000 integer land-value, pollution, noise, crime and park-amenity channels
- Manhattan linear falloff, additive composition and explicit clamp order
- stable source definitions and source IDs
- sorted dirty-cell propagation with old/new affected regions
- exact refreshed-cell and touched-chunk diagnostics
- residential/commercial/industrial suitability factors
- derived road-noise and industrial-emitter sources
- custom source application commands and schema-1 capability snapshot
- versioned read model, five-band overlay DTO and text legend
- single dynamic mesh renderer with band-dependent geometry
- gameplay runtime host, UI selector and scene lifecycle

## Static evidence

- Simulation contains no Unity, Scene, Material, UI or renderer dependency.
- Source iteration and dirty evaluation have explicit stable ordering.
- Capability snapshots omit cell caches, dirty sets, read models and mesh state.
- Derived road/building sources are excluded from saved custom source rows.
- UI and Presentation consume Application DTOs and cannot write field arrays.
- The 256x256 fixture evaluates 81 dirty cells for one radius-4 source versus 65,536 cells for a full rebuild contract.

The evaluated-cell comparison is algorithmic evidence only, not a measured CPU speedup.

## Committed fixtures

- `SpatialFieldSystemTests`
- `SpatialFieldOverlayPlayModeTests`
- `Docs/Specs/SPATIAL_FIELDS.md`

## Unverified hardware evidence

Local and self-hosted Unity execution is disabled while both machines are unavailable.

- `unverified`: Unity import and C# compilation
- `unverified`: EditMode/PlayMode execution
- `unverified`: actual dynamic-mesh rendering and legend layout
- `unverified`: profiler marker duration, allocation and frame cost
- `unverified`: Windows/macOS field checksum comparison
- `unverified`: visual accessibility and Korean font fallback

No passing test count or performance time is claimed.
