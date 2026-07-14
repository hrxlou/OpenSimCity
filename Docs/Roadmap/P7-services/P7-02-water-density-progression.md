# [P7-02] 수도·중고밀도 조건·업그레이드 구현

- Phase: `P7`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p7-02-water-density`
- Depends on: `P7-01`

## Goal

수도 공급과 인구·입지·도로·서비스 조건을 종합해 중·고밀도 개발과 기존 건물 업그레이드를 결정한다.

## Scope

- water utility network/state
- density definition/cap and upgrade eligibility
- upgrade state transition and deterministic candidate sampling
- EditMode scenarios

## Mac steps

- 수도 network의 공급/수요와 dirty update를 구현한다.
- 중·고밀도 조건과 growth cap을 diagnostic factor로 제공한다.
- upgrade 시 BuildingState stable ID 유지/교체 정책을 명시한다.
- 조건 누락, 공급 부족, downgrade 없음/있음 정책 테스트를 만든다.

## Windows steps

- 수도와 밀도 overlay, upgrade progress placeholder를 연결한다.
- 조건 충족/해제 시 UI reason과 실제 state가 일치하는지 확인한다.
- 중·고밀도 footprint/prefab key validator를 실행한다.

## Contracts and constraints

- 밀도 조건은 prefab 크기나 미존재 에셋 때문에 변하지 않는다.
- upgrade RNG와 순서는 결정적이다.
- 현재 수도 공급은 runtime state이며 definition asset에 기록하지 않는다.

## Verification

- 수도 연결·용량 부족
- density 조건 조합
- upgrade ordering/cap
- overlay/reason 일치

## Done when

- [ ] 고밀도 개발이 인구·수도·입지·도로 조건을 모두 요구한다.
- [ ] 업그레이드 결과가 재현 가능하다.
- [ ] Windows에서 placeholder가 state 전이를 정확히 표현한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- production high-density art
- 상세 수도 압력
- 건물별 리노베이션
