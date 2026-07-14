# [P6-02] 쇠퇴·폐건물·재개발과 원인 데이터맵 구현

- Phase: `P6`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p6-02-decline-redevelopment`
- Depends on: `P6-01`

## Goal

입지와 점유율 악화가 시간 지연을 거쳐 상태 악화·폐건물·재개발로 이어지고 플레이어가 원인을 UI와 데이터맵에서 확인하게 한다.

## Scope

- building condition state machine와 timers
- occupancy decline, abandonment, redevelopment eligibility
- reason factors ViewModel, tooltip, datamap
- scenario tests and PlayMode smoke

## Mac steps

- 상태 전이·복구 지연과 threshold를 EditMode 테스트로 고정한다.
- 같은 seed/commands에서 decline timeline이 같은지 회귀 fixture를 만든다.
- redevelopment가 stable ID/state 계약을 지키는지 검토한다.

## Windows steps

- building condition view와 pooled visual variant를 연결한다.
- 선택 inspector/tooltip과 datamap에 top reason을 표시한다.
- 오염 추가→점유율 저하→폐건물→조건 회복→재개발 시나리오를 smoke 검증한다.

## Contracts and constraints

- 시각 상태가 simulation timer를 소유하지 않는다.
- 문제 원인은 query factor에서 제공하며 UI가 재계산하지 않는다.
- 상태 전이는 매 프레임이 아닌 scheduler tick으로 처리한다.

## Verification

- 악화·회복 time delay
- abandon/redevelopment eligibility
- reason ordering
- UI/map/query 일치

## Done when

- [ ] 조건 악화에서 쇠퇴까지 명시적 시간 지연이 있다.
- [ ] 플레이어가 두 곳 이상에서 원인을 확인한다.
- [ ] P6 시나리오가 양쪽에서 재현된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 서비스 효과
- 고밀도 upgrade
- production decay art
