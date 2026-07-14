# OpenSimCity 실행 로드맵

이 디렉터리는 제품 로드맵을 1~3일 안에 검토 가능한 이슈 단위로 관리한다. 각 이슈 문서는 목표, 선행 작업, Mac/Windows 단계, 계약, 검증, 완료 조건을 함께 가진다.

GitHub 실행 현황은 [Workflow Kanban](https://github.com/users/hrxlou/projects/7/views/3)과 [roadmap 이슈 목록](https://github.com/hrxlou/OpenSimCity/issues?q=is%3Aissue%20is%3Aopen%20label%3Aroadmap)에서 확인한다. 로컬 문서와 GitHub 이슈는 제목의 이슈 ID로 대응한다.

## 운영 원칙

- `P0-01` 같은 ID는 로컬 문서, GitHub 이슈, 브랜치, PR에서 동일하게 사용한다.
- 장비별 브랜치를 만들지 않는다. `feature/p1-02-world-grid`처럼 기능별 브랜치를 사용한다.
- Primary 장비가 구현하고 Verify 장비가 같은 커밋을 검증한다. 같은 브랜치를 두 장비에서 동시에 수정하지 않는다.
- 작업 이동 전에는 commit/push, 이동 후에는 `git pull --ff-only`와 `git lfs pull`을 수행한다.
- Scene, Prefab, ScriptableObject의 동시 편집을 피하고 생성·검증 Editor 도구를 우선한다.
- 아트가 기능 계약을 앞서지 않게 한다. P0~P2는 프리미티브를 사용하고, P3에서 최소 placeholder 세트를 만든다.
- Phase gate를 통과하기 전 다음 Phase의 상태를 `In Progress`로 바꾸지 않는다. 조사와 준비 작업은 허용한다.

## 장비 역할

| 구분 | Mac | Windows |
|---|---|---|
| 기본 역할 | Core/Simulation/Application, 문서, EditMode 테스트 | Unity 통합, 입력·화면, Blender, PlayMode, Windows 빌드·Profiler |
| Primary가 아닌 경우 | 순수 로직 회귀와 macOS smoke 검증 | 동일 커밋의 Windows import·compile·PlayMode 검증 |
| 산출 증거 | 테스트 XML, 명령 로그, 결정성 fixture | Console 캡처, 빌드 로그, Profiler/화면 캡처 |

## 의존 관계와 이슈

| Phase | Gate | 이슈 |
|---|---|---|
| P0 기반 | 양쪽에서 같은 프로젝트와 테스트가 재현됨 | [P0-01](P0-foundation/P0-01-cross-platform-baseline.md), [P0-02](P0-foundation/P0-02-reproducible-project-scenes.md), [P0-03](P0-foundation/P0-03-cross-platform-test-handoff.md) |
| P1 그리드 상호작용 | 64x64 모든 셀을 정확히 선택하며 순수 로직 테스트 가능 | [P1-01](P1-grid/P1-01-core-coordinates-determinism.md), [P1-02](P1-grid/P1-02-world-state-grid.md), [P1-03](P1-grid/P1-03-clock-command-queue.md), [P1-04](P1-grid/P1-04-isometric-camera-input.md), [P1-05](P1-grid/P1-05-selection-debug-grid.md) |
| P2 도로·구역·필지 | 도로 접면과 외부 연결이 개발 가능성을 결정함 | [P2-01](P2-roads-zones/P2-01-road-state-commands.md), [P2-02](P2-roads-zones/P2-02-road-graph-external-connection.md), [P2-03](P2-roads-zones/P2-03-zoning-commands.md), [P2-04](P2-roads-zones/P2-04-parcels-development-eligibility.md), [P2-05](P2-roads-zones/P2-05-placement-overlays-cross-platform.md) |
| P3 첫 MVP | 도로·전력·수요 기반 성장과 월간 경제가 30분 동작 | [P3-01](P3-mvp/P3-01-building-definitions-state.md), [P3-02](P3-mvp/P3-02-power-network.md), [P3-03](P3-mvp/P3-03-population-employment-migration.md), [P3-04](P3-mvp/P3-04-market-rci-demand.md), [P3-05](P3-mvp/P3-05-growth-development.md), [P3-06](P3-mvp/P3-06-municipal-economy-hud.md), [P3-07](P3-mvp/P3-07-placeholder-art-deterministic-soak.md) |
| P4 저장·복원 | 성장 중인 도시가 양쪽 플랫폼에서 동일하게 복원됨 | [P4-01](P4-save/P4-01-snapshot-dto-serialization.md), [P4-02](P4-save/P4-02-storage-migration.md), [P4-03](P4-save/P4-03-save-ui-cross-platform-roundtrip.md) |
| P5 집계 교통 | 표시 차량과 무관한 교통 결과와 벤치마크가 존재함 | [P5-01](P5-traffic/P5-01-trip-demand-matching.md), [P5-02](P5-traffic/P5-02-routing-traffic-aggregation.md), [P5-03](P5-traffic/P5-03-vehicle-views-congestion-benchmark.md) |
| P6 입지·쇠퇴 | 입지 악화가 시간 지연을 거쳐 쇠퇴로 설명됨 | [P6-01](P6-decline/P6-01-spatial-condition-fields.md), [P6-02](P6-decline/P6-02-decline-redevelopment-datamaps.md) |
| P7 서비스·밀도 | 서비스·수도·예산이 밀도와 도시 상태에 반영됨 | [P7-01](P7-services/P7-01-service-coverage-budget.md), [P7-02](P7-services/P7-02-water-density-progression.md), [P7-03](P7-services/P7-03-dispatch-vehicles-service-ui.md) |
| P8 규모·성능 | 목표 규모의 CPU/GPU/GC 예산을 양쪽에서 기록함 | [P8-01](P8-performance/P8-01-benchmark-fixtures-budgets.md), [P8-02](P8-performance/P8-02-pooling-instancing-lod.md) |
| P9 출시 준비 | 신규 사용자 루프와 Windows/macOS 출시 후보가 검증됨 | [P9-01](P9-release/P9-01-production-art-pipeline.md), [P9-02](P9-release/P9-02-ux-tutorial-accessibility.md), [P9-03](P9-release/P9-03-release-builds-save-compatibility.md) |

## 권장 칸반 상태

`Backlog → Ready → In Progress → Verify on Other Device → Done`

GitHub Project의 `Workflow` 필드를 칸반 상태의 기준으로 사용한다. 각 카드에는 `Phase`와 `Primary Device`가 표시되며, 상태 라벨은 초기 검색과 보조 표시 용도다.

- `Ready`: 선행 이슈와 필요한 계약이 완료됨
- `In Progress`: Primary 장비에서 구현 중
- `Verify on Other Device`: 구현 커밋이 push됐고 Verify 장비에서 확인 중
- `Done`: 양쪽 증거와 완료 조건이 이슈에 기록됨

## 이슈 시작·종료

시작할 때 해당 문서의 체크리스트를 GitHub 이슈에 반영하고 브랜치를 만든다. 종료할 때 테스트 결과, 사용한 Unity 버전, 검증 OS, 관련 커밋/PR, 화면 변경 증거를 이슈에 남긴다. 공통 형식은 [ISSUE_TEMPLATE.md](ISSUE_TEMPLATE.md)를 따른다.
