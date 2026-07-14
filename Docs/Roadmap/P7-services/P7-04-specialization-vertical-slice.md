# [P7-04] 도시 특성화·생산 체인 상태 구현

- Phase: `P7`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `3 days`
- Branch: `feature/p7-04-specialization-state`
- Depends on: `P5-02, P6-02, P7-03`

## Goal

하나의 특성화를 위한 2~3단계 생산 체인, 외부 시장 연결, 진행 상태와 병목 reason을 결정적이고 저장 가능하게 구현한다.

## Scope

- 특성화 definition과 runtime progress state
- 원료→중간재→완제품의 한 가지 생산 체인
- 외부 시장 수입/수출과 집계 상품 통행 연결
- 단계별 해금 조건과 진행 상태 query
- 수익, 노동, 오염, 교통, 서비스 pressure 진단 factor
- 생산·재고·병목 EditMode fixture와 save DTO

특성화의 구체 테마는 이 이슈 시작 시 기존 아트와 시스템 fixture를 기준으로 하나만 선택한다. 전자제품, 광업, 관광 등 여러 테마를 동시에 구현하지 않는다.

## Mac steps

- 각 생산 단계의 입력·출력·손실·가동률·가격 단위를 정수 또는 양자화 값으로 정의한다.
- 외부 수입이 끊기거나 노동·전력·상품 통행이 부족할 때의 가동률과 reason을 테스트한다.
- 특성화 progress와 해금 조건을 stable ordering과 저장 가능한 runtime state로 구현한다.
- 하나의 생산 단계가 무한 수익 또는 무한 재투자 loop를 만들지 않는 경계 테스트를 추가한다.
- save/load 뒤 재고·가동률·progress와 다음 기간 결과가 같은지 검증한다.

## Windows steps

- placeholder 시설과 생산/저장/수출 상태를 기존 pooled view·overlay 흐름에 연결한다.
- debug read model에서 특성화 진행, 재고, 가동률, 병목 reason을 fixture와 비교한다.
- 원료 부족 또는 상품 통행 중단 뒤 stale view가 남지 않는지 smoke를 수행한다.

## Contracts and constraints

- ScriptableObject는 생산량·가격·해금 조건 같은 정적 definition만 가진다.
- 재고, 가동률, progress, 현재 가격/수익은 stable ID를 사용하는 serializable runtime state다.
- 표시 트럭이나 시설 animation은 상품 이동·가동률 결과를 만들지 않는다.
- `MunicipalFinanceState`와 민간 생산/거래 원장은 계속 분리한다.
- 특성화는 체크박스로 도시 정체성을 부여하지 않고 실제 건설·생산·거래 결과에서 파생한다.
- 일반 도시를 실패 상태로 만들거나 특성화를 유일한 흑자 전략으로 만들지 않는다.

## Verification

- 생산량·재고·손실 보존과 기간 경계
- 수입 중단·노동 부족·통행 병목·오염 pressure reason
- progress 해금 순서와 저장 round-trip
- view 0/기본값 simulation checksum

## Experience acceptance

- P7-04는 특성화 상태와 query의 capability를 구현하는 단계다. 플레이어용 비교·회복 experience gate는 P7-05에서 검증한다.
- 이 단계의 수동 smoke는 표시 시설과 debug read model이 production state를 왜곡하지 않는지만 확인한다.

## Done when

- [ ] 하나의 2~3단계 생산 체인이 외부 시장과 집계 상품 통행에 연결된다.
- [ ] 특성화 progress와 병목 reason이 저장·조회·설명 가능하다.
- [ ] 생산·재고·progress의 저장 왕복과 다음 기간 결과가 재현된다.
- [ ] P7-05가 사용할 query와 고정 fixture가 준비된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 두 번째 특성화 테마
- 최종 특성화 패널과 일반/특성화 비교 밸런스
- 도시 간 멀티플레이·공동 시장
- 동적 글로벌 가격 경제 전체
- 대역사급 프로젝트
- production 특성화 아트
