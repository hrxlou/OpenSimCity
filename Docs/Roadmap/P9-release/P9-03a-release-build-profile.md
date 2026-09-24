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

## 2026-09-05 standalone boot repair

- `Bootstrap`은 프로젝트/빌드 계약의 index 0 진입점으로 유지한다.
- 실제 standalone 플레이어에서는 `StandaloneSceneFlow`가 `CityPrototype`으로 즉시 전환한다.
- `CityPrototype`의 release menu가 시작 화면을 담당하며, Editor PlayMode scene smoke는 자동 전환하지 않는다.
- `-osc-showcase -osc-capture <png>`는 application command로 seeded 도시를 만든 뒤 실제 player frame을
  저장하고 종료한다. 잠금 화면이나 원격 runner에서도 수동 캡처 없이 visual regression 증거를 남기는 QA 전용 경로다.

## Local visual integration evidence

Unity CLI dirty-worktree macOS 개발 빌드와 일반/쇼케이스 launch는 2026-09-05에 검증했다. 이는 UI·asset 통합
smoke이며 아래 clean release candidate 검증을 대신하지 않는다.

## Deferred release verification

The following remain `unverified`:

- Windows clean release build and launch
- macOS clean release build and launch
- same-commit pair-gate evaluation using produced JSON evidence
- macOS signing/notarization/Gatekeeper behavior
- Windows clean-install and user-data-path smoke
- package size/startup/render/profiler observations

These are user-run release-candidate checks, not inferred implementation results.
