# [P5-03] pooled 차량·혼잡 데이터맵·P5 벤치마크

- Phase: `P5`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p5-03-traffic-views`
- Depends on: `P5-02`

## Goal

실제 집계 통행을 샘플링한 pooled 차량과 혼잡 데이터맵을 구현하고 차량 표시 수가 simulation 결과를 바꾸지 않음을 증명한다.

## Scope

- vehicle sample read model, central scheduler, pool
- route interpolation과 congestion datamap
- 0/100/500 vehicle benchmark and checksum
- PlayMode/Benchmark Scene tests

## Mac steps

- vehicle view 수 0과 기본값에서 simulation checksum을 비교한다.
- macOS baseline CPU/GPU/GC를 기록하고 결과를 Windows와 직접 혼합하지 않는다.
- pool lifecycle PlayMode test를 실행한다.

## Windows steps

- 차량 pool과 중앙 interpolation scheduler를 구현한다.
- 화면 차량 한도·LOD와 datamap toggle을 연결한다.
- 대표 병목 fixture에서 혼잡·도달 불가 reason을 확인하고 도로 변경 전후 query/datamap을 비교한다.
- 0/100/500 표시 차량에서 frame time, GC, draw calls를 Profiler로 기록한다.
- 도로 변경·load 후 pool 회수와 stale route 제거를 검증한다.

## Contracts and constraints

- 차량마다 `Update()`를 두지 않는다.
- vehicle view는 save snapshot에 포함하지 않는다.
- 표시 샘플이 실제 edge volume을 다시 수정하지 않는다.

## Verification

- view count별 simulation checksum 동일
- pool acquire/release와 scene unload
- datamap/query 일치
- 도로 변경 전후 병목·통근시간 개선 시나리오
- 128x128 benchmark 기록

## Experience acceptance

- 플레이어가 대표 fixture의 상위 병목과 영향받는 OD/통근시간을 공개 데이터맵 또는 inspector에서 확인한다.
- 대체 연결 추가 또는 병목 도로 변경 뒤 적어도 한 대표 OD의 통근시간이나 unmet demand가 개선된다.
- 표시 차량 0/100/500대에서 원인 표시와 simulation 개선 결과가 같다.

## Done when

- [ ] 표시 차량을 0대로 해도 simulation 결과가 동일하다.
- [ ] pooled 차량과 혼잡 map이 집계 통행을 일관되게 표현한다.
- [ ] 문제→병목 원인→도로 수정→통행 개선 시나리오가 양쪽에서 재현된다.
- [ ] P5 benchmark와 profiler 증거가 저장된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 차량 물리·충돌
- 개별 시민 여정
- 최종 차량 아트
