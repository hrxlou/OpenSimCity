# P5-05A - Transit state, mode choice, finance and persistence

Issue: #104

## Implemented capability

- stable stop/line IDs and directional ordered stop state
- placement/removal/upsert/delete/budget application commands
- walking access, route continuity, generalized-cost and capacity mode choice
- conserved bus/private commute split
- transit-aware private-vehicle route and edge aggregation
- municipal balance debit plus transit-specific expense evidence
- schema-versioned capability snapshot and deterministic rebuild
- presentation-safe transit read model

## Static evidence

- Simulation code has no Unity, UI, Scene, prefab or renderer reference.
- Commands consume the base runtime command sequence through an explicit module boundary.
- Mode-choice state and private traffic are derived from stable road/OD/transit sources.
- Tests are committed for conservation, reasons, budget timing, view independence and restore parity.

## Unverified hardware evidence

The following items were deliberately not executed because both local and self-hosted Unity machines are unavailable:

- `unverified`: Unity import and C# compilation
- `unverified`: EditMode execution
- `unverified`: PlayMode/CityPrototype integration
- `unverified`: Windows/macOS checksum comparison
- `unverified`: profiler and benchmark measurements

No passing test count or runtime performance number is claimed.
