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

### Simulation

그리드, 건물, 인구, RCI, 경제, 전력, 교통 등 도시의 실제 상태와 규칙을 가진다. Scene, Prefab, Material, UI 타입을 참조하지 않는다.

### Application

플레이어 명령을 검증하고 시뮬레이션 시스템의 실행 순서를 조정한다. 저장/불러오기, 새 도시 생성, 게임 속도도 여기서 조율한다.

### Gameplay

셀 선택, 도로 드래그, 구역 칠하기, 철거처럼 입력을 도시 명령으로 변환한다. 상태를 직접 수정하지 않는다.

### Presentation

시뮬레이션의 읽기 모델과 이벤트를 받아 지형, 도로, 건물, 차량, 데이터맵을 표현한다. 보이는 차량 수와 실제 통행량은 독립적이다.

### UI

예산, RCI, 툴바, 선택 정보, 경고를 표시한다. 시뮬레이션 내부 배열을 직접 읽지 않고 공개된 조회 API나 읽기 모델을 사용한다.

### Editor / Tests

Scene·Prefab·정의 데이터 생성, 검증, 벤치마크, EditMode/PlayMode 테스트를 둔다. Scene YAML을 자동화 스크립트 없이 대규모로 직접 편집하지 않는다.

## 3. 어셈블리 경계

초기부터 asmdef를 나누되 너무 잘게 쪼개지는 않는다.

| Assembly | 책임 | 참조 가능 대상 |
|---|---|---|
| `OpenSimCity.Core` | 공통 값 타입, ID, 시간, 유틸리티 | 없음 |
| `OpenSimCity.Simulation` | 도시 상태와 순수 규칙 | Core |
| `OpenSimCity.Application` | 명령, 틱 조율, 저장 포트 | Core, Simulation |
| `OpenSimCity.Gameplay` | 입력과 건설 도구 | Core, Application |
| `OpenSimCity.Presentation` | 월드 렌더링과 뷰 동기화 | Core, Application |
| `OpenSimCity.UI` | 화면 UI | Core, Application |
| `OpenSimCity.Editor` | 생성·검증 도구 | 모든 Runtime assembly, Editor 전용 |
| `OpenSimCity.Tests.EditMode` | 순수 로직 테스트 | Core, Simulation, Application |
| `OpenSimCity.Tests.PlayMode` | Scene/UI/입력 통합 테스트 | Runtime assemblies |

금지되는 참조 방향:

- Simulation -> Presentation/UI/Gameplay/Editor
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
│  │  │  │  ├─ Environment/
│  │  │  │  └─ UI/
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
│  ├─ Export/
│  ├─ Scripts/
│  └─ References/
├─ Docs/
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
- 실제 Unity 임포트 파일은 승인된 결과만 `Assets/OpenSimCity/Content/Art`로 이동한다.
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
- `RoadNetworkState`: 노드, 간선, 용량, 교통량, 버전
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

일시정지, 배속, 저장 요청은 도시 상태를 변경하는 명령과 분리된 Application control이다. 배속이 달라도 같은 수의 시뮬레이션 틱과 같은 도시 명령을 적용했다면 도시 결과는 같아야 한다.

모든 도시 명령에는 적용할 `SimulationTick`과 해당 틱 안에서 단조 증가하는 `CommandSequence`를 붙인다. 시뮬레이션은 이 두 값을 기준으로 명령을 정렬한다.

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
