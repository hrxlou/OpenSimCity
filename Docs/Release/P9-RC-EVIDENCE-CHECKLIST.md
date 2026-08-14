# P9 release-candidate evidence checklist

Use one candidate commit for every row. Do not mark an item passed from source/test presence alone.

## Candidate identity

- [ ] Record candidate commit SHA.
- [ ] Run `Tools/CI/build-windows.ps1` from a clean checkout and preserve `TestResults/build-windows.release-evidence.json`.
- [ ] Run `Tools/CI/build-macos.sh` from a clean checkout and preserve `TestResults/build-macos.release-evidence.json`.
- [ ] Confirm `ReleaseBuildPairGate` passes for the two evidence files.

## Regression and P9-02 UX

- [ ] Run the full EditMode suite; record XML/log, discovered count and pass result.
- [ ] Run the full PlayMode suite; record XML/log, discovered count and pass result.
- [ ] Complete first-15-minute onboarding/recovery/settings/audio/day-night smoke on Windows.
- [ ] Complete the same P9-02 smoke on macOS from the same commit.

## Performance

- [ ] Run the P8 `Target128` production-like benchmark protocol on Windows and evaluate `P8BenchmarkGate` against approved budgets.
- [ ] Run the same P8 `Target128` gate on macOS.
- [ ] Record `Stress256` measurements for P8 decision work; do not treat them as an automatic map-expansion approval.

## Save/content compatibility

- [ ] Create/load a current release-envelope save.
- [ ] Load a previous-supported unwrapped snapshot fixture through the migration path.
- [ ] Transfer a Windows save to macOS and load it.
- [ ] Transfer a macOS save to Windows and load it.
- [ ] Verify missing referenced definition fails explicitly without silent substitution.
- [ ] Verify unsupported content catalog metadata fails explicitly.

## Production art and licensing

- [ ] Run the production-art provenance validator.
- [ ] Audit every shipped third-party source/license/provenance record and preserve evidence ID/path.
- [ ] Complete release visual QA and preserve screenshots/evidence ID.
- [ ] Confirm the production-art performance budget is approved using measured P8 evidence.
- [ ] Confirm Windows/macOS release builds contain/render required production assets without LFS pointers or missing paths.

## Soak and packaging

- [ ] Windows release build: 30+ minute representative soak, no crash, no fatal log finding.
- [ ] macOS release build: 30+ minute representative soak, no crash, no fatal log finding.
- [ ] Windows clean-install/package launch and user-data path check.
- [ ] macOS package launch and Gatekeeper policy check.
- [ ] Record signing/notarization policy/result appropriate to the release scope.
- [ ] Record crash/log paths.
- [ ] Record known issues, including an explicit empty list if none remain.
- [ ] Record final build SHA, application version and content catalog version.

Only after these records all reference the same candidate commit should `ReleaseCandidateGate` be evaluated as a release pass and parent #44 be closed.
