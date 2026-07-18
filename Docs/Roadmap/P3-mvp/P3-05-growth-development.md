# [P3-05] 개발 후보 샘플링·건설·성장 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p3-05-growth-development`
- Depends on: `P2-04, P3-01, P3-02, P3-04`

## Goal

도로·외부 연결·전력·RCI 조건을 모두 만족하는 필지에서 접근성 입지 factor를 반영해 결정적으로 건물을 선택하고 건설 시간과 월별 성장 제한을 적용한다.

## Scope

- DevelopmentState, candidate query, sampled definition selection
- 용도별 외부 접근성 가중치와 parcel suitability factor
- construction progress, completion, demolition/retry 기본 상태
- 월별 cap과 deterministic RNG stream
- 성장 EditMode fixture와 event/read model

## Mac steps

- 후보 정렬과 RNG 소비 순서를 고정한다.
- 도로/전력/수요 중 하나라도 없을 때 후보에서 제외하고 reason을 제공한다.
- `NetworkDistanceToExternalGate` band를 용도별 약한 가중치로 적용하고 raw factor를 query에 남긴다.
- 도시 전체 수요가 높지만 실행 가능한 필지가 없는 상태를 별도 결과로 제공한다.
- 접근성 단독으로 개발 여부나 definition을 결정하지 않는 경계 테스트를 추가한다.
- 건설 시작·진행·완료 event와 BuildingState 생성을 분리한다.
- 동일 seed/command log의 월별 건물 목록 회귀 테스트를 추가한다.

## Windows steps

- construction placeholder가 progress state에 맞게 나타나는지 연결한다.
- growth event를 받아 view를 생성/회수하고 per-building Update를 피한다.
- 전력 차단, 수요 감소, 시간 배속에서 성장 시작/정지 smoke를 수행한다.
- 같은 용도·조건이지만 접근성 band가 다른 두 필지의 factor와 선택 결과를 확인한다.

## Contracts and constraints

- Prefab 존재 여부나 view frame rate가 definition 선택에 영향을 주지 않는다.
- RNG stream은 다른 시스템 호출 추가로 흔들리지 않게 subsystem별로 분리한다.
- 건물 state는 stable ID와 serializable 값만 가진다.
- P3 접근성은 임시 교통 결과가 아니며 OD·혼잡·실제 통근시간을 만들지 않는다.
- 용도별 가중치는 정적 definition이고 현재 factor와 선택 결과는 runtime state/read model이다.

## Verification

- 필수 조건 누락별 성장 차단
- 접근성 band별 factor와 용도별 가중치
- 높은 시장 수요/실행 가능 필지 없음의 분리
- 같은 seed/commands의 건물 목록
- 월간 cap·건설 시간
- view 0개에서도 같은 simulation 결과

## Experience acceptance

- 외부 연결에 가까운 필지와 먼 필지의 접근성 factor를 공개 query에서 구분한다.
- 단순 격자와 간선-가지형 도로 배치가 모두 도로·전력·수요 조건을 충족하면 기본 성장 루프를 완주한다.
- 둘의 성능이 같을 필요는 없지만 접근성 하나만으로 한 배치가 개발 불가능해져서는 안 된다.

## Done when

- [x] 도로·전력·수요가 모두 있을 때만 성장한다.
- [x] 도시 수요와 필지별 개발 실행 가능성을 별도로 조회할 수 있다.
- [x] 접근성 factor가 위치 차이를 만들지만 단일 정답 배치를 강제하지 않는다.
- [x] 성장 결과가 같은 seed와 command stream에서 재현된다.
- [ ] Windows에서 view와 simulation state가 event 기반으로 동기화된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Mac implementation evidence

- Date: `2026-07-18`
- OS: `macOS 25.5.0 (Darwin 25.5.0), Apple Silicon arm64`
- Unity: `6000.5.3f1`
- Branch: `feature/p3-05-growth-development`
- Commit/PR: this implementation commit; PR pending Windows verification
- New tests: `GrowthDevelopmentTests` adds `10` EditMode cases (`150 → 160` total)
- Regression test: `./Tools/CI/run-unity-tests.sh edit` → EditMode `160/160` passed
- Regression result/log: `TestResults/editmode.xml`, `TestResults/editmode.log`
- PlayMode regression: `./Tools/CI/run-unity-tests.sh play` → PlayMode `23/23` passed
- PlayMode result/log: `TestResults/playmode.xml`, `TestResults/playmode.log`
- Covered contracts: per-requirement growth block (road/power/demand) with separate
  `DevelopmentBlockReason`; accessibility band suitability weights that never gate
  development or definition selection; per-use accessibility weight differences; high
  demand vs. no-feasible-parcel separation via `DevelopmentSectorSummary.HasUnmetDemand`;
  monthly cap and construction time; separated start/progress/completion events with
  `BuildingState` created only at completion; deterministic monthly building list for the
  same seed/command stream; identical simulation with or without a read model observing
  it; `DevelopmentState`/snapshot DTO round-trip; completed parcels are not redeveloped
- Remaining verify: Windows import/compile, construction placeholder + growth-event view
  create/recycle without per-building `Update`, power-cut/demand-drop/time-scale growth
  start/stop smoke, and two-road-layout basic loop; commit/PR handoff evidence
- Blocked: Windows view sync and commit/PR evidence cannot be completed from the macOS
  work tree

## Out of scope

- 쇠퇴·재개발
- 고밀도 upgrade
- OD 통행·혼잡·실제 통근시간
- production 건물 아트
