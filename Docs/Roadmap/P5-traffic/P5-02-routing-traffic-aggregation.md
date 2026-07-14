# [P5-02] 대표 경로·용량·혼잡 집계 구현

- Phase: `P5`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p5-02-routing-aggregation`
- Depends on: `P2-02, P5-01`

## Goal

OD별 대표 경로 1~3개, 도로 version 기반 cache 무효화, 용량 대비 교통량과 유효 속도·통근시간을 계산한다.

## Scope

- deterministic path query와 route alternatives
- road version keyed cache
- edge volume/capacity, effective speed, commute time
- routing benchmark fixture

## Mac steps

- 경로 tie-break 규칙과 비용 단위를 명시한다.
- 도로 변경 시 영향 cache가 무효화되고 unchanged cache는 유지되는지 테스트한다.
- OD volume을 edge에 집계하고 혼잡 함수 경계를 고정한다.
- 작은 그래프 정답 fixture와 128x128 baseline 측정을 만든다.

## Windows steps

- 경로·혼잡 overlay로 병목과 unreachable route를 확인한다.
- Profiler에서 routing/monthly tick 측정 위치와 marker를 검증한다.
- 도로 변경 뒤 stale path가 표시되지 않는지 smoke 검증한다.

## Contracts and constraints

- cache는 저장 대상이 아니며 road version에서 재구축 가능하다.
- 경로 알고리즘 결과는 collection iteration/hash 순서에 의존하지 않는다.
- 성능 주장은 benchmark 숫자와 함께 기록한다.

## Verification

- 동일 비용 tie-break
- cache hit/invalidation
- volume 보존과 congestion 경계
- 128x128 tick baseline

## Done when

- [ ] 병목 도로가 유효 속도와 통근시간에 반영된다.
- [ ] road version 변경이 관련 경로를 정확히 무효화한다.
- [ ] Mac/Windows benchmark 환경과 결과를 구분해 기록한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 신호·차선
- 물리 차량
- Jobs/Burst 최적화
