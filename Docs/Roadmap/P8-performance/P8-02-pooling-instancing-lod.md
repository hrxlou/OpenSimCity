# [P8-02] pooling·instancing·LOD와 근거 기반 최적화

- Phase: `P8`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `perf/p8-02-rendering-simulation`
- Depends on: `P8-01`

## Goal

측정된 병목에 한해 view pooling, GPU instancing, LOD, cache 또는 Jobs/Burst를 적용하고 동일 fixture의 전후 수치를 비교한다.

## Scope

- building/vehicle pool and renderer batching
- LOD/culling/material atlas readiness
- selected production-candidate asset의 material/triangle/renderer inventory
- measured simulation hotspot optimization
- before/after benchmark report

## Mac steps

- simulation 최적화가 결정성 checksum과 EditMode suite를 깨지 않는지 검증한다.
- Mac baseline과 동일 조건으로 before/after를 측정한다.
- Jobs/Burst가 선택된 경우 fallback과 serialization 경계를 확인한다.

## Windows steps

- Profiler의 상위 병목부터 하나씩 변경하고 capture를 보존한다.
- instancing/LOD/pool에서 draw calls, GPU/CPU, GC 변화를 측정한다.
- MegaKit 등 source asset의 표본 수치는 성능 budget으로 사용하지 않고, 정규화된 Unity fixture에서 병목과 시각 trade-off를 측정한다.
- 128x128 목표 fixture와 256x256 stress fixture를 다시 실행한다.
- P8-01에서 정한 selection·preview·HUD/query feedback 예산을 같은 fixture에서 다시 측정한다.

## Contracts and constraints

- 숫자 없는 성능 개선 주장을 하지 않는다.
- 시각 최적화가 simulation state와 command 순서를 바꾸지 않는다.
- optimization commit은 가능하면 병목 하나씩 분리한다.
- 모든 건물에 collider나 동일한 LOD 단계를 일괄 요구하지 않는다. grid query를 selection의 기준으로 유지하고, P8-01 측정과 camera distance에 근거해 asset군별 규칙을 정한다.
- material 통합과 atlas는 appearance·provenance·배포 라이선스를 보존하는 범위에서 draw call 병목이 확인된 경우에만 적용한다.

## Verification

- before/after profiler numbers
- simulation checksum 동일
- pool/scene lifecycle
- quality/LOD visual smoke
- asset군별 renderer/material/triangle/LOD 결정표
- 입력/명령 feedback latency before/after

## Experience acceptance

- 목표 fixture에서 selection, placement preview, command result, 공개 reason 갱신이 P8-01의 플랫폼별 예산을 충족한다.
- 최적화 전후 같은 문제 fixture에서 플레이어에게 표시되는 원인과 가능한 명령이 의미상 바뀌지 않는다.
- 품질/LOD를 낮춰야 통과하는 경우 필요한 설정과 시각적 trade-off를 기록한다.

## Done when

- [ ] 목표 fixture의 성능 예산 충족 여부가 숫자로 판정된다.
- [ ] 최적화 전후와 부작용이 `Docs/Benchmarks`에 기록된다.
- [ ] 양쪽 플랫폼 회귀와 결정성 테스트가 통과한다.
- [ ] 목표 규모에서 문제 식별과 명령 feedback이 응답 예산을 충족한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 측정되지 않은 전면 재작성
- 플랫폼 하나만의 무근거 튜닝
- 최종 아트 제작
