# P8-02 optimization evidence gate

## Status

P8-02 is not yet a completed optimization pass. P8-01 has committed the reproducible fixture/evidence schema, but real platform baselines and approved budgets are still absent. Therefore no rendering, simulation, pooling, LOD, instancing, Jobs/Burst, cache, or other optimization is selected or claimed here.

`P8OptimizationEvidence` exists to prevent that missing evidence from being bypassed later.

## Required before/after contract

An optimization comparison can pass only when:

1. the **before** evidence already passes the approved P8 benchmark budget;
2. the **after** evidence passes the same approved budget;
3. both use the same platform, hardware, Unity version, graphics API, resolution, quality level, scenario and content lane;
4. the simulation checksum is unchanged;
5. at least one explicitly declared target metric improves;
6. every declared protected metric stays within its explicit regression tolerance.

The comparator uses the median of the recorded per-run medians for each metric. It does not compare runs from different machines or average Windows and macOS results.

## No speculative optimization

Until #35 records profiler evidence, P8-02 intentionally does **not**:

- add Jobs/Burst because a subsystem looks expensive;
- batch/atlas materials because draw calls might be high;
- introduce LOD to every asset class;
- change pooled view counts;
- rewrite routing/economy loops based on assumptions;
- report an improvement based on code size or subjective feel.

When P8-01 evidence exists, select the highest measured bottleneck, declare target/protected metrics, make one bounded change, then record the before/after evidence under this gate. The optimization issue #37 remains open until measured platform evidence exists.
