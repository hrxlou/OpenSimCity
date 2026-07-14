# [P7-01] 공공서비스 범위·용량·예산 구현

- Phase: `P7`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p7-01-services-budget`
- Depends on: `P6-02`

## Goal

경찰·소방·학교·병원·폐기물의 범위, 용량, 예산 민감도를 집계하고 도시 상태에 단계적으로 반영한다.

## Scope

- ServiceDefinition/State와 facility capacity
- 공간 범위와 도로 이동시간 기반 범위 분리
- budget command, monthly effects, diagnostics
- service EditMode fixtures

## Mac steps

- 서비스별 공통 계약과 서로 다른 coverage strategy를 분리한다.
- 예산 삭감이 즉시 binary off가 아닌 단계적 용량/효율 변화를 만들게 한다.
- 수요 초과, 고립 도로, 예산 경계 테스트를 추가한다.
- condition/demand 시스템과 event/query 계약으로 연결한다.

## Windows steps

- facility placeholder 배치와 coverage overlay를 연결한다.
- 예산 slider/command와 다음 월 효과를 smoke 검증한다.
- 선택 inspector에서 capacity, usage, uncovered reason을 표시한다.

## Contracts and constraints

- 공원·환경은 공간 거리, 출동 서비스는 도로 이동시간 field를 구분한다.
- ScriptableObject에는 현재 budget/usage를 저장하지 않는다.
- facility view 수가 coverage 결과를 바꾸지 않는다.

## Verification

- 범위·용량 초과·고립망
- budget 단계 효과와 적용 월
- coverage query/overlay
- 양쪽 monthly snapshot

## Done when

- [ ] 서비스 부족과 예산 삭감이 단계적으로 상태에 반영된다.
- [ ] 서비스별 coverage와 원인을 조회할 수 있다.
- [ ] Mac 규칙 테스트와 Windows UI smoke가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 출동 차량 view
- 수도
- 고밀도
