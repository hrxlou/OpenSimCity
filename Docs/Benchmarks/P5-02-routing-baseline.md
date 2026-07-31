# P5-02 Routing Baseline

## Environment

- Date: 2026-07-31
- OS: Windows self-hosted GitHub Actions runner
- Runner: `opensimcity-win-runner`
- Unity: `6000.5.3f1`
- Pull request: `#98`
- Verified branch commit: `993b148f3c303ca211fb092cde58bd7c93755370`
- CI run: `30625777564` (`#107`)

## Fixture

Test case:

`OpenSimCity.Tests.EditMode.TrafficRoutingSystemTests.LargeGridRouteIsDeterministicAndSimulationStaysUnityFree`

The fixture creates a fully occupied 128 x 128 cardinal road grid and requests one
deterministic route from `(0, 0)` to `(127, 127)`.

Expected contract:

- route alternatives requested: 1
- selected route edges: 254
- second identical query returns the cached route-set instance
- cache hits after second query: 1
- Simulation assembly references neither `UnityEngine` nor `UnityEngine.CoreModule`

## Result

- NUnit case duration: **0.018076 seconds**
- Result: **Passed**
- Full EditMode suite: **224 / 224 passed**
- Full PlayMode suite: **38 / 38 passed**
- ProjectSettings committed baseline: passed

The NUnit duration includes the test body on this specific Windows runner. It is a
regression baseline, not a cross-platform performance claim. macOS measurements must
be recorded separately and must not be averaged with this result.

## Persistence and determinism evidence

The same CI run also verifies:

- equal-cost route tie-break by complete cell sequence;
- affected-component cache invalidation and unrelated-component retention;
- matched OD amount conservation into route assignments and edge volume;
- capacity boundary and over-capacity speed/travel-time behavior;
- stale route removal after a public road-demolition command;
- snapshot v1 contains no traffic-route or edge-cache fields;
- restored road, economy, and OD source state rebuilds the same routing checksum.

GitHub artifact upload reported the repository storage-quota warning. Test counts,
benchmark duration, and failure diagnostics remain available directly in the Actions
log, so the quota warning did not change the successful test conclusion.
