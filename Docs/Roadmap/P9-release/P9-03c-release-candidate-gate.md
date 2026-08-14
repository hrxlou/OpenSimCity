# [P9-03C] Regression, soak, license and release-candidate gate

Issue: #137  
Parent: #44  
Depends on: #136, #132

## Gate composition

`ReleaseCandidateGate` is the final P9 evidence composition gate. It does not execute Unity, profiler, builds or platform checks itself. It consumes evidence produced by those runs and refuses to infer a pass from source, fixture or configuration presence.

The release candidate can pass only when all evidence points to the same source commit as the P9-03A Windows/macOS release-build pair:

- Windows and macOS build pair passes `ReleaseBuildPairGate`
- EditMode and PlayMode were both executed, discovered at least one test, and passed
- Windows and macOS release-build soak each ran for at least 30 minutes, completed, and were crash/fatal-log free
- Windows and macOS each record a passing `P8BenchmarkGate` result for `Target128`
- current save round-trip, previous-supported save load, Windows↔macOS save exchange, missing-definition fail-closed, and unknown-catalog fail-closed checks passed
- P9 production-art evidence passes the existing `P9ArtReleaseQaGate`, including provenance, license audit, visual QA, build evidence and approved performance budget
- Windows/macOS package launch, user-data/Gatekeeper policy, signing/notarization policy record, crash/log paths, known-issues review and build SHA record are complete
- known issues are explicitly represented, including an intentionally empty list

Every executable evidence group has an evidence ID and commit SHA. A missing ID, missing run, zero discovered tests, mismatched commit, failed child gate, or incomplete checklist fails the RC gate with a specific reason.

## Deferred execution

This implementation pass intentionally did not run any of the above checks. Therefore the repository contains the gate and fixture source but **no P9-03C runtime evidence is claimed**. Until the user performs the release-candidate validation, all platform/build/profile/regression/soak/save-exchange/art/render/license/package observations remain `unverified`, and parent #44 must remain open.
