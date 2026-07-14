# [P5-04] 도로 계층·무철거 업그레이드·일방통행 구현

- Phase: `P5`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3–5 days`
- Branch: `feature/p5-04-road-hierarchy`
- Depends on: `P5-02, P5-03`

## Goal

소형 도로·일반 도로·대로가 비용·용량·속도·유지비·소음에서 서로 다른 선택을 제공하고, 기존 도시를 철거하지 않고 도로 등급과 방향을 바꿔 병목을 회복하게 한다.

## Scope

- 세 가지 `RoadDefinition`과 결정적 definition ID
- 기존 셀 경로를 유지하는 road upgrade/replace command
- 양방향·일방통행 directed edge와 경로 cache 무효화
- 건설 차액·유지비·용량·속도·소음 read model
- upgrade preview, 실패 reason, 교통 overlay 통합

## Mac steps

- 도로 등급 차이는 수요·토지가치 같은 결과에 직접 보너스를 주지 않고 비용·용량·속도·유지비·소음 입력으로만 표현한다.
- 같은 셀 경로의 definition과 방향을 바꾸는 command validation, 비용 정산, rollback 불변식을 구현한다.
- 일방통행을 directed edge로 표현하고 진입 금지·도달 불가·우회 경로의 정답 fixture를 만든다.
- upgrade/direction 변경 시 road version과 관련 route cache만 정확히 무효화되는지 테스트한다.
- 저장 왕복과 동일 command stream의 checksum을 검증한다.

## Windows steps

- 기존 도로 위 upgrade preview와 방향 표시를 연결한다.
- 철거 없이 소형 도로를 대로로 바꾸거나 일방통행 방향을 반전하는 입력 흐름을 smoke 검증한다.
- 변경 전후 비용·소음·용량·병목·도달 불가 reason을 같은 기간 기준으로 표시한다.
- 도로 mesh/표시가 simulation edge 방향과 어긋나지 않는지 확인한다.

## Contracts and constraints

- upgrade는 철거와 재설치를 암묵적으로 실행하지 않으며 기존 도로 stable identity와 연결 관계의 보존/교체 정책을 명시한다.
- `산업도로 = 화물 효율 증가`처럼 결과를 직접 수정하는 도로 전용 버프를 만들지 않는다.
- 차선 수는 집계 용량의 정적 입력이며 차량별 차선 변경을 뜻하지 않는다.
- 일방통행 표시와 물리 차량 view는 경로 결과를 만들지 않는다.

## Verification

- road definition 비용·용량·속도·유지비 경계
- upgrade 비용·실패·rollback·저장 왕복
- directed edge 도달성·tie-break·cache invalidation
- UI preview/commit과 overlay 일치

## Experience acceptance

- 동일한 시작 도시에서 저비용 소형 도로 중심 배치와 고비용 대로 중심 배치가 모두 기본 운영을 지속하지만 서로 다른 유지비·소음·혼잡 구성을 가진다.
- 플레이어가 혼잡 원인을 확인하고 건물을 철거하지 않은 채 도로 upgrade 또는 일방통행 변경으로 대표 병목 하나를 완화한다.
- 변경 때문에 새로 도달 불가가 된 경로는 적용 기간과 차단 reason을 공개 UI에서 확인할 수 있다.

## Done when

- [ ] 세 도로 등급이 직접 결과 버프 없이 비용·용량·속도·유지비·소음으로 구분된다.
- [ ] upgrade와 방향 변경이 application command로만 수행되고 저장·재현된다.
- [ ] 철거 없는 병목 회복 experience acceptance가 통과한다.
- [ ] 양쪽 플랫폼에서 command fixture와 표시 방향이 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 자유곡선 도로
- 신호 주기·차선 화살표·차선 변경 AI
- 교량·터널·입체교차로
- 산업·주거 전용 마법 modifier
