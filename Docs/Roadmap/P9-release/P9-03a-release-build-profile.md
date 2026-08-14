# [P9-03A] Release build profile and versioning

Issue: #136  
Parent: #44

## Committed contract

- product: `OpenSimCity`
- application version: `1.0.0`
- built-in content catalog version: `1`
- product Scenes, in order:
  1. `Assets/OpenSimCity/Scenes/Bootstrap.unity`
  2. `Assets/OpenSimCity/Scenes/MainMenu.unity`
  3. `Assets/OpenSimCity/Scenes/CityPrototype.unity`
- Windows output: `build/StandaloneWindows64/OpenSimCity.exe`
- macOS output: `build/StandaloneOSX/OpenSimCity.app`

`ReleaseBuildProfile` is the shared source of truth used by both Editor build entry points. `ReleaseBuildExecutionEvidence` records platform, source commit, clean-checkout flag, Unity version, product/application/content versions, output and exact product Scene sequence. `ReleaseBuildPairGate` rejects failed/dirty/different-commit/different-Unity/profile-or-Scene-mismatched candidate pairs.

`Tools/CI/build-windows.ps1` and `Tools/CI/build-macos.sh` pass the current Git commit and clean-checkout observation into `BuildScript`; the Editor writes the release evidence JSON only when the build command is actually executed. Source presence alone is not build evidence.

## Deferred runtime verification

The implementation pass intentionally does not invoke Unity or either build script. The following therefore remain `unverified`:

- Windows clean release build and launch
- macOS clean release build and launch
- same-commit pair-gate evaluation using produced JSON evidence
- macOS signing/notarization/Gatekeeper behavior
- Windows clean-install and user-data-path smoke
- package size/startup/render/profiler observations

These are user-run release-candidate checks, not inferred implementation results.
