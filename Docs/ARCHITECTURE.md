# OpenSimCity 아키텍처

## 1. 설계 목표

이 구조는 세 가지 문제를 먼저 막는다.

1. 수천 개 GameObject가 각자 시뮬레이션을 실행하는 구조
2. Unity 표현 코드와 도시 규칙이 서로 강하게 결합되는 구조
3. 교통·전력·RCI 등 모든 시스템이 서로 직접 참조해 변경하기 어려워지는 구조

도시 상태는 데이터로 관리하고, Unity 오브젝트는 그 상태를 보여주거나 플레이어 명령을 만드는 역할만 맡는다.

## 2. 최상위 계층

```text
UI / Input
    |
    v
Gameplay Commands -----------+
    |                         |
    v                         |
Application / Orchestration  |
    |                         |
    v                         |
Simulation Core <------------+
    |
    +----> Read Models / Events ----> Presentation
    |
    +----> Save Snapshot
```

### Core

좌표, ID, 시간, 수학 유틸리티, 결정적 난수 등 어느 기능에도 종속되지 않는 기반 타입을 둔다. 가능하면 `UnityEngine` 의존성을 갖지 않는다.

Core의 좌표와 결정성 규칙은 다음과 같다.

- `CellCoord`와 `ChunkCoord`는 `Y`를 먼저 비교하고 같은 행에서 `X`를 비교하는 row-major 순서를 사용한다. 음수 셀의 chunk 변환은 0 방향 절삭이 아니라 floor 나눗셈을 사용한다.
- `GetHashCode()`는 컬렉션 버킷용일 뿐 저장 데이터나 난수 seed로 사용하지 않는다. 영속 입력에는 명시적인 결정적 hash만 사용한다.
- `SimulationTick`과 `CommandSequence`는 최댓값에서 wrap하지 않고 overflow를 실패로 보고한다. 순서를 유지할 수 없는 상태를 조용히 다음 값으로 바꾸지 않는다.
- 시스템별 난수는 `DeterministicRandom.CreateStream(seed, subsystemKey)`로 분리한다. subsystem key는 런타임의 `string.GetHashCode()`가 아닌 명시된 ordinal hash로 파생하며, 각 stream의 `State`를 저장할 수 있다.

### Simulation

그리드, 건물, 인구, RCI, 경제, 전력, 교통 등 도시의 실제 상태와 규칙을 가진다. Scene, Prefab, Material, UI 타입을 참조하지 않는다.

### Application

플레이어 명령을 검증하고 시뮬레이션 시스템의 실행 순서를 조정한다. 저장/불러오기, 새 도시 생성, 게임 속도도 여기서 조율한다.

### Gameplay

셀 선택, 도로 드래그, 구역 칠하기, 철거처럼 입력을 도시 명령으로 변환한다. 상태를 직접 수정하지 않는다.

P1 카메라는 Gameplay 안에서 별도의 view state로 동작한다. `IsometricCameraRig`는
`WorldBoundsReadModel`만 받아 64x64 bounds, 화면 비율별 ground footprint, orthographic zoom을
계산하며 Simulation의 셀 배열이나 Scene 이름을 조회하지 않는다. 카메라가 소비한 중클릭 drag는
`CameraPointerInteractionState.ShouldSuppressClick`으로 노출해 선택 도구의 click 경로와 분리한다.
WASD, 휠, Q/E action은 `IsometricCamera.inputactions`에 고정하고 Editor 생성 도구가
`CityPrototype`의 rig·camera·read-model source 참조를 재현 가능하게 구성한다.

P1-05의 `GridSelectionTool`은 별도 `Selection` action map에서 좌클릭을 받아 화면 ray를
ground plane으로 투영한 뒤 `GridSelectionMath`로 `CellCoord` 조회 결과만 만든다. 선택은
simulation state를 변경하지 않으며, camera drag 억제 상태와 UI pointer 차단을 먼저 확인한다.
`GridDebugGridRenderer`는 bounds read model로 만든 단일 line mesh를, highlight는 재사용되는
단일 `LineRenderer`를 사용하므로 선택 변경마다 GameObject를 만들지 않는다.

### Presentation

시뮬레이션의 읽기 모델과 이벤트를 받아 지형, 도로, 건물, 차량, 데이터맵을 표현한다. 보이는 차량 수와 실제 통행량은 독립적이다.

### UI

예산, RCI, 툴바, 선택 정보, 경고를 표시한다. 시뮬레이션 내부 배열을 직접 읽지 않고 공개된 조회 API나 읽기 모델을 사용한다.

UI와 Presentation의 시각 token, component, 화면 슬롯과 Phase별 노출 범위는 [디자인·UI·아트 기준](DESIGN.md)을 따른다. 디자인 문서는 표현 계약이며 Simulation/Core의 참조 대상이 아니다.

### Editor / Tests

Scene·Prefab·정의 데이터 생성, 검증, 벤치마크, EditMode/PlayMode 테스트를 둔다. Scene YAML을 자동화 스크립트 없이 대규모로 직접 편집하지 않는다.

## 3. 어셈블리 경계

초기부터 asmdef를 나누되 너무 잘게 쪼개지는 않는다.

| Assembly | 책임 | 참조 가능 대상 |
|---|---|---|
| `OpenSimCity.Core` | 공통 값 타입, ID, 시간, 유틸리티 | 없음 |
| `OpenSimCity.Simulation` | 도시 상태와 순수 규칙 | Core |
| `OpenSimCity.Application` | 명령, 틱 조율, 저장 포트 | Core, Simulation |
| `OpenSimCity.Definitions` | Unity ScriptableObject 정적 정의 schema와 순수 catalog data 변환 | Core, Simulation, Application, UnityEngine |
| `OpenSimCity.Gameplay` | 입력과 건설 도구 | Core, Application |
| `OpenSimCity.Presentation` | 월드 렌더링과 뷰 동기화 | Core, Application |
| `OpenSimCity.UI` | 화면 UI | Core, Application |
| `OpenSimCity.Editor` | 생성·검증 도구 | 모든 Runtime assembly, Editor 전용 |
| `OpenSimCity.Tests.EditMode` | 순수 로직 테스트 | Core, Simulation, Application, Gameplay |
| `OpenSimCity.Tests.PlayMode` | Scene/UI/입력 통합 테스트 | Runtime assemblies |

금지되는 참조 방향:

- Simulation -> Presentation/UI/Gameplay/Editor
- Simulation/Application -> Definitions
- Core -> 다른 모든 계층
- Runtime -> Editor
- UI -> 구체적인 렌더러 또는 Scene 오브젝트

교통, 경제처럼 코드가 커진 모듈은 실제 컴파일 시간과 팀 충돌이 문제가 될 때 별도 asmdef로 분리한다. 처음부터 시스템마다 asmdef를 만들지는 않는다.

## 4. 권장 디렉터리

외부 에셋과 프로젝트 코드를 섞지 않기 위해 모든 자체 Unity 자산을 `Assets/OpenSimCity` 아래에 둔다.

```text
OpenSimCity/
├─ Assets/
│  ├─ OpenSimCity/
│  │  ├─ Runtime/
│  │  │  ├─ Core/
│  │  │  │  ├─ Collections/
│  │  │  │  ├─ Determinism/
│  │  │  │  ├─ Mathematics/
│  │  │  │  └─ Time/
│  │  │  ├─ Simulation/
│  │  │  │  ├─ World/
│  │  │  │  ├─ Roads/
│  │  │  │  ├─ Parcels/
│  │  │  │  ├─ Zones/
│  │  │  │  ├─ Buildings/
│  │  │  │  ├─ ExternalConnections/
│  │  │  │  ├─ Employment/
│  │  │  │  ├─ Migration/
│  │  │  │  ├─ Markets/
│  │  │  │  ├─ Population/
│  │  │  │  ├─ Demand/
│  │  │  │  ├─ Economy/
│  │  │  │  ├─ Utilities/
│  │  │  │  ├─ Traffic/
│  │  │  │  ├─ Transit/
│  │  │  │  ├─ Services/
│  │  │  │  ├─ Districts/
│  │  │  │  ├─ Development/
│  │  │  │  └─ Statistics/
│  │  │  ├─ Application/
│  │  │  │  ├─ Commands/
│  │  │  │  ├─ Queries/
│  │  │  │  ├─ Persistence/
│  │  │  │  └─ Bootstrap/
│  │  │  ├─ Definitions/
│  │  │  │  └─ Buildings/
│  │  │  ├─ Gameplay/
│  │  │  │  ├─ Camera/
│  │  │  │  ├─ Selection/
│  │  │  │  └─ Construction/
│  │  │  ├─ Presentation/
│  │  │  │  ├─ World/
│  │  │  │  ├─ Roads/
│  │  │  │  ├─ Buildings/
│  │  │  │  ├─ Vehicles/
│  │  │  │  └─ Overlays/
│  │  │  └─ UI/
│  │  │     ├─ HUD/
│  │  │     ├─ Toolbars/
│  │  │     ├─ Inspectors/
│  │  │     └─ Charts/
│  │  ├─ Content/
│  │  │  ├─ Art/
│  │  │  │  ├─ Buildings/
│  │  │  │  ├─ Roads/
│  │  │  │  ├─ Vehicles/
│  │  │  │  └─ Environment/
│  │  │  ├─ UI/
│  │  │  │  ├─ Styles/
│  │  │  │  ├─ Fonts/
│  │  │  │  └─ Icons/
│  │  │  ├─ Audio/
│  │  │  ├─ Materials/
│  │  │  ├─ Prefabs/
│  │  │  └─ Definitions/
│  │  │     ├─ Buildings/
│  │  │     ├─ Roads/
│  │  │     ├─ Utilities/
│  │  │     ├─ Services/
│  │  │     ├─ Transit/
│  │  │     ├─ Policies/
│  │  │     └─ Economy/
│  │  ├─ Scenes/
│  │  │  ├─ Bootstrap.unity
│  │  │  ├─ MainMenu.unity
│  │  │  ├─ CityPrototype.unity
│  │  │  └─ Benchmark.unity
│  │  ├─ Settings/
│  │  ├─ Editor/
│  │  └─ Tests/
│  │     ├─ EditMode/
│  │     ├─ PlayMode/
│  │     └─ Fixtures/
│  └─ ThirdParty/
├─ Packages/
├─ ProjectSettings/
├─ UserSettings/                 # 커밋하지 않음
├─ Blender/
│  ├─ Source/
│  │  ├─ External/
│  │  └─ OpenSimCity/
│  ├─ Export/
│  ├─ Scripts/
│  │  ├─ Importers/
│  │  ├─ Generators/
│  │  ├─ Validators/
│  │  ├─ Exporters/
│  │  └─ Previews/
│  └─ References/
├─ Docs/
│  ├─ DESIGN.md
│  ├─ Decisions/
│  ├─ Specs/
│  ├─ Benchmarks/
│  └─ References/
├─ Tools/
│  ├─ Bootstrap/
│  ├─ Build/
│  └─ CI/
├─ .github/
│  └─ workflows/
├─ AGENTS.md
└─ README.md
```

### 디렉터리 규칙

- 원본 `.blend`는 `Blender/Source`, 내보낸 FBX/glTF는 `Blender/Export`에서 관리한다.
- Unity runtime asset은 source 소유권에 따라 나눈다. vendor 원본과 호환성 정규화 mesh/texture는 `Assets/ThirdParty`, 자체 제작 art와 프로젝트 wrapper prefab/material/definition은 `Assets/OpenSimCity`에 둔다.
- 외부 source는 `Blender/Source/External`, 자체 제작·변형 source는 `Blender/Source/OpenSimCity`에 둔다. 임시 art 결과와 report는 gitignore된 `Artifacts/Art`를 사용한다.
- vendor runtime asset과 호환성 정규화 결과는 `Assets/ThirdParty`에 두고 source URL, hash, license와 변형 provenance를 보존한다.
- 에셋 파일명은 definition ID나 prefab key가 아니다. 런타임과 저장 데이터는 이동·rename에 안정적인 ID를 사용한다.
- ScriptableObject는 정적 정의다. 인구, 점유율, 현재 전력 같은 런타임 상태를 저장하지 않는다.
- 공용 `Scripts/Managers`, `Misc`, `Utils` 폴더를 만들지 않는다. 책임이 드러나는 모듈에 둔다.
- 외부 패키지 파일을 `Assets/OpenSimCity` 아래로 복사하지 않는다.

## 5. 핵심 런타임 모델

### 정적 정의

에디터에서 작성하는 변경이 적은 데이터다.

- `BuildingDefinition`: 용도, 밀도, 크기, 수용 인구/일자리, 수요량, Prefab key
- `RoadDefinition`: 방향 지원, 집계 차선/용량, 속도, 건설비, 유지비, 소음 입력
- `UtilityDefinition`: 생산량, 반경/연결 규칙, 유지비
- `ServiceDefinition`: 수용량, 효과, 범위, 예산 민감도
- `TransitDefinition`: 정류장 접근 규칙, 차량 집계 용량, 기본 운행비와 대기 비용
- `PolicyDefinition`: 적용 가능한 입력 factor, 비용, 적용 기간과 표시 key

정의는 문자열 이름이 아니라 안정적인 ID로 런타임 상태와 연결한다.

### 런타임 상태

배열 또는 조밀한 컬렉션으로 관리하는 저장 가능한 데이터다.

- `WorldState`: 맵 크기, 시드, 셀/청크
- `RoadNetworkState`: row-major 도로 셀 점유와 변경 batch version
- `TransitState`: stable 정류장·노선 ID, 정렬된 정류장 순서, 예산, 집계 승객·용량
- `DistrictState`: stable 지구 ID, 셀 membership, 이름, 활성 정책과 적용 기간
- `BuildingState`: definition ID, 위치, 점유율, 상태, 공급 여부
- `ExternalConnectionState`: 도시 밖과 연결되는 도로·철도·항만·유틸리티 게이트웨이
- `PopulationState`: 건물 또는 코호트 단위 인구·노동자·학생
- `EmploymentState`: 노동 가능 인구, 충원된 일자리, 실업자, 빈 일자리
- `MigrationState`: 외부 유입·유출 압력과 월별 실제 이동량
- `UrbanEconomyState`: 집계된 생산, 상품 공급, 소비력, 사업체 가동률
- `MunicipalFinanceState`: 시 재정, 세율, 건설비, 유지비, 수입/지출 원장
- `DemandState`: R/C/I 및 부유도·산업 유형별 수요
- `CityStatistics`: UI와 규칙이 사용할 집계값

MonoBehaviour가 이 데이터의 진실 원천이 되어서는 안 된다.

### 월별 인구·고용·이주 계약

P3-03의 `PopulationEmploymentMigrationSystem`은 `BuildingStateCollection`과 Unity object 없는
definition profile을 입력으로 사용한다. 현재 단계에서는 연령·교육 cohort를 만들지 않고 모든
거주자를 노동 가능 인구로 간주한다. 주거 점유와 일자리 충원은 건물 stable ID 순서로 처리하며,
각 값은 definition capacity의 `0..capacity` 범위를 벗어나지 않는다.

월별 순서는 Application의 `MonthlySimulationScheduler`가 소유한다. 같은 tick의 command를 먼저
처리한 뒤 인구 정규화, 고용 matching, 이주 압력 적용, 최종 고용 재계산 순으로 한 번 실행한다.
활성 Road/Rail/Sea/Air 외부 연결이 있을 때만 주택 여유와 빈 일자리의 최소값만큼 유입되고,
실업자 수만큼 유출된다. capacity 감소로 이미 상한을 넘은 점유는 강제 이탈로 정규화한다.
HUD나 inspector는 simulation collection 대신 versioned `PopulationEmploymentReadModel` snapshot을
소비한다. 시민·통근자 GameObject와 표시 차량 수는 이 계산에 참여하지 않는다.

### WorldState/grid 저장 계약

`WorldState`의 기본 맵은 64x64 셀과 16x16 청크다. 생성 DTO에서 width, height,
chunk size, seed를 받아 셀 배열과 청크 인덱스를 한 번 생성하며, 청크 인덱스와
dirty 표식은 셀 상태에서 파생되는 런타임 데이터다.

- 맵 좌표는 `0 <= X < width`, `0 <= Y < height`인 로컬 좌표다.
- 셀 순회와 셀 배열 index는 `Y` 오름차순 안에서 `X` 오름차순인
  `index = Y * width + X` row-major 규칙을 사용한다.
- 청크 순회와 청크 index도 같은 규칙을 사용한다. `chunkColumns`는
  `ceil(width / chunkSize)`이며 `chunkIndex = chunkY * chunkColumns + chunkX`다.
- 맵 크기가 chunk size로 나누어지지 않으면 마지막 행·열 청크는 실제 남은
  셀 수만 가진다. 청크의 첫 셀 index는 주소 정보이며 청크 내부 행이 셀 배열에서
  하나의 연속 구간이라고 가정하지 않는다.
- dirty 상태는 변경된 청크의 index만 표시한다. `Try` 조회는 범위 밖에서
  `false`를 반환하고, 검증된 접근 API는 명시적 범위 예외를 보고한다.

### 읽기 모델

UI와 렌더링에 필요한 최소 데이터만 내보낸다.

- 변경된 셀/청크
- 생성·변경·철거된 건물
- 도로별 혼잡도
- 노선별 승객·용량·자동차 대체 통행과 상위 차단 reason
- 지구별 인구·고용·통행·서비스·오염·재정과 정책 적용 기간
- RCI, 재정, 인구 통계
- 도시 시장 압력과 개발 가능한 필지 수를 구분한 개발 상태
- 결정적으로 정렬된 상위 차단 reason, factor, 적용 기간
- 플레이어에게 보여줄 경고와 사건

매 틱 전체 도시 복사 대신 변경 집합과 주기적 전체 스냅샷을 병행한다.

## 6. 명령과 이벤트

플레이어 입력은 즉시 배열을 수정하지 않고 명령을 만든다.

```text
PlaceRoadCommand
UpgradeRoadCommand
ZoneAreaCommand
BulldozeCommand
PlaceUtilityCommand
SetTaxRateCommand
CreateTransitLineCommand
PaintDistrictCommand
SetDistrictPolicyCommand
```

명령 처리 결과는 성공/실패와 비용, 영향을 받은 영역을 반환한다. 성공한 변경은 도메인 이벤트와 Dirty Flag를 발생시킨다.

P2 도로 명령은 이 결과를 `RoadCommandResult` DTO로 제공하며, preview와 commit이 같은
검증·비용·변경 셀 집합을 공유한다. Application dispatcher의 상세 payload는 UI가 실패
사유와 비용을 다시 계산하지 않고 표시할 수 있도록 전달된다.

P2-03의 `ZoneType`은 `None=0`, `ResidentialLow=1`, `CommercialLow=2`,
`IndustrialLow=3`의 안정 enum이며 각 셀은 zone type과 stable definition ID를 함께 가진다.
`ZoneAreaCommand`/`ZonePathCommand`와 `ClearZoneCommand`는 brush 셀을 중복 제거한 뒤
row-major(`Y`, `X`) 순서로 정규화한다. zone 칠하기는 도로 셀·범위 밖·다른 zone 덮어쓰기를
원자적으로 거부하고, 같은 zone 재적용과 clear는 멱등이다. P2에서는 zone 비용을 무료로
고정해 `ZoneCommandResult.Cost`를 `0`으로 반환한다.

`ZoneCommandResult`는 preview와 commit의 requested/changed/rejected 셀과 실패 사유를
공개하고, `ZoneReadModel`은 `ZoneNetworkState.Version`을 기준으로 전체 셀 snapshot과
마지막 refresh의 정확한 변경 셀을 Application 소유 DTO로 복사한다. Presentation과 UI는
이 read model을 소비하며 zone 규칙이나 색/material을 재계산하지 않는다.

P2-02의 `RoadConnectivityGraph`는 `RoadNetworkState`에서 다시 만들 수 있는 cardinal
연결 그래프와 외부 road gate 거리 cache다. 그래프 version은 성공한 도로 batch의
`RoadNetworkState.Version`을 따르고, 각 rebuild는 이전 공개 query와 비교해 실제 값이
달라진 row-major 셀 목록과 그 bounding dirty region을 남긴다. component label은 component의
최소 row-major cell index를 사용해 무관한 component가 지역 편집 때문에 다시 번호 매겨지지
않게 하지만, stable entity ID나 저장 계약은 아니다. 저장에는 도로 셀과 stable external gate
ID만 필요하며 graph, component, 거리 cache는 load 뒤 재구축한다.

외부 접근성은 활성 `Road` gate가 지도 경계의 점유 도로 셀에 있을 때만 시작한다. gate 셀은
거리 0, cardinal edge는 비용 1이며 동일 거리 gate가 여럿이면 가장 작은 stable gate ID를
선택한다. `RoadAccessibilityReadModel`은 이 결과를 Application 소유 DTO로 version당 한 번
복사한다. Presentation과 UI는 이 read model의 connection mask, 거리, band, reason을 그대로
표시하고 road view에서 다시 계산하지 않는다.

일시정지, 배속, 저장 요청은 도시 상태를 변경하는 명령과 분리된 Application control이다. 배속이 달라도 같은 수의 시뮬레이션 틱과 같은 도시 명령을 적용했다면 도시 결과는 같아야 한다.

모든 도시 명령에는 적용할 `SimulationTick`과 해당 틱 안에서 단조 증가하는 `CommandSequence`를 붙인다. 시뮬레이션은 이 두 값을 기준으로 명령을 정렬한다.

P1-03의 Application 구현은 렌더 프레임과 시뮬레이션을 `SimulationClock`으로 분리한다.
새 clock의 첫 실행 틱은 0이며 기본 고정 간격은 0.1초, 한 프레임에서 소비할 수 있는
최대 catch-up은 4틱이다. pause 중에는 실시간 delta를 누적하지 않고, catch-up 상한을
넘긴 delta는 무제한 backlog로 남기지 않고 버린다. 배속은 `Paused`, `One`, `Two`,
`Four` application control만 허용한다.

`CommandQueue`는 미래 틱 명령을 받아 `(SimulationTick, CommandSequence)` 순으로
정렬하고 동일 stamp를 거부한다. 이미 처리한 틱의 명령도 거부한다. 고정 틱마다
`CommandDispatcher`가 등록된 handler 하나를 선택하고 검증한 뒤 실행하므로 Gameplay와
View가 Simulation 배열을 직접 수정하지 않는다. 처리 결과는 적용·거부·미지원·실패를
포함한 결정적 순서의 결과 목록으로 반환한다.

```text
RoadNetworkChanged
TransitLineChanged
DistrictChanged
ZoneChanged
BuildingConstructed
BuildingAbandoned
UtilityNetworkChanged
BudgetSettled
```

이벤트는 동일 프레임에 무제한 연쇄 실행하지 않는다. 큐에 모아 정해진 단계에서 소비해 실행 순서를 재현 가능하게 만든다.

## 7. 시뮬레이션 실행 순서

렌더 프레임과 시뮬레이션 시간을 분리한다.

### 프레임

- 입력 수집
- 카메라
- 보간된 차량/애니메이션
- UI와 렌더링

### 고정 시뮬레이션 스텝

1. `(SimulationTick, CommandSequence)` 순으로 도시 명령 적용
2. 그리드·도로·필지·외부 연결 변경 처리
3. Dirty 전력/수도 네트워크 재계산과 경로 캐시 무효화
4. 기간 시작 시점의 건물·인구·고용 스냅샷 확정
5. 스냅샷 기반 통행량·혼잡 갱신
6. 오염·토지가치·서비스 필드 갱신
7. 건물 생산·소비·운영 상태 갱신
8. 고용 매칭, 이주, 입주·퇴거 갱신
9. 인구·일자리·상품·도시 통계 집계
10. 도시 전체 RCI 시장 압력 계산
11. 필지 입지 적합도와 개발 실행 가능성 평가
12. 성장·쇠퇴 후보 처리
13. 시 재정 정산
14. 변경 집합과 이벤트 발행

모든 단계를 매 스텝 실행할 필요는 없다. 전력은 Dirty 시, 교통은 일 단위, 이주·RCI·재정은 월 단위처럼 스케줄링한다. 한 기간의 교통은 기간 시작 스냅샷을 사용하고 그 결과가 다음 점유율·이주 갱신에 반영되는 식으로 순환 의존의 시차를 명시한다.

## 8. 시스템별 최소 계약

| 시스템 | 주요 입력 | 주요 출력 | 최초 구현 방식 |
|---|---|---|---|
| World/Grid | 좌표, 명령 | 셀·청크 상태 | 64x64, 16x16 청크 |
| Roads | 도로 명령 | 연결 그래프, 외부 연결 접근성 | 격자 직선 도로 + 외부 gate까지 정수 거리 |
| Parcels | 도로·구역 | 개발 가능한 필지·접근성 factor | 1x1~2x2 단순 필지 |
| External Connections | 지도 경계, 도로망 | 외부 시장·이주 도달성 | 경계 도로 게이트웨이 |
| Utilities | 생산시설·연결 | 건물 공급 여부 | 연결 요소 + 총용량 |
| Employment | 인구·일자리·도달성 | 취업자·실업자·빈 일자리 | 건물/권역 단위 집계 매칭 |
| Migration | 주택·일자리·도시 매력도 | 유입·유출 코호트 | 월별 집계 이동 |
| Markets | 인구·산업·상업 | 소비력·상품 공급·가동률 | 집계된 상품 한 종류 |
| Demand | 고용·주택·시장·세율 | -1~1 RCI 시장 압력 | 단순 가중합 + 평활화 |
| Development | 시장 압력·입지·필지 | 건물 생성/쇠퇴 | 월별 제한된 후보 샘플 |
| Municipal Finance | 건물·시설·세율 | 시 예산 원장 | 월간 정산 |
| Traffic | OD 통행·도로 | 교통량·통근시간 | 청크 집계 + 경로 캐시 |
| Services | 시설·예산 | 셀별 서비스 효과 | 거리 감쇠 필드 |
| Persistence | 전체 런타임 상태 | 버전 스냅샷 | JSON 디버그 후 바이너리 검토 |

P2 필지는 zone 셀을 row-major anchor부터 같은 용도의 최대 `2x2` 직사각형으로 탐욕 분할한다.
가능한 크기는 `1x1`, `2x1`, `1x2`, `2x2`이며 비정형 결합이나 건물 prefab footprint 기반 분할은
하지 않는다. `ParcelState`의 stable ID는 parcel namespace, anchor의 world row-major index, zone
type을 충돌 없이 조합한다. 따라서 같은 크기의 world에서 anchor와 zone이 유지되는 필지는 도로
접근성만 바뀌어도 ID가 유지된다.

`ParcelNetworkState`는 zone version과 road graph version을 관찰한다. 재계산 후보가 이전 공개
값과 같으면 기존 `ParcelState` 객체를 재사용하고, 추가·삭제·자격 또는 frontage가 달라진 필지만
`ChangedParcelIds`와 row-major `DirtyCells`에 포함한다. 개발 자격은 같은 zone으로 구성된 필지에
하나 이상의 도로 frontage가 있고 그중 하나가 external road gate에 도달할 때 얻는다. 여러
frontage는 최소 정수 네트워크 거리, 동일 거리에서는 최소 gate stable ID를 선택한다. 거리 band는
진단·P3 입지 factor이며 도달 가능한 먼 필지의 자격을 P2에서 박탈하지 않는다. Application의
`ParcelReadModel`이 이 값과 변경 집합을 복사해 Presentation에 제공한다.

P3-01의 `BuildingDefinition` ScriptableObject는 stable definition ID, 저밀도 R/C/I 용도,
`1x1~2x2` footprint, 주거 점유 수용량 또는 일자리 수용량, 문자열 presentation key만 가진다.
현재 점유율, 채워진 일자리, 전력 공급, 건설 진행도와 prefab/GameObject reference는 definition
asset에 저장하지 않는다. `OpenSimCity.Definitions`가 이 asset을 Unity object가 없는
`BuildingDefinitionData`로 변환하고, `OpenSimCity.Application`의 `BuildingDefinitionCatalog`가
stable ID 순서의 조회와 중복 검증을 제공한다. Simulation과 Application은 Definitions assembly를
역참조하지 않으므로 Simulation이 ScriptableObject를 소유하지 않는다.

`BuildingState`는 stable building ID, definition ID, parcel ID, row-major 기준 anchor, 현재
점유 수와 채워진 일자리만 저장한다. `BuildingStateCollection`의 생성·조회·제거와 열거 순서는
building stable ID 오름차순이며, `BuildingStateDto`/`BuildingStateCollectionDto`는 primitive
field만 사용한다. Application 참조 validator는 각 state의 definition ID와 parcel ID가 알려진
catalog·parcel 집합에서 해석되는지 결정적 building ID 순서로 검사한다. 건물 성장, 점유율 계산,
전력 상태와 prefab 생성은 이 계약에 포함하지 않는다.

P3-02의 `PowerNetwork`는 P2 도로 component를 재구축 가능한 전력 전달 cache로 사용한다.
발전소와 건물 footprint가 인접한 도로 중 row-major 최소 셀 하나를 선택하고, component별 생산량을
건물 stable ID 순서로 전량 배분한다. `BuildingDefinition`의 수요와 `UtilityDefinition`의 최대 용량은
정적이며, `PowerProducerState`의 현재 생산량만 runtime state다. component label과 공급 결과는 DTO에
저장하지 않고 road/building/producer version에서 다시 만든다. Application의 `PowerReadModel`과
`DevelopmentPowerEligibilityQuery`가 Presentation overlay 및 후속 성장 시스템의 유일한 읽기 경계다.
CityPrototype의 발전소 primitive는 중앙 `PowerProducerViewRenderer`가 pool로 동기화하며 view 수와
material은 Simulation 결과에 영향을 주지 않는다.

## 9. 성능 예산

프로토타입 목표:

- 64x64 셀, 500개 이하 건물, 표시 차량 100대 이하
- 1080p 기준 목표 60 FPS
- 일반 시뮬레이션 틱 8 ms 이하, 월간 틱 30 ms 이하
- 정상 플레이 중 프레임당 관리 힙 할당 0 B 목표
- 저장/불러오기 왕복 후 의미 있는 상태 차이 0건

최적화는 프로파일 결과를 근거로 한다. 초기에는 일반 C# 배열과 중앙 시스템을 사용하고, 병목이 확인된 순수 계산만 Jobs/Burst로 이동한다. ECS 전체 전환은 기본 계획에 포함하지 않는다.

## 10. 저장 규칙

- 저장 데이터에 Scene object reference, instance ID, Transform을 넣지 않는다.
- 모든 개체는 저장 가능한 안정 ID를 사용한다.
- `SaveHeader`에 포맷 버전, 게임 버전, 타임스탬프, 맵 시드를 둔다.
- 시스템별 DTO를 분리하고 마이그레이션 함수를 둔다.
- 결정적 RNG 상태, 현재 틱, 명령 sequence, 시스템별 다음 실행 시점을 저장한다.
- 자동 저장은 임시 파일에 쓴 뒤 성공 시 교체한다.
- 런타임 상태와 화면용 차량/파티클/뷰 풀은 분리하여 후자는 저장하지 않는다.

## 11. 첫 번째 기술 결정

확정:

- 통계 기반 시뮬레이션 + 샘플링된 가시 에이전트
- Simulation/Presentation 분리
- 64x64 평지, 저밀도, 격자 도로부터 시작
- 외부 도로 연결을 이주·노동·시장 흐름의 최소 게이트웨이로 사용
- 시 재정과 집계 민간경제를 별도 상태로 관리
- 중앙 스케줄러와 시스템별 틱 주기
- Unity Editor 스크립트로 Scene/Prefab 생성

나중에 프로토타입 측정 후 결정:

- 직교 카메라와 약한 원근 카메라 중 최종 선택
- JSON 이후 최종 저장 포맷
- Jobs/Burst 적용 범위
- Addressables 도입 시점
- 자유 곡선 도로와 지형 높낮이 지원 여부

## 12. Capability gate와 experience gate

각 Phase는 상태·결정성·성능을 검증하는 capability gate와, 플레이어가 공개 UI에서 문제를 이해하고 정상 command로 결과를 바꾸는 experience gate를 함께 가진다. UI는 Simulation/Application query의 reason과 factor를 표시하며 자체 규칙을 만들지 않는다.

단계별 최소 경험과 증거 형식은 [플레이어 경험 게이트 계약](Specs/PLAYER_EXPERIENCE_CONTRACTS.md)을 따른다. Experience gate는 후속 시스템을 앞당겨 임시 구현하는 근거가 아니며, 해당 Phase에 존재하는 기능으로만 시나리오를 구성한다.
