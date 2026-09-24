# P5-05B - Transit construction UX, pooled views and strategy gate

Issue: #105

## Implemented player flow

- deterministic stop placement preview/commit/cancel
- ordered line editor with duplicate, missing-stop and broken-path reasons
- public keyboard and pointer adapter with shared UI input capture
- budget control and current-period ridership/cost/reason panel
- directional route, stop, capacity and utilization overlay query
- deterministic 0..200 display-bus sampling
- central behaviour-free bus pool and pooled line/stop views
- transient gameplay-scene bootstrap and unload cleanup

## Experience fixture

The committed fixture starts from the same deterministic growing city and compares:

1. avenue upgrade: same OD remains on the road network, municipal construction cost is paid and weighted travel time does not worsen;
2. bus investment: stop/line construction plus operating cost is paid and accessible commute OD is conserved as bus plus private-vehicle trips.

Building stable IDs and footprints remain unchanged in both strategies. Low-ridership and failed-line cases retain explicit access, direction, path, cost, capacity or budget reasons.

## Architecture evidence

- Gameplay only projects screen input to cells and invokes the pure transit tool.
- UI consumes Application read models and commands; it does not reference Simulation types.
- Presentation samples aggregate trips and never writes mode share or route volume.
- Every displayed bus is controlled by one coordinator; sampled views contain no per-vehicle `MonoBehaviour`.
- The default bus is a generated, behaviour-free single-renderer prefab loaded from a stable `Resources` path.
- Overlay and pool objects are derived and omitted from persistence.

## Committed verification fixtures

- `TransitConstructionExperienceTests`
- `TransitUxPlayModeTests`
- P5-05A conservation and persistence fixtures

## Unverified hardware evidence

Local and self-hosted Unity execution is intentionally disabled while both machines are unavailable.

- `unverified`: Unity import and C# compilation
- `unverified`: EditMode execution
- `unverified`: CityPrototype PlayMode execution
- `unverified`: actual keyboard/mouse/trackpad feel
- `unverified`: renderer/material appearance and Korean font fallback
- `unverified`: pool/profiler/GC/frame measurements
- `unverified`: Windows/macOS replay and checksum comparison

No test pass count, frame budget or profiler value is claimed.
