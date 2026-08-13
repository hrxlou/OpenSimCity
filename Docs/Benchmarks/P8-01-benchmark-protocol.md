# P8-01 benchmark protocol

## Status

This document defines the reproducible P8 measurement contract. It does **not** contain a completed P8 platform baseline or an approved release performance budget yet. No P8 pass/fail performance claim may be made until the required runs are recorded on both required platform environments.

The existing `P3-08-production-like-art-baseline.md` remains a Windows rendering observation for its 24-instance art fixture. It is not promoted to a P8 release budget.

## Fixed scenarios

| Scenario | Map | Seed | Required buildings | Requested visible vehicles | Settlement months |
|---|---:|---:|---:|---:|---:|
| `P8.Target128` | 128x128 | `0x08011280` | 2,000 | 500 | 1 |
| `P8.Stress256` | 256x256 | `0x08012560` | 2,000 | 500 | 1 |

`P8BenchmarkFixture` creates both cities from the same deterministic two-cell road-grid algorithm. Isolated 1x1 development cells provide enough road-frontage parcels on both map sizes. The fixture applies a stable R/C/I pattern, reserves one powered utility cell, seeds up to 2,000 stable-ID 1x1 buildings in stable parcel order using normal building definitions, then advances one normal monthly settlement through the public clock so economy/trip/traffic state exists before measurement.

The fixture records the actual building count and checksum. A measured run below the required building/view count is rejected by `P8BenchmarkGate`.

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
- Resolution, quality level, graphics API, Unity version, OS and hardware ID must be identical across repetitions and recorded.
- macOS and Windows results are evaluated independently; they are never averaged together.
- Every repetition must finish with the same simulation checksum.

The frame counts above are measurement-protocol inputs, not performance budgets.

## Required measurements

`P8BenchmarkMetric` requires simulation tick, monthly tick, CPU frame, GPU frame, GC/frame, total memory, draw calls, save bytes/time, load time, selection feedback, placement preview, command feedback, and HUD/query refresh.

Time values use integer microseconds; memory/GC/save size use bytes; draw calls use integer counts. If a counter is unavailable, the metric is recorded as unmeasured rather than substituting zero.

## Evidence gate

`P8BenchmarkGate` refuses a pass for fewer than three repetitions, environment mismatch, undersized fixture, checksum drift, any missing required metric, any missing approved platform/scenario/content budget, or any measured median above its approved maximum.

## Budget approval table

No P8 budget is approved yet. Values must come from real P8 runs rather than P3, another machine, or estimates.

| Platform | Scenario | Content | Status |
|---|---|---|---|
| Windows | Target128 | Placeholder | **UNAPPROVED / UNMEASURED** |
| Windows | Target128 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| Windows | Stress256 | Placeholder | **UNAPPROVED / UNMEASURED** |
| Windows | Stress256 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| macOS | Target128 | Placeholder | **UNAPPROVED / UNMEASURED** |
| macOS | Target128 | ProductionLike | **UNAPPROVED / UNMEASURED** |
| macOS | Stress256 | Placeholder | **UNAPPROVED / UNMEASURED** |
| macOS | Stress256 | ProductionLike | **UNAPPROVED / UNMEASURED** |

Each run must record platform, OS, hardware, Unity version, graphics API, resolution, quality, scenario, content mode, repeat index, actual building/vehicle count, simulation checksum, and min/median/max for every required metric. P8-02 comparisons must reuse this scenario/checksum contract.
