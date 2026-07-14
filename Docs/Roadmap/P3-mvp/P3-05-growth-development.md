# [P3-05] 개발 후보 샘플링·건설·성장 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p3-05-growth-development`
- Depends on: `P2-04, P3-01, P3-02, P3-04`

## Goal

도로·외부 연결·전력·RCI 조건을 모두 만족하는 필지에서 결정적으로 건물을 선택하고 건설 시간과 월별 성장 제한을 적용한다.

## Scope

- DevelopmentState, candidate query, sampled definition selection
- construction progress, completion, demolition/retry 기본 상태
- 월별 cap과 deterministic RNG stream
- 성장 EditMode fixture와 event/read model

## Mac steps

- 후보 정렬과 RNG 소비 순서를 고정한다.
- 도로/전력/수요 중 하나라도 없을 때 후보에서 제외하고 reason을 제공한다.
- 건설 시작·진행·완료 event와 BuildingState 생성을 분리한다.
- 동일 seed/command log의 월별 건물 목록 회귀 테스트를 추가한다.

## Windows steps

- construction placeholder가 progress state에 맞게 나타나는지 연결한다.
- growth event를 받아 view를 생성/회수하고 per-building Update를 피한다.
- 전력 차단, 수요 감소, 시간 배속에서 성장 시작/정지 smoke를 수행한다.

## Contracts and constraints

- Prefab 존재 여부나 view frame rate가 definition 선택에 영향을 주지 않는다.
- RNG stream은 다른 시스템 호출 추가로 흔들리지 않게 subsystem별로 분리한다.
- 건물 state는 stable ID와 serializable 값만 가진다.

## Verification

- 필수 조건 누락별 성장 차단
- 같은 seed/commands의 건물 목록
- 월간 cap·건설 시간
- view 0개에서도 같은 simulation 결과

## Done when

- [ ] 도로·전력·수요가 모두 있을 때만 성장한다.
- [ ] 성장 결과가 같은 seed와 command stream에서 재현된다.
- [ ] Windows에서 view와 simulation state가 event 기반으로 동기화된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 쇠퇴·재개발
- 고밀도 upgrade
- production 건물 아트
