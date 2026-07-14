# [P7-06] District·정책 두 종류 수직 슬라이스

- Phase: `P7`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `Epic; Ready 전 2개 1–3 day 이슈로 분할`
- Branch: `feature/p7-06-district-policy`
- Depends on: `P5-05, P6-02, P7-05`

## Goal

플레이어가 셀 권역을 지구로 지정하고 화물차 제한·대중교통 보조금 두 정책을 적용해, 같은 도시 안에서도 서로 다른 공간 운영과 설명 가능한 비용·부작용을 만들게 한다.

## Scope

- stable `DistrictId`, 셀별 단일 district membership, 이름과 표시 색
- 지구 칠하기·지우기·이름 변경 application command
- `HeavyVehicleRestriction`, `TransitSubsidy` 두 정책과 다음 월 적용
- 정책 비용, 적용 범위, 입력 factor, 파생 결과 reason/query
- 지구별 인구·고용·통행·서비스·오염·재정 read model

## Required split before Ready

1. District ID·membership command/read model·저장과 지구 편집 UX
2. 두 policy command/effective month·파생 결과·비교 UI와 P7 experience gate

## Mac steps

- 지구 membership 변경 집합을 안정 셀 순서로 만들고 겹침·지우기·경계·빈 지구 정책을 테스트한다.
- 정책은 수요·토지가치·성장률 같은 결과에 직접 보너스를 주지 않고 경로 접근/비용, 운행 예산/용량 같은 명명된 입력만 바꾸게 한다.
- 화물차 제한이 대상 도로의 접근 또는 경로 비용과 행정비를 바꾸고 소음·상품 이동시간은 기존 시스템에서 파생되게 한다.
- 대중교통 보조금이 시 지출과 버스 일반화 비용/용량을 바꾸고 mode share·자동차 통행은 P5 계약에서 파생되게 한다.
- 정책 effective month, command ordering, 저장 왕복과 지구 삭제 시 정책 state 정리를 테스트한다.

## Windows steps

- 셀 브러시로 지구를 칠하고 이름·색·정책을 설정하는 입력과 preview를 구현한다.
- 정책 패널에서 적용 월, 월간 비용, 직접 변경하는 입력, 예상 trade-off, 실제 지난 기간 결과를 구분해 표시한다.
- 화물차 제한 뒤 주거지 소음과 화물 우회/도달 불가를 함께 확인한다.
- 대중교통 보조금 뒤 승객·자동차 통행·시 지출 변화를 같은 기간 기준으로 비교한다.

## Contracts and constraints

- 지구와 정책 runtime state는 ScriptableObject가 아니라 stable ID 기반 저장 상태다.
- UI와 Presentation은 지구 통계나 정책 효과를 재계산하지 않는다.
- 정책은 `산업 수요 +10%`, `토지가치 +5` 같은 출력값 직접 modifier를 소유하지 않는다.
- 정책 변경은 명시된 기간 경계에서 적용하며 소급해서 이전 기간 결과를 바꾸지 않는다.

## Verification

- membership paint/erase/overlap/empty district
- policy effective month·비용·입력 factor
- freight restriction route/noise/travel-time derivation
- transit subsidy budget/mode-share derivation
- 저장·replay·UI/query 일치

## Experience acceptance

- 정책이 없는 지구와 정책이 있는 지구가 같은 기간에 서로 다른 비용·교통·소음 결과를 보이며 그 차이를 공개 UI로 설명한다.
- 플레이어가 정책의 이득과 대가를 적용 전 preview와 적용 후 지난 기간 결과에서 구분한다.
- 정책을 해제하거나 경계를 수정하면 계약된 다음 기간에 원인과 결과가 갱신되고 stale overlay가 남지 않는다.

## Done when

- [ ] 지구가 stable ID와 cell membership으로 저장·재현된다.
- [ ] 두 정책이 직접 결과 버프 없이 기존 교통·재정 입력을 통해 효과와 대가를 만든다.
- [ ] 지구별 통계·정책 원인·적용 기간이 공개 UI와 overlay에서 일치한다.
- [ ] 양쪽 플랫폼에서 policy scenario가 같은 결과를 낸다.
- [ ] P7 전체 capability/experience gate 증거와 알려진 밸런스 문제가 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 정책 3종 이상과 정책 트리
- 지구별 세율·서비스 차량 수동 배차
- 겹치는 복수 행정 구역
- 테마 에셋 자동 교체
