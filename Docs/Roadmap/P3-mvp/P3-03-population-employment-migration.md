# [P3-03] 인구·고용·월별 이주 집계 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p3-03-population-employment`
- Depends on: `P3-01`

## Goal

건물 단위 점유율에서 노동 가능 인구, 취업자, 실업자, 빈 일자리를 집계하고 외부 연결을 통한 월별 이주를 계산한다.

## Scope

- PopulationState, EmploymentState, MigrationState
- 건물별 주거/일자리 capacity와 단순 고용 matching
- 월별 scheduler와 snapshot/read model
- EditMode 규칙·불변식 테스트

## Mac steps

- 인구·노동력·일자리 보존 불변식을 정의한다.
- stable ordering으로 일자리 매칭하고 점유율 상한/하한을 보장한다.
- 주택 여유, 빈 일자리, 실업, 외부 연결을 이주 압력으로 계산한다.
- 월 경계, 빈 도시, capacity 감소, 외부 연결 차단 fixture를 작성한다.

## Windows steps

- HUD 없이 debug inspector/read model로 월별 수치를 확인한다.
- 배속 변경과 pause 후 월별 실행 횟수가 정확한지 smoke 검증한다.
- 같은 command log를 실행해 월별 snapshot을 Mac과 비교한다.

## Contracts and constraints

- 개별 시민 GameObject나 per-agent Update를 만들지 않는다.
- 표시 차량/시민 수는 고용·이주 결과와 무관하다.
- 월별 시스템 실행 순서는 application scheduler가 소유한다.

## Verification

- 인구·고용 보존 불변식
- 외부 연결 유무에 따른 이주
- capacity 감소와 실업
- 월별 snapshot 결정성

## Done when

- [ ] 인구, 취업자, 실업자, 빈 일자리가 일관되게 집계된다.
- [ ] 외부 연결과 주택/일자리 조건이 월별 이주에 반영된다.
- [ ] 양쪽에서 동일 command log의 snapshot이 같다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 연령·교육 cohort
- 개별 통근
- 시민 view
