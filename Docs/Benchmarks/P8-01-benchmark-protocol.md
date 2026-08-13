# P8-01 benchmark protocol

## Status

This document defines the reproducible P8 measurement contract. It does **not** contain a completed P8 platform baseline or an approved release performance budget yet. No P8 pass/fail performance claim may be made until the required runs are recorded on both required platform environments.

The existing `P3-08-production-like-art-baseline.md` remains a Windows rendering observation for its 24-instance art fixture. It is not promoted to a P8 release budget.

## Fixed scenarios

| Scenario | Map | Seed | Required buildings | Requested visible vehicles | Growth months |
|---|---:|---:|---:|---:|---:|
| `P8.Target128` | 128x128 | `0x08011280` | 2,000 | 500 | 120 |
| `P8.Stress256` | 256x256 | `0x08012560` | 2,000 | 500 | 120 |

`P8BenchmarkFixture` creates both cities from the same deterministic eight-cell road-grid algorithm, zoning pattern, power capacity, command path and monthly growth schedule. The fixture records the actual building count and checksum. It never fabricates the requested scale: a measured run below the required building/view count is rejected by `P8BenchmarkGate`.

## Render/content lanes

Every platform result is recorded separately for:

1. `ViewOff`: simulation/query/persistence measurements without render-budget claims.
2. `Placeholder`: current lightweight project presentation.
3. `ProductionLike`: P3-08 representative production-like content lane.

Placeholder numbers cannot establish the production-art render budget. Production-like measurements must retain the same representative catalog/provenance contract used by P3-08 or document an explicitly versioned successor fixture.

## Warm-up and samples

- Warm-up: 300 rendered frames before sampling.
- Sample window: 600 frames.
- Repetitions: 3 per platform/scenario/content lane.
- Resolution, quality level, graphics API, Unity version, OS and hardware ID must be identical across the three repetitions and recorded in evidence.
- macOS and Windows results are evaluated independently; they are never averaged together.
- Every repetition must finish with the same simulation checksum.

The frame counts above are measurement-protocol inputs, not performance budgets.

## Required measurements

`P8BenchmarkMetric` requires evidence for all of the following:

- simulation tick time
- monthly tick time
- CPU frame time
- GPU frame time
- GC allocated per frame
- total memory
- draw calls
- save payload size
- save time
- load time
- selection feedback latency
- placement-preview latency
- command-to-public-feedback latency
- HUD/query refresh latency

Time values are stored as integer microseconds, memory/GC/save size as bytes, and draw calls as integer counts. If a Unity Profiler counter is unavailable, the metric is marked `IsMeasured=false`; zero is not substituted for missing evidence.

## Evidence gate

`P8BenchmarkGate` refuses a performance pass when any of these conditions hold:

- fewer than three repetitions
- hardware/runtime/quality mismatch between repetitions
- actual fixture scale below the required building or visible-vehicle count
- checksum disagreement
- any missing required metric
- any missing **approved** platform/scenario/content budget
- any measured median above an approved maximum

This prevents benchmark infrastructure, placeholder-only measurements, or a single profiler capture from being reported as a release performance pass.

## Budget approval table

The following rows are intentionally unapproved until real P8 measurements are collected. Values must be entered from the P8 baseline review rather than inferred from P3, another machine, or model estimates.

| Platform | Scenario | Content | CPU/GPU/GC/memory/draw/save/input/HUD budgets |
|---|---|---|---|
| Windows | Target128 | Placeholder | **UNAPPROVED / UNMEASURED** |
| Windows | Target128 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| Windows | Stress256 | Placeholder | **UNAPPROVED / UNMEASURED** |
| Windows | Stress256 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| macOS | Target128 | Placeholder | **UNAPPROVED / UNMEASURED** |
| macOS | Target128 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| macOS | Stress256 | Placeholder | **UNAPPROVED / UNMEASURED** |
| macOS | Stress256 | ProductionLike | **UNAPPROVED / UNMEASURED** |

## Required evidence record

Each run records:

- platform / OS / hardware ID
- Unity version / graphics API
- resolution / quality
- scenario and content mode
- repeat index
- actual building count and visible vehicle count
- simulation checksum
- measured min/median/max for every required metric

A follow-up P8 measurement run should commit the resulting evidence and approved budget set without changing the deterministic scenario definition. Optimization work in P8-02 must compare against these exact scenario/checksum contracts.
