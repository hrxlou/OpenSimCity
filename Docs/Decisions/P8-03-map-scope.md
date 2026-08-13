# P8-03 map scope

The release default remains the existing finite **64x64** world while P8 benchmark evidence is incomplete. Benchmark-only 128/256 fixtures do not imply a player-facing expansion promise.

`P8MapScopeDecision` requires both Target128 and Stress256 benchmark gates to pass before returning `ExpansionEligible`. Missing runs, measurements, approved budgets, checksum parity or fixture scale keep `Fixed64 / BenchmarkEvidenceIncomplete`; a measured budget failure keeps `Fixed64 / BenchmarkBudgetFailed`.

`ExpansionEligible` only permits a separately reviewed region-unlock slice. It does not resize current worlds, reinterpret saves or expose locked-region UI automatically.

Current status: P8-01 infrastructure exists, but Windows/macOS baseline runs and approved budgets are unrecorded, so the product contract remains fixed 64x64. Issue #56 stays open until measured evidence supports its final disposition.