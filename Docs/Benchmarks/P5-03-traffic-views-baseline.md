# P5-03 Traffic Presentation Baseline

## Environment

- Date: 2026-07-31
- OS: Windows self-hosted GitHub Actions runner
- Runner: `opensimcity-win-runner`
- Unity: `6000.5.3f1`
- Pull request: `#99`
- Verified implementation commit: `bb0e0677250ef16dc787a030b350233b3a2e1547`
- CI run: `30627265366` (`#119`)

## Verification result

- EditMode: **231 / 231 passed**
- PlayMode: **41 / 41 passed**
- ProjectSettings committed baseline: passed
- P5-02 128 x 128 route case: **0.039745 seconds**
- P5-03 read-model sampling case: **0.016544 seconds**
- P5-03 Benchmark Scene case: **0.213124 seconds**

## Benchmark Scene fixture

The PlayMode fixture loads `Benchmark`, creates a behaviour-free one-renderer cube
prefab, and measures the central `VehicleViewScheduler` at three visible sample
limits. The same immutable route path is reused so the result measures pool resize and
central transform scheduling rather than route calculation.

| Visible views | Synchronous pool + update | GC allocated in sampled frame | Draw-call counter | Main-thread counter | Active | Pooled |
|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0.064 ms | 80 bytes | unavailable (`-1`) | 0 ns | 0 | 0 |
| 100 | 1.511 ms | 13,778 bytes | unavailable (`-1`) | 0 ns | 100 | 0 |
| 500 | 5.611 ms | 51,378 bytes | unavailable (`-1`) | 0 ns | 500 | 0 |

The self-hosted CI process runs Unity with a null graphics device. Unity did not expose
a valid draw-call counter in that environment, so the fixture records `-1` instead of
inferring a value. The test prefab has exactly one renderer and no MonoBehaviour per
vehicle; therefore the fixture has 0, 100 and 500 active renderers respectively, but
that is not a measured GPU draw-call claim.

The main-thread profiler counter also returned zero in this batch-mode environment.
The explicit stopwatch result and production profiler markers remain the usable CPU
baseline. A graphics-enabled Windows Editor or Player profile must be recorded
separately before making GPU or draw-call claims.

## Pool lifecycle evidence

A separate PlayMode fixture verifies:

1. acquire 500 active behaviour-free views;
2. release all 500 to the inactive pool;
3. reacquire 100 without creating per-vehicle behaviours;
4. retain 400 pooled views;
5. destroy active and pooled views on scheduler disposal and scene unload.

The CityPrototype smoke additionally verifies that a public road-demolition command
removes every sampled path containing the demolished cell and that the datamap edge
count remains identical to the application query.

## Simulation invariance

EditMode fixtures run visible limits 0, 100 and 500 against the same routed aggregate
state. For every limit:

- `TrafficRoutingState.ComputeChecksum()` is unchanged;
- routing version is unchanged;
- represented-trip weights remain conserved;
- stable presentation sample IDs are deterministic;
- snapshot v1 contains no sample, pool, GameObject or datamap mesh state;
- restoring the source city rebuilds the same routing and visualization checksums.

## Production profiler markers

- `OpenSimCity.TrafficViews.Update`
- `OpenSimCity.TrafficVehicles.UpdatePositions`
- `OpenSimCity.TrafficVehicles.PoolResize`
- `OpenSimCity.TrafficDataMap.Rebuild`

These markers should be used for graphics-enabled Windows verification and future
regression comparisons. Results from macOS and graphics-enabled Windows runs must be
recorded separately rather than averaged with this null-graphics CI baseline.

## Artifact note

The GitHub Actions artifact upload reported the repository storage-quota warning. Test
counts and benchmark evidence were printed directly into the Actions log, so the quota
warning did not change the successful test conclusion.
