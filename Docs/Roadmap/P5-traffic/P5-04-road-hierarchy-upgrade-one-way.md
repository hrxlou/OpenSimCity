# [P5-04] 도로 계층·무철거 업그레이드·일방통행 구현

- Phase: `P5`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `Epic; P5-04A/P5-04B로 분할`
- Depends on: `P5-02, P5-03`
- Contract: [`Docs/Specs/ROAD_HIERARCHY.md`](../../Specs/ROAD_HIERARCHY.md)

## Goal

소형 도로·일반 도로·대로가 비용·용량·속도·유지비·소음에서 서로 다른 선택을 제공하고, 기존 도시를 철거하지 않고 도로 등급과 방향을 바꿔 병목을 회복하게 한다.

## Delivery split

### P5-04A — authoritative state, command, routing, persistence

- Issue: `#100`
- Branch: `feature/p5-04a-road-state-directed-graph`
- PR: `#102`
- Scope:
  - stable small/street/avenue definitions,
  - road-cell definition and cardinal exit direction,
  - no-demolition atomic `UpgradeRoadCommand`,
  - positive build-cost difference and no downgrade refund,
  - directed deterministic routing,
  - affected-component route-cache invalidation,
  - central `RoadHierarchyReadModel`,
  - snapshot v2 and v1-to-v2 bidirectional migration,
  - 128×128 deterministic benchmark.

### P5-04B — gameplay, visualization, and experience gate

- Issue: `#101`
- Planned branch: `feature/p5-04b-road-upgrade-ux`
- Scope:
  - existing-road upgrade preview/commit/cancel,
  - direction arrows plus non-color pattern,
  - before/after cost, maintenance, capacity, speed, and noise comparison,
  - congestion-overlay integration,
  - CityPrototype bottleneck-recovery fixture.

## Contracts and constraints

- The original street stable ID and historical economics remain unchanged.
- Upgrade does not implicitly demolish/reinstall road cells.
- Tier effects are limited to cost, maintenance, capacity, speed, and noise inputs; there are no direct zone or demand bonuses.
- Lane count is represented by aggregate capacity and does not imply per-vehicle lane-changing simulation.
- Direction is a road-cell exit rule. Presentation arrows do not create routing results.
- UI and Presentation consume application read models and never mutate Simulation directly.
- Graphs, route caches, overlay meshes, and pooled views remain rebuildable non-persisted state.

## Mac steps

- [x] Define stable road hierarchy data and retain the existing street ID.
- [x] Implement command validation, difference charging, no-refund downgrade, and atomic rollback.
- [x] Implement directed exit routing and explicit no-path results.
- [x] Invalidate only route-cache entries intersecting the affected road component.
- [x] Add snapshot v2 direction state and v1 bidirectional migration.
- [x] Add runtime hierarchy read model and deterministic fixtures.
- [ ] Record the final Windows CI benchmark and merge P5-04A.

## Windows steps

- [ ] Verify the P5-04A 128×128 upgrade/directed-route baseline on Unity `6000.5.3f1`.
- [ ] Connect P5-04B preview and direction visualization.
- [ ] Smoke-test upgrade, direction reversal, cancel, invalid path, and insufficient funds.
- [ ] Verify that metric comparison and congestion overlay use the same read-model versions.
- [ ] Capture the no-demolition bottleneck-recovery experience evidence.

## Verification

### P5-04A

- stable IDs and exact definition values,
- upgrade cost, no-change, insufficient funds, rollback, and occupancy preservation,
- forward/reverse reachability and explicit no-path,
- affected cache invalidation and unrelated cache retention,
- v1 golden migration and v2 round-trip,
- snapshot declaration-order and canonical JSON checksums,
- Simulation assembly Unity-reference guard,
- 128×128 benchmark output in CI logs.

### P5-04B

- preview/commit/cancel command flow,
- direction arrow/pattern agreement with authoritative exit direction,
- metric comparison and congestion-overlay agreement,
- PlayMode CityPrototype bottleneck-recovery fixture.

## Experience acceptance

- 동일한 시작 도시에서 저비용 소형 도로 중심 배치와 고비용 대로 중심 배치가 모두 기본 운영을 지속하지만 서로 다른 유지비·소음·혼잡 구성을 가진다.
- 플레이어가 혼잡 원인을 확인하고 건물을 철거하지 않은 채 도로 upgrade 또는 일방통행 변경으로 대표 병목 하나를 완화한다.
- 변경 때문에 새로 도달 불가가 된 경로는 적용 기간과 차단 reason을 공개 UI에서 확인할 수 있다.

## Done when

- [x] 세 도로 등급이 직접 결과 버프 없이 비용·용량·속도·유지비·소음으로 구분된다.
- [x] upgrade와 방향 변경이 application command로만 수행되고 snapshot v2에 저장·재현된다.
- [ ] 철거 없는 병목 회복 experience acceptance가 통과한다.
- [ ] 양쪽 플랫폼에서 command fixture와 표시 방향이 일치한다.
- [ ] 각 하위 이슈에 OS, Unity 버전, commit/PR, 테스트 및 benchmark 증거를 기록한다.

## Out of scope

- 자유곡선 도로
- 신호 주기·차선 화살표·차선 변경 AI
- 교량·터널·입체교차로
- 산업·주거 전용 마법 modifier
