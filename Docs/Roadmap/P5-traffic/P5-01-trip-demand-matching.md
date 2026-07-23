# [P5-01] 건물·청크 단위 통행 수요와 목적지 매칭 구현

- Phase: `P5`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p5-01-trip-demand`
- Depends on: `P4-04`

## Goal

고용 매칭과 시장 흐름에서 건물/청크 단위 origin-destination 통행량을 생성하고 결정적으로 목적지를 매칭한다.

## Scope

- TripDemandState와 OD aggregate
- 통근·상품 이동 발생량과 destination matching
- unreachable demand reason과 fixture

## Mac steps

- 통행 단위와 시간 window를 정의하고 개별 시민 생성 없이 집계한다.
- origin/destination 후보를 stable order로 매칭하고 unmet demand를 기록한다.
- 외부 연결·고립망·일자리 변화 시나리오 테스트를 만든다.
- 표시 차량을 전혀 만들지 않은 상태로 결과를 검증한다.

## Windows steps

- OD debug lines 또는 table read model로 대표 fixture를 시각 확인한다.
- 배속과 view on/off에서 monthly trip totals가 같은지 확인한다.
- 큰 fixture import 후 Console과 memory smoke를 수행한다.

## Contracts and constraints

- TripDemand는 차량 GameObject 수와 독립적이다.
- 목적지 matching RNG/순서는 다른 시스템 stream과 분리한다.
- OD state는 path cache와 구분한다.

## Verification

- 통근·상품 OD 합계
- unreachable/외부 연결
- stable destination matching
- view disabled checksum

## Done when

- [ ] 고용과 시장에서 집계 통행이 생성된다.
- [ ] 표시 차량 0대에서도 동일한 통행 수요가 계산된다.
- [ ] 양쪽 fixture 총량이 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 실시간 개별 agent
- 차선 선택
- pathfinding
