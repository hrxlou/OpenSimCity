# OpenSimCity 실행 로드맵

이 디렉터리는 제품 로드맵을 1~3일 안에 검토 가능한 실행 이슈 단위로 관리한다. 각 이슈 문서는 목표, 선행 작업, Mac/Windows 단계, 계약, 검증, 완료 조건을 함께 가진다. 여러 제작 slice를 묶는 gate 문서는 `Ready` 전에 1~3일 하위 이슈로 분할한다.

GitHub 실행 현황은 [Workflow Kanban](https://github.com/users/hrxlou/projects/7/views/3)과 [roadmap 이슈 목록](https://github.com/hrxlou/OpenSimCity/issues?q=is%3Aissue%20is%3Aopen%20label%3Aroadmap)에서 확인한다. 로컬 문서와 GitHub 이슈는 제목의 이슈 ID로 대응한다.

## 운영 원칙

- `P0-01` 같은 ID는 로컬 문서, GitHub 이슈, 브랜치, PR에서 동일하게 사용한다.
- 장비별 브랜치를 만들지 않는다. `feature/p1-02-world-grid`처럼 기능별 브랜치를 사용한다.
- Primary 장비가 구현하고 Verify 장비가 같은 커밋을 검증한다. 같은 브랜치를 두 장비에서 동시에 수정하지 않는다.
- 작업 이동 전에는 commit/push, 이동 후에는 `git pull --ff-only`와 `git lfs pull`을 수행한다.
- Scene, Prefab, ScriptableObject의 동시 편집을 피하고 생성·검증 Editor 도구를 우선한다.
- [디자인·UI·아트 기준](../DESIGN.md)은 화면과 에셋의 source of truth다. 시안에 있더라도 해당 Phase에 구현되지 않은 기능과 지표는 노출하지 않는다.
- 아트가 기능 계약을 앞서지 않게 한다. P0~P2 월드는 프리미티브를 사용하고, P3에서 최소 placeholder 세트를 만든다. 에셋 inventory·라이선스 확인·pipeline proof는 준비 작업으로 병행할 수 있지만 제품 통합으로 간주하지 않는다.
- Phase gate를 통과하기 전 다음 Phase의 상태를 `In Progress`로 바꾸지 않는다. 조사와 준비 작업은 허용한다.
- 다음 Phase의 첫 capability 이슈는 이전 Phase의 마지막 gate 이슈를 명시적으로 `Depends on`에 포함한다. Kanban의 `Ready` 판정은 문장상 Phase 순서가 아니라 이 dependency graph를 사용한다.
- Phase gate는 기능·결정성을 증명하는 capability gate와 플레이어가 상태를 이해하고 수정할 수 있는 experience gate를 함께 가진다.
- Experience gate는 [플레이어 경험 게이트 계약](../Specs/PLAYER_EXPERIENCE_CONTRACTS.md)에 따라 해당 Phase에 구현된 기능만 검증한다.
- 새 기능은 `P3.5` 같은 대형 중간 묶음으로 끼워 넣지 않고 선행 capability가 검증된 Phase의 후속 이슈로 추가한다.
- 명목 estimate가 3일을 넘거나 조건부 구현이 섞인 문서는 `Ready` 전에 각각 1–3일인 하위 이슈로 분할하고, gate 문서는 검증·승인만 담당한다.
- 첫 출시 gate를 막지 않는 마일스톤·시설 업그레이드·시그니처 건물·data-only 콘텐츠 후보는 [Post-MVP 후보와 진입 조건](POST_MVP.md)에서 관리한다.

## 장비 역할

| 구분 | Mac | Windows |
|---|---|---|
| 기본 역할 | Core/Simulation/Application, 문서, EditMode 테스트 | Unity 통합, 입력·화면, Blender, PlayMode, Windows 빌드·Profiler |
| Primary가 아닌 경우 | 순수 로직 회귀와 macOS smoke 검증 | 동일 커밋의 Windows import·compile·PlayMode 검증 |
| 산출 증거 | 테스트 XML, 명령 로그, 결정성 fixture | Console 캡처, 빌드 로그, Profiler/화면 캡처 |

## 의존 관계와 이슈

| Phase | Capability gate | Experience gate | 이슈 |
|---|---|---|---|
| P0 기반 | 양쪽에서 같은 프로젝트와 테스트가 재현됨 | 기반 단계. 별도 제품성 판단 없음 | [P0-01](P0-foundation/P0-01-cross-platform-baseline.md), [P0-02](P0-foundation/P0-02-reproducible-project-scenes.md), [P0-03](P0-foundation/P0-03-cross-platform-test-handoff.md), [P0-04](P0-foundation/P0-04-windows-ci-cd-baseline.md) |
| P1 그리드 상호작용 | 64x64 모든 셀을 정확히 선택하며 순수 로직 테스트 가능 | 입력·카메라·셀 선택 결과가 예측 가능함 | [P1-01](P1-grid/P1-01-core-coordinates-determinism.md), [P1-02](P1-grid/P1-02-world-state-grid.md), [P1-03](P1-grid/P1-03-clock-command-queue.md), [P1-04](P1-grid/P1-04-isometric-camera-input.md), [P1-05](P1-grid/P1-05-selection-debug-grid.md) |
| P2 도로·구역·필지 | 도로 접면·외부 연결·접근성 거리가 개발 입력으로 제공됨 | 설치·취소·철거가 가능하고 고립망·개발 불가·접근성 band를 화면에서 구분함 | [P2-00](P2-roads-zones/P2-00-ui-toolkit-foundation.md), [P2-01](P2-roads-zones/P2-01-road-state-commands.md), [P2-02](P2-roads-zones/P2-02-road-graph-external-connection.md), [P2-03](P2-roads-zones/P2-03-zoning-commands.md), [P2-04](P2-roads-zones/P2-04-parcels-development-eligibility.md), [P2-05](P2-roads-zones/P2-05-placement-overlays-cross-platform.md) |
| P3 첫 MVP | 도로·전력·수요·접근성 기반 성장과 월간 경제가 30분 동작 | 성장 정지 원인을 공개 UI에서 확인하고 전력·외부 연결 복구 뒤 성장이 재개되며, 서로 다른 두 도로 배치가 기본 루프를 완주함 | [P3-01](P3-mvp/P3-01-building-definitions-state.md), [P3-02](P3-mvp/P3-02-power-network.md), [P3-03](P3-mvp/P3-03-population-employment-migration.md), [P3-04](P3-mvp/P3-04-market-rci-demand.md), [P3-05](P3-mvp/P3-05-growth-development.md), [P3-06](P3-mvp/P3-06-municipal-economy-hud.md), [P3-07](P3-mvp/P3-07-placeholder-art-deterministic-soak.md) |
| P3–P7 아트 성능 준비 | P8 측정용 production-like renderer/material/texture fixture가 재현됨 | 제품 gate 아님. CityPrototype placeholder와 gameplay를 바꾸지 않음 | [P3-08](P3-mvp/P3-08-production-like-art-performance-slice.md) |
| P4 저장·복원 | 성장 중인 도시가 양쪽 플랫폼에서 동일하게 복원됨 | 저장 전후 공개 원인·가능 명령·다음 기간 결과가 의미상 동일함 | [P4-01](P4-save/P4-01-snapshot-dto-serialization.md), [P4-02](P4-save/P4-02-storage-migration.md), [P4-03](P4-save/P4-03-save-ui-cross-platform-roundtrip.md) |
| P5 집계 교통·교통 설계 | 표시 차량과 무관한 교통 결과, 도로 계층·방향, 집계형 버스 결과와 벤치마크가 존재함 | 철거 없는 도로 변경 또는 버스 투자로 병목을 개선하고 두 전략의 비용·공간 trade-off를 설명함 | [P5-01](P5-traffic/P5-01-trip-demand-matching.md), [P5-02](P5-traffic/P5-02-routing-traffic-aggregation.md), [P5-03](P5-traffic/P5-03-vehicle-views-congestion-benchmark.md), [P5-04](P5-traffic/P5-04-road-hierarchy-upgrade-one-way.md), [P5-05](P5-traffic/P5-05-aggregate-bus-transit.md) |
| P6 입지·쇠퇴 | 입지 악화가 시간 지연을 거쳐 쇠퇴로 설명됨 | 쇠퇴 원인을 두 채널 이상에서 확인하고 조건 회복 뒤 재개발까지 추적함 | [P6-01](P6-decline/P6-01-spatial-condition-fields.md), [P6-02](P6-decline/P6-02-decline-redevelopment-datamaps.md) |
| P7 서비스·밀도·정체성 | 서비스·수도·예산·밀도, 특성화 한 종류, 지구와 정책 두 종류가 도시 상태에 반영됨 | 일반/특성화 도시와 정책 유무 지구가 서로 다른 실행 가능한 운영과 설명 가능한 trade-off를 제공함 | [P7-01](P7-services/P7-01-service-coverage-budget.md), [P7-02](P7-services/P7-02-water-density-progression.md), [P7-03](P7-services/P7-03-dispatch-vehicles-service-ui.md), [P7-04](P7-services/P7-04-specialization-vertical-slice.md), [P7-05](P7-services/P7-05-specialization-experience-gate.md), [P7-06](P7-services/P7-06-district-policy-vertical-slice.md) |
| P8 규모·성능·맵 범위 | 목표 규모의 CPU/GPU/GC 예산과 고정 맵/한 단계 확장 제품 결정을 기록함 | 목표 규모에서도 문제 식별과 명령 feedback이 막히지 않고, 선택한 맵 경계 또는 확장 목표를 이해함 | [P8-01](P8-performance/P8-01-benchmark-fixtures-budgets.md), [P8-02](P8-performance/P8-02-pooling-instancing-lod.md), [P8-03](P8-performance/P8-03-map-expansion-decision-slice.md) |
| P9 출시 준비 | 콘텐츠 catalog·저장 호환을 포함한 Windows/macOS 출시 후보가 검증됨 | 신규 사용자가 첫 15분 안에 핵심 루프와 대표 실패 회복을 완료하고 도시 선택의 시각적 차이를 확인함 | [P9-01](P9-release/P9-01-production-art-pipeline.md), [P9-02A](P9-release/P9-02a-onboarding-release-ui.md), [P9-02B](P9-release/P9-02b-accessibility-input-settings.md), [P9-02C](P9-release/P9-02c-audio-day-night-presentation.md), [P9-02 gate](P9-release/P9-02-ux-tutorial-accessibility.md), [P9-03](P9-release/P9-03-release-builds-save-compatibility.md) |

### Phase gate dependency chain

| 다음 Phase 진입 이슈 | 반드시 완료할 이전 gate |
|---|---|
| `P1-01` | `P0-04` |
| `P2-00` | `P1-05` |
| `P3-01` | `P2-05` |
| `P4-01` | `P3-07` |
| `P5-01` | `P4-03` |
| `P6-01` | `P5-05` |
| `P7-01` | `P6-02` |
| `P8-01` | `P7-06`와 병행 준비 `P3-08` |
| `P9-01` | `P8-03`와 병행 준비 `P3-08` |

이 표는 설명용이며 각 이슈의 `Depends on` 필드가 자동화의 실제 입력이다. `P3-08`은 P3 capability/experience gate가 아니라 P8 전에 완료하는 병행 성능 준비다. `P9-02B`는 `P9-01` 외에 `P2-00`의 token·focus 기반에도 의존하는 Phase 간 예외이므로 이 표에 나타나지 않는다.

P2-02가 후속 이슈에 넘기는 접근성 계약은 cardinal edge 비용 `1`, gate 셀 거리 `0`,
도달 불가 `-1`과 결정적 거리 band다. graph component label과 거리 cache는 도로 셀과 stable
external gate에서 재구축하며 저장하지 않는다. P2-05 overlay와 P3-05 성장 입력은
Simulation 값을 재계산하지 않고 versioned Application read model을 소비한다.

P2-03은 `ZoneType`/stable definition ID, 정규화된 path·area brush, 무료 R/C/I·clear command와
`ZoneCommandResult`/`ZoneReadModel` 변경 집합을 제공한다. 도로 셀·범위 밖·다른 zone 덮어쓰기는
원자적으로 거부하고, Presentation은 zone state를 소유하지 않는다.

P2-04는 같은 zone 셀을 row-major 기준 `1x1~2x2` 직사각형 parcel로 분할하고, stable parcel ID,
방향별 road frontage, 외부 gate 최소 정수 거리·band와 공개 개발 불가 reason을 제공한다. zone/road
version 변경 뒤 실제 공개 값이 바뀐 필지만 dirty로 전달하며, P2-05 overlay와 P3-05 개발 시스템은
`ParcelReadModel`을 통해 이 결과를 소비한다.

P3-01은 Unity `BuildingDefinition` asset을 stable ID, 저밀도 R/C/I, `1x1~2x2` footprint,
점유/일자리 수용량과 문자열 presentation key만 가진 정적 schema로 제한한다. Definitions assembly가
이를 Unity object 없는 Application catalog data로 변환하고 Simulation은 stable definition ID만
저장한다. `BuildingState`는 stable building/definition/parcel ID, anchor, 현재 점유 수와 채워진
일자리를 field-only DTO로 왕복하며, collection lifecycle과 참조 검증은 building ID 순서로
결정적이다. 성장·점유율 계산·전력·prefab 배치는 후속 이슈가 이 계약을 소비해 구현한다.

P3-02는 도로 component를 초기 전력 전달 요소로 사용한다. 발전소/건물은 인접한 row-major 최소
도로 셀 하나에 연결되고, component 용량은 건물 stable ID 순서로 전량 배분된다. 공급 cache와
component label은 저장하지 않으며 road/building/producer version에서 재구축한다. 전력 overlay는
같은 `PowerReadModel` snapshot을 표시하고, 성장용 query는 P2 도로 자격과 남은 전력 용량을 모두 요구한다.

P3-03은 모든 거주자를 현재 단계의 노동 가능 인구로 집계하고, 건물 stable ID 순서로 job capacity에
matching한다. Application scheduler가 command 처리 뒤 월 경계마다 인구 정규화, 고용, 이주, 최종 고용을
실행한다. 활성 교통 외부 연결이 있을 때 주택 여유와 빈 일자리의 최소값만큼 유입되고 실업자만큼
유출되며, capacity 감소 초과분은 강제 이탈로 정규화된다. 공개 수치는 versioned
`PopulationEmploymentReadModel` snapshot에서 읽고 시민·통근·표시 차량은 집계에 관여하지 않는다.

## 디자인·아트 단계 적용

| 시점 | 제품 화면에 적용 | 준비 작업 |
|---|---|---|
| P1 | grid hover·selection·camera debug 표현 | UI token smoke와 reference 정리 |
| P2 | P2-00 UI Toolkit/token/focus 기반 뒤 Tool Rail, Build Palette, Context Inspector, 배치 preview와 overlay | 외부 에셋 license·inventory 확인 |
| P3–P7 | 구현된 기능만 표시하는 HUD·화면, P3 CityPrototype은 primitive placeholder | P3-08 Benchmark 전용 production-like 대표 에셋 slice |
| P8 | 실제 fixture 기반 render/UI budget과 LOD·batching 결정 | production validator 기준 확정 |
| P9 | 한국 도시화 production set, 출시 메뉴·튜토리얼·접근성 polish | vendor provenance와 배포 감사 |

AI 시안은 레이아웃 참고 자료다. 완료 증거는 실제 Unity game render, query와 일치하는 UI 상태, 양 플랫폼 smoke와 benchmark로 남긴다.

## 권장 칸반 상태

`Backlog → Ready → In Progress → Verify on Other Device → Done`

Post-MVP 후보는 진입 조건을 충족하고 별도 승인되기 전까지 이 칸반의 `Ready`로 올리지 않는다.

GitHub Project의 `Workflow` 필드를 칸반 상태의 기준으로 사용한다. 각 카드에는 `Phase`와 `Primary Device`가 표시되며, 상태 라벨은 초기 검색과 보조 표시 용도다.

- `Ready`: 선행 이슈와 필요한 계약이 완료됨
- `In Progress`: Primary 장비에서 구현 중
- `Verify on Other Device`: 구현 커밋이 push됐고 Verify 장비에서 확인 중
- `Done`: 양쪽 증거와 완료 조건이 이슈에 기록됨

## 이슈 시작·종료

시작할 때 해당 문서의 체크리스트를 GitHub 이슈에 반영하고 브랜치를 만든다. 종료할 때 테스트 결과, 사용한 Unity 버전, 검증 OS, 관련 커밋/PR, 화면 변경 증거를 이슈에 남긴다. 공통 형식은 [ISSUE_TEMPLATE.md](ISSUE_TEMPLATE.md)를 따른다.
