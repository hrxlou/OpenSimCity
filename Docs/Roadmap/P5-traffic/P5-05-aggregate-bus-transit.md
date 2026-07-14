# [P5-05] 집계형 버스 대중교통 수직 슬라이스

- Phase: `P5`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `Epic; Ready 전 2개 1–3 day 이슈로 분할`
- Branch: `feature/p5-05-bus-transit`
- Depends on: `P5-03, P5-04`

## Goal

정류장과 한 종류의 버스 노선을 플레이어가 구성하고, 접근성·일반화 이동비용·노선 용량·예산에 따라 자동차 OD 일부가 버스로 전환되는 과정을 개별 시민 없이 설명 가능하게 구현한다.

## Scope

- stable `TransitStopId`, `TransitLineId`와 저장 가능한 노선 순서
- 정류장 배치·노선 생성/수정/삭제·운행 예산 application command
- 보행 접근권, 노선 우회 비용, 대기 비용, 용량을 사용한 집계 mode choice
- 자동차·버스 통행량 보존, 노선 승객 수, 혼잡도, 운영비
- pooled bus view와 대중교통/접근성 overlay

## Required split before Ready

1. 정류장·노선 command/state, 접근·mode choice·용량·재정과 저장/결정성
2. 노선 편집 UX, pooled bus view, overlay와 교통 전략 experience gate

## Mac steps

- 정류장 접근 가능 OD 후보와 노선 경로를 안정 ID 순서로 계산한다.
- 자동차에서 버스로 전환되는 통행량이 전체 OD를 생성하거나 삭제하지 않도록 보존식을 고정한다.
- 접근성, 자동차 대비 일반화 비용, 잔여 용량, 예산에서 mode share를 계산하고 반올림·tie-break를 테스트한다.
- 도로·정류장·노선 변경 시 관련 경로와 mode choice cache가 정확히 무효화되는지 검증한다.
- 노선과 예산의 저장 왕복, command replay, 월간 재정 반영을 테스트한다.

## Windows steps

- 정류장 순서 지정과 노선 수정 preview를 구현하고 끊긴 구간·중복 정류장·자금 부족 reason을 표시한다.
- 노선별 승객, 용량, 우회 비용, 운영비, 대체한 자동차 통행량을 한 패널 흐름에서 확인하게 한다.
- 표시 버스 수 0/기본/상한에서 simulation checksum과 승객 수가 같은지 확인한다.
- 교통·접근성 overlay에서 버스 도입 전후 병목과 미서비스 권역을 비교한다.

## Contracts and constraints

- 개별 시민이나 승객 GameObject가 탑승·환승 상태의 진실 원천이 되지 않는다.
- 버스 view는 집계 노선 결과를 샘플링해 보여주며 승객 수와 도로 교통량을 결정하지 않는다.
- 버스는 자동차 통행량에 고정 비율을 곱해 삭제하지 않고 접근 가능한 OD와 비용·용량·예산으로 전환량을 계산한다.
- 노선과 정류장 변경은 application command를 통해서만 runtime state를 바꾼다.

## Verification

- OD·mode share·차량 통행량 보존
- 정류장 접근·노선 우회·용량·예산 경계
- road/line version cache invalidation
- 저장·replay·view on/off checksum

## Experience acceptance

- 도로 확장으로 병목을 완화하는 도시와 버스 투자로 같은 OD의 자동차 통행을 줄이는 도시가 모두 실행 가능하지만 서로 다른 건설비·운영비·공간 사용을 가진다.
- 플레이어가 버스 승객이 낮은 상위 원인을 공개 UI에서 확인하고 정류장·노선·예산 command 중 하나로 개선한다.
- 버스가 만능 해법이 되지 않으며 접근 불가·과도한 우회·용량 부족·재정 부담 중 하나 이상의 관찰 가능한 trade-off를 가진다.

## Done when

- [ ] 버스 전환량이 접근성·비용·용량·예산에서 결정되고 OD를 보존한다.
- [ ] 정류장·노선·예산이 stable ID와 command로 저장·재현된다.
- [ ] 도로 중심과 버스 중심의 두 실행 가능한 교통 개선 시나리오가 통과한다.
- [ ] 표시 버스 수가 simulation 결과를 바꾸지 않는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 지하철·철도·트램·택시·선박·항공
- 다중 교통수단 환승과 통합 환승센터
- 개별 시민 일정·탑승 시뮬레이션
- 차량별 차선 AI와 버스 신호 우선
