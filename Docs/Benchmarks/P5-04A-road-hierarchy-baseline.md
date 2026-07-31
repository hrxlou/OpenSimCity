# P5-04A Road Hierarchy Baseline

## Environment

- Date: 2026-07-31
- OS: Windows self-hosted GitHub Actions runner
- Runner: `opensimcity-win-runner`
- Unity: `6000.5.3f1`
- Pull request: `#102`
- Verified branch commit: `32d258a96f0cf618ab7c6909ad7286a0d1971e1f`
- Verified PR merge ref: `9ce17acdbf0b4dd4873d79032205f0671c050a2d`
- CI run: `30636228391` (`#159`)

## Fixture

Test case:

`OpenSimCity.Tests.EditMode.RoadHierarchyBenchmarkTests.LargeGridUpgradeAndDirectedRouteBaselineIsDeterministic`

The fixture creates one 128 x 128 deterministic city runtime, places a 128-cell
horizontal street path, upgrades it in place to a forward one-way avenue, and issues
64 repeated forward and reverse directed-route queries.

Expected contract:

- occupied road cells after upgrade: 128
- upgraded road cells: 128
- forward route edges: 127
- reverse route result: explicit no-path
- road-cell occupancy identity remains intact
- every upgraded cell uses the avenue stable definition ID
- every upgraded cell exits east
- repeated queries return deterministic results
- Simulation assembly references neither `UnityEngine` nor `UnityEngine.CoreModule`

## Result

- Upgrade body time: **2.171 ms**
- Directed query body time: **2.913 ms**
- NUnit case duration: **0.011357 seconds**
- Route edges: **127**
- Road checksum: **13237117612676299589** (`0xB7B3B055E1A33B45`)
- Result: **Passed**
- Full EditMode suite: **245 / 245 passed**
- Full PlayMode suite: **41 / 41 passed**
- ProjectSettings committed baseline: passed

The body timings are measured by `Stopwatch` inside the fixture. The NUnit duration
includes setup and assertions on this specific Windows runner. They are regression
baselines, not cross-platform performance claims. macOS measurements must be recorded
separately and must not be averaged with this result.

## Persistence and determinism evidence

The same CI run verifies:

- stable small-road, street, and avenue definition IDs and explicit trade-off values;
- positive build-cost difference charging, no downgrade refund, insufficient-funds
  rejection, no-change rejection, and atomic rollback;
- road occupancy preservation during definition and direction replacement;
- forward and reverse directed reachability plus explicit no-path behavior;
- changed-component route-cache invalidation and unrelated-component retention;
- snapshot v2 definition/direction round-trip;
- committed v1 golden-save migration to explicit bidirectional road state;
- deterministic continuation after v1 migration;
- canonical snapshot v2 schema rows `207` and checksum `0x6F6AA666B4FBC6B3`;
- canonical growing-city JSON checksum `0x84A162E5843E98E0`;
- rebuild of graph, route cache, read models, and presentation-derived state after restore.

GitHub artifact upload reported the repository storage-quota warning. Test counts,
benchmark output, and failure diagnostics remain available directly in the Actions log,
so the quota warning did not change the successful test conclusion.
