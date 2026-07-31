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

- `TripDemandState`와 건물 단위 OD aggregate
- 통근·지역 상품·외부 수입·외부 수출의 월간 발생량
- stable-ID 목적지 matching과 도로 component 자격
- unreachable demand reason과 fixture
- versioned building/chunk debug read model

## Mac steps

- 통행 단위와 시간 window를 정의하고 개별 시민 생성 없이 집계한다.
- origin/destination 후보를 stable order로 매칭하고 unmet demand를 기록한다.
- 외부 연결·고립망·일자리 변화 시나리오 테스트를 만든다.
- 표시 차량을 전혀 만들지 않은 상태로 결과를 검증한다.

## Windows steps

- OD table read model로 대표 `CityPrototype` fixture를 확인한다.
- view 관찰 on/off에서 monthly trip totals와 checksum이 같은지 확인한다.
- 400개 건물 aggregate fixture에서 Console·결정성 smoke를 수행한다.

## Contracts and constraints

- `TripDemandSystem`은 Simulation assembly의 정수 집계 시스템이며 차량·GameObject·Scene을 참조하지 않는다.
- 월간 순서는 `Population → Economy → Growth → TripDemand → Finance`다. OD는 월말 건물 집합을 읽지만
  시장이나 건물 상태를 다시 변경하지 않는다.
- 통근량은 주거 점유 인구, 목적지 용량은 상업·산업의 채워진 일자리에서 나온다.
- 지역 상품량은 기존 `MarketState.GoodsSold`, 수입은 `GoodsShortage`, 수출은 `GoodsSurplus`를 사용한다.
- 같은 road component 안에서 stable building ID 순서로 배분한다. RNG와 collection 우연 순서를 사용하지 않는다.
- `NoOriginRoadAccess`, `NoDestinationRoadAccess`, `DifferentRoadComponent`, 용량 부족, 외부 관문 없음·분리를
  서로 다른 reason으로 남긴다.
- OD state는 P5-02의 path cache와 구분한다. row에는 endpoint와 rebuildable component만 있고 경로 edge는 없다.
- OD는 저장 원본이 아니라 건물·시장·parcel·road에서 재구축 가능한 파생 상태다. snapshot v1 필드를 늘리지 않고
  restore 후 첫 read-model refresh에서 같은 checksum을 재구축한다.

## Verification

- 통근·지역 상품·수입·수출 OD 합계와 matched/unmet 보존식
- 외부 관문 없음, origin/destination 도로 없음, 서로 다른 component reason
- 건물 삽입 순서와 read-model 관찰 여부에 독립적인 stable checksum
- 다음 달 고용 변화에 따른 OD 재계산
- 400개 건물 fixture의 양방향 삽입 순서 checksum
- 공개 건설 도구를 사용하는 `CityPrototype` PlayMode smoke
- snapshot round-trip 뒤 동일 OD checksum과 snapshot v1 무변경

## Done when

- [x] 고용과 시장에서 집계 통행이 생성된다.
- [x] 표시 차량 0대에서도 동일한 통행 수요가 계산된다.
- [x] 양쪽 fixture 총량이 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 실시간 개별 agent
- 차선 선택
- pathfinding
- edge volume·capacity·congestion feedback (`P5-02`)
