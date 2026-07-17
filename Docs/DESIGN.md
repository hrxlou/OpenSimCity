# OpenSimCity 디자인·아트 기준

이 문서는 OpenSimCity의 월드 비주얼, UI, 외부 에셋 활용과 검증 기준을 정의하는 저장소의 기준 문서다. 외부에서 작성한 기획안과 AI 시안은 아이디어 입력으로 사용하되, 구현 범위와 충돌할 때는 이 문서와 [실행 로드맵](Roadmap/README.md)을 우선한다.

## 1. 디자인 목표

OpenSimCity는 도시가 주인공인 2.5D 시뮬레이션이다. 화면은 정보를 많이 보여주는 것보다 플레이어가 현재 상태, 원인, 가능한 다음 행동을 빠르게 이해하도록 돕는 데 우선순위를 둔다.

- 로우폴리·세미 스타일라이즈드 3D 월드와 제한형 아이소메트릭 카메라를 기본으로 한다.
- 실제 한국 도시에서 익숙한 밀도, 옥상 설비, 간판, 주차, 골목과 가로 시설의 인상을 사용하되 특정 도시·건물·상표를 그대로 복제하지 않는다.
- 사진처럼 사실적인 배경이나 고밀도 메가시티 표현을 렌더링 목표로 삼지 않는다.
- UI는 월드를 가리지 않는 운영 도구이며, 장식용 대시보드가 아니다.
- 시뮬레이션에 없는 상태를 시각적으로 암시하지 않는다. 쇠퇴, 교통, 서비스, 정책 같은 표현은 해당 시스템이 구현된 Phase부터 사용한다.
- 같은 시뮬레이션 상태는 플랫폼과 저장 왕복 뒤에도 같은 안정 ID를 통해 같은 의미의 표현을 선택한다.

## 2. 시각 언어

### 2.1 월드

- 형태: 멀리서도 용도와 성장 단계를 구분할 수 있는 단순하고 굵은 실루엣
- 표면: 과도한 미세 디테일보다 제한된 재질군, 명확한 명도 차, 반복 가능한 모듈
- 색: UI보다 채도를 낮추되 R/C/I, 도로 계층, 공공시설이 형태와 색의 조합으로 구분되게 한다.
- 카메라 거리: 개별 간판보다 블록 구성과 도로망이 먼저 읽혀야 한다.
- 다양성: 고유 모델 수를 무작정 늘리지 않고 외벽, 옥상, 간판, 설비, palette 모듈의 조합으로 만든다.
- 의미 보존: 외형 variant는 기본적으로 장식이다. 수용량, 세수, 성장 자격 등 gameplay 의미는 definition과 simulation state가 결정한다.

### 2.2 한국 도시화 방향

첫 세트는 특정 실재 지역의 복제가 아닌, 범용적인 한국형 도심 인상을 목표로 한다.

- 세로·가로형 한글 간판과 간판 프레임
- 실외기, 물탱크, 환기구, 난간, 옥상 출입구 같은 옥상·외벽 설비
- 1층 상가 입면, 차양, 유리문, 셔터와 혼합 용도처럼 보이는 facade 조합
- 좁은 주차 면, 볼라드, 화단, 분전함, 쓰레기 수거 지점 등 가로 소품
- 저층 주거의 발코니·방범창·외부 계단과 중저층 상업 건물의 간판 밀도 차

한글 텍스트는 실제 상표·전화번호·기관명을 복제하지 않고 가상의 짧은 문구를 사용한다. 작은 화면에서 읽히지 않는 텍스트는 장식 패턴으로 취급하고, gameplay 정보를 전달하는 텍스트와 구분한다.

## 3. UI 시스템

### 3.1 원칙

- 월드 화면을 기본 상태로 유지하고 필요한 도구와 정보만 단계적으로 연다.
- 같은 정보는 가능한 한 같은 위치와 component로 표시한다.
- 색만으로 성공·경고·실패를 구분하지 않는다. icon, pattern, 문구 중 하나 이상의 보조 채널을 함께 사용한다.
- UI는 Application query/ViewModel을 표시하고 simulation 배열이나 renderer에서 값을 재계산하지 않는다.
- 시안에 있더라도 현재 Phase에 없는 기능은 숨긴다. 비활성 메뉴로 미래 기능을 약속하지 않는다.
- 반투명 패널은 실제 월드 위에서 대비를 검증한다. 장식 blur나 투명도가 정보 판독을 해치면 불투명도를 높인다.

runtime 화면 UI는 P2-00부터 Unity UI Toolkit으로 통일한다. 공통 PanelSettings, USS token과 focus/pointer boundary를 먼저 만들고 P2 배치 UX와 P3 HUD가 재사용한다. grid, road, 대량 datamap geometry처럼 세계 공간 렌더링이 적합한 요소는 Presentation renderer가 담당하며 UI Toolkit VisualElement를 셀마다 만들지 않는다.

### 3.2 기본 색 토큰

다음 값은 구현 시작점이다. 텍스트와 상태색의 명암비는 불투명한 `panel` 배경을 기준으로 계산했으며, 실제 렌더와 접근성 smoke 결과에 따라 토큰 단위로 조정한다.

| Token | 값 | 용도 | `panel` 대비 |
|---|---:|---|---:|
| `canvas` | `#08131D` | 메뉴·빈 화면의 가장 어두운 바탕 | - |
| `panel` | `#0D1D2A` | 기본 HUD·도구 패널 | - |
| `elevated` | `#132838` | 선택·popover·상위 panel | - |
| `border` | `#244253` | divider와 비활성 윤곽 | - |
| `text-primary` | `#F2F5F3` | 제목·주요 값 | `15.60:1` |
| `text-secondary` | `#AAB8C2` | 설명·보조 값 | `8.44:1` |
| `accent` | `#31C7D5` | 선택·활성 도구·focus | `8.37:1` |
| `info` | `#66B3FF` | 정보·외부 연결 | `7.71:1` |
| `success` | `#83CD52` | 유효·공급·개선 | `8.82:1` |
| `warning` | `#F2C14E` | 주의·중간 상태 | `10.20:1` |
| `danger` | `#F07171` | 무효·단절·악화 | `5.96:1` |
| `special` | `#B095F5` | 정책·특성화 등 제한된 강조 | `6.90:1` |

overlay 색은 월드 material과 별도 토큰으로 관리한다. 유효/무효 preview, 접근성 band, zone은 색 외에도 윤곽, hatch, icon 또는 label로 구분한다.

### 3.3 화면 슬롯

게임 화면은 다음 슬롯을 안정적으로 유지한다. 모든 슬롯을 항상 채우지는 않는다.

| 위치 | 역할 | 최초 적용 |
|---|---|---|
| 상단 Global HUD | 날짜, 속도, 잔액, 인구와 현재 구현된 핵심 상태 | P3 |
| 좌측 Tool Rail | 도로·구역·철거·overlay 같은 상위 도구 전환 | P2 |
| 하단 Build Palette | 선택 도구의 subtype, 비용, 단축키 | P2 |
| 우측 Context Inspector | 선택 대상, 실패 사유, 상세 상태와 가능한 행동 | P2 |
| 우하단 Simulation Controls | pause와 배속 | P3 |
| 중앙 World View | 선택, preview, 배치, datamap의 주 작업 공간 | P1 |

화면이 좁을 때는 Inspector와 Build Palette를 접거나 교대로 표시한다. UI scale과 창 크기를 바꿔도 World View의 선택 지점과 입력 결과가 바뀌지 않아야 한다.

### 3.4 공통 component

- `GlobalHud`: 날짜·속도·재정·인구 등 현재 Phase의 전역 상태
- `ToolRail`: 상위 건설/조회 도구와 선택 상태
- `BuildPalette`: 배치 가능한 항목, 비용, 유효성
- `ContextInspector`: 선택 대상과 원인·행동
- `DataOverlayLegend`: overlay 범례, 기간, 값 범위와 대체 pattern
- `ProblemIndicator`: 원인 코드, 심각도, 관련 위치로 이동하는 진입점
- `SimulationControls`: pause와 허용된 배속
- `Toast/CommandFeedback`: 명령 성공·실패와 비용 변화

component의 상태는 최소 `default`, `hover`, `focus`, `selected`, `disabled`, `warning`, `error`를 정의한다. motion은 상태 전환을 설명하는 짧은 feedback에만 사용하며 simulation tick이나 명령 완료 시점과 결합하지 않는다.

### 3.5 타이포그래피·아이콘

한글과 숫자가 함께 읽히는 글꼴을 사용한다. P3-06에서 실제 font asset을 가져오기 전에 다음을 확정한다.

- 한글/라틴/숫자 glyph 범위와 fallback
- 배포·임베딩·수정 허용 범위가 명확한 라이선스
- 100%, 125%, 150% UI scale에서 본문·caption의 최소 크기
- 숫자 표의 tabular figure 지원 여부 또는 정렬 방식

아이콘도 같은 시점에 라이선스와 source를 기록한다. 아이콘만으로 기능을 설명하지 않고 tooltip 또는 label을 제공한다.

## 4. 제공된 시안의 사용 범위

제공된 네 장의 AI 시안은 레이아웃과 분위기 참고 자료이며, 게임 데이터·실제 렌더 품질·출시 기능 목록의 증거가 아니다.

| 시안 | 채택할 점 | 그대로 채택하지 않는 점 |
|---|---|---|
| 건설 화면 (`00.34.56`) | 좌측 Tool Rail, 하단 Build Palette, 우측 Context Inspector | 사실적 야경, 미구현 교통·수도·소음·upgrade 정보 |
| 도시 wellbeing overlay (`00.34.54`) | overlay 범례, 월드 marker와 상세 panel의 연결 | 동시에 너무 많은 panel, P7 이전 서비스·행복도 지표 |
| 예산 화면 (`00.34.51`) | 별도 관리 화면의 정보 계층과 요약→상세 흐름 | 일반 플레이 HUD를 업무용 dashboard처럼 채우는 구성, 미구현 정책·서비스 |
| 메인 메뉴 (`00.34.47`) | Continue/New/Load/Settings의 기본 계층 | News, Community, Mods, Scenarios, 온라인 계정과 실제 사진 배경 |

시안 속 로고, 사진, 수치, 문구와 아이콘은 production asset으로 사용하지 않는다. 메인 메뉴의 브랜드·로고는 별도 승인 전까지 임시 표기로 둔다.

## 5. Phase별 적용 범위

| Phase | 허용되는 시각 작업 | 아직 하지 않는 작업 |
|---|---|---|
| P0 | UI/월드 bootstrap과 테스트용 기본 스타일 | 제품 UI·production art |
| P1 | grid, hover, selection, 카메라 debug 표현 | 건설 palette·상세 HUD |
| P2 | UI Toolkit/token/focus 기반, Tool Rail, Build Palette, Context Inspector, 배치 preview와 도로/구역/필지 overlay | 성장·재정·교통·서비스 dashboard |
| P3 | 최소 Global HUD, Simulation Controls, 원인/회복 feedback, CityPrototype primitive placeholder | MegaKit의 제품 화면 교체, 출시 수준 polish |
| P3–P7 준비 | Benchmark 전용 representative production-like asset slice | production catalog 수량 확장·한국 도시화 완료 |
| P4–P7 | 저장 UI와 실제로 구현된 교통·쇠퇴·서비스·정책 화면을 기존 component에 추가 | 구현 전 지표의 선행 노출 |
| P8 | 실제 fixture 기반 UI/render 성능 측정과 필요한 LOD·pool·batching | 측정 없는 전면 최적화 |
| P9 | 검증된 production art, 한국 도시화 세트, 튜토리얼·접근성·메뉴·오디오·낮밤 presentation polish | 온라인·뉴스·커뮤니티·모드 기능의 암시 |

아트 inventory, 라이선스 확인, importer 실험 같은 읽기 전용 준비는 이전 Phase에도 할 수 있다. 다만 해당 Phase의 제품 화면과 prefab을 production 에셋으로 교체했다는 뜻은 아니다.

P3-02에서는 Tool Rail에 발전소 설치·철거와 전력 overlay만 추가한다. 발전소는 CityPrototype의
pooled primitive placeholder로 표시하고, 연결 발전소/미연결 발전소/공급 건물/미공급 건물은
색과 함께 solid/hatch/cross-hatch 및 `+`, `!`, `X` marker로 구분한다. 수도, 예산별 효율,
송전선 상세, 후속 수요·경제 지표는 노출하지 않는다.

## 6. Downtown City MegaKit Standard 검증 기준선

검토한 무료 파일은 `Downtown City MegaKit[Standard].zip`이며 SHA-256은 `499dc648a2451686baa11d4519192f07339fdf66c8bd23739c931ca4667ef1f2`다. ZIP 내부 라이선스는 CC0 1.0으로 확인했다. 실제 import 전에 acquisition manifest에 원본 URL, 취득일, 배포 tier, hash와 라이선스 파일을 함께 기록한다.

### 6.1 확인된 구성

- 고유 모델 153개와 완성 건물 3개, 나머지는 facade·cornice·roof·street·prop 등 조립 모듈
- Unity용 FBX 153개, Unreal용 FBX 153개, Godot용 glTF 153개
- 공유 2K texture가 중심이며 root texture 약 100.3 MiB, Unity FBX 디렉터리 약 5.9 MiB
- `.blend`, 완성 Unity project, custom shader, collision, LOD는 Standard ZIP에 포함되지 않음
- `__MACOSX` 항목 698개는 import와 version control에서 제외
- FBX 안의 제작자 로컬 절대 texture path 때문에 Blender에서 texture 재연결이 필요할 수 있음

[공식 배포 페이지](https://quaternius.com/packs/downtowncitymegakit.html)의 전체 팩 수치와 무료 ZIP의 실제 모델 수를 혼동하지 않는다. Standard는 prototype과 모듈 source로 평가하며, 완성된 한국형 production 세트로 간주하지 않는다.

### 6.2 Blender 표본 관찰

| 모델 | 대략적 크기(m) | 삼각형 | material slot |
|---|---:|---:|---:|
| `Building_Small_1` | `12.46 × 14.54 × 17.03` | `18,344` | `13` |
| `Building_Medium_2_001` | `15.06 × 13.06 × 25.01` | `25,612` | `12` |
| `Building_Large_2` | `23.97 × 19.97 × 28.00` | `45,122` | `13` |
| `Brick_Plain_3` | `2.00 × 0.20 × 3.00` | `6` | `2` |
| `Street_2Lane` | `6.00 × 12.00 × 0.15` | `220` | `3` |
| `Prop_ACUnit` | `0.89 × 0.35 × 0.60` | `136` | `1` |

표본은 Blender 5.1.2에서 scale `0.01`, X rotation `90°`가 적용되지 않은 transform으로 들어왔다. 이는 성능 budget이 아니라 source 관찰값이다. 8m grid 기준 완성 건물은 정규화 후 대략 2×2, 2×2, 3×3 footprint 후보지만, 실제 footprint는 validator와 배치 smoke로 확정한다.

## 7. 에셋 파이프라인

### 7.1 기준 경로

```text
Blender/
├─ Source/
│  ├─ External/Quaternius/DowntownCityMegaKit/Standard/
│  └─ OpenSimCity/KoreanModules/
├─ Scripts/
│  ├─ Importers/
│  ├─ Generators/
│  ├─ Validators/
│  ├─ Exporters/
│  └─ Previews/
├─ Export/
└─ References/

Artifacts/Art/<batch>/                 # 임시 생성물·보고서, 커밋하지 않음
Assets/ThirdParty/Quaternius/          # vendor 원본·호환성 정규화 mesh/texture
Assets/OpenSimCity/Content/Art/        # 자체 제작 geometry·texture
Assets/OpenSimCity/Content/{Materials,Prefabs,Definitions}/ # 프로젝트 wrapper
```

원본 ZIP 전체를 Unity `Assets`에 넣지 않는다. 필요한 source FBX와 texture만 선별하고, 취득 manifest와 라이선스를 보존한다. vendor 원본과 축·scale·pivot·texture path 등 호환성을 위해 정규화한 mesh/texture는 계속 `Assets/ThirdParty/Quaternius`에 둔다. OpenSimCity 전용 prefab, material, definition과 metadata wrapper는 `Assets/OpenSimCity`에 둔다. 자체 제작하거나 실질적으로 재설계한 geometry/texture만 provenance를 남기고 `Assets/OpenSimCity/Content/Art`에 둔다.

### 7.2 수집·정규화·승인

1. ZIP 안전성, hash, 라이선스, 파일 inventory를 기록한다.
2. `__MACOSX`와 다른 engine용 중복 파일을 제외하고 필요한 source만 선별한다.
3. texture를 pack의 `Textures`에서 basename으로 재연결하고 누락을 보고한다.
4. 1 unit=1m, Unity 축, scale `(1,1,1)`, 기본 bottom-center pivot으로 정규화한다.
5. 8m cell footprint와 도로 접면 anchor를 metadata로 검증한다.
6. material slot과 texture 사용을 inventory하고, 실제 benchmark에 필요한 범위에서만 통합한다.
7. 승인된 export에 provenance sidecar와 validator report를 붙인다.
8. Unity import preset, definition/prefab 연결과 Windows/macOS render smoke를 통과시킨다.

source 파일명은 사람이 읽는 이름일 뿐 runtime ID가 아니다. `asset_id`, `definition_id`, prefab key는 파일 이동·이름 변경과 무관한 안정 ID로 관리한다.

모든 건물에 collider나 LOD를 일괄 생성하지 않는다. grid query가 selection의 기준이며, 물리 상호작용이 필요한 view에만 단순 collider를 둔다. LOD 단계, material 수와 triangle 기준은 P8 benchmark 결과를 바탕으로 P9 validator 규칙으로 확정한다.

### 7.3 한국 도시화 작업 순서

- `K0 Inventory`: 라이선스·hash·모델·texture·크기·material 현황 기록
- `K1 Pipeline Proof`: 건물 1개, facade 모듈 1개, 도로/소품 1개로 import→정규화→export→Unity smoke 검증
- `K2 Vertical Slice`: 저층 R/C/I와 공공시설 각 최소 표본에 간판·옥상·입면 모듈 적용
- `K3 Set Expansion`: gameplay definition과 필요한 성장 단계에 맞춰 조합 확대
- `K4 Production Polish`: P8 budget 기반 LOD/material 최적화, 양 플랫폼 QA와 provenance 마감

K0와 K1 및 대표 성능 fixture는 P3-08에서 준비할 수 있다. 이 fixture는 Benchmark 전용이며 CityPrototype의 placeholder를 교체하지 않는다. K2 이후의 제품 통합은 P9-01에서 gameplay 계약과 benchmark가 안정된 뒤 진행한다. Source 유료 tier는 Standard로 pipeline proof나 필요한 variant를 만들 수 없다는 증거가 있을 때만 검토하며, 구매 전에 포함된 Unity project와 Unity 6/URP 호환성을 별도로 확인한다.

## 8. 검증과 증거

시각 변경은 코드가 compile되는 것만으로 완료하지 않는다.

### 8.1 작업과 확인 환경

| 작업 | 기본 환경 | 확인 방식 |
|---|---|---|
| 모델·모듈 제작과 정규화 | Blender | source/validator 확인 뒤 명시적 FBX 또는 glTF export |
| asset import·material·prefab·Scene·UI 조립 | Unity Editor | Unity Hub에 등록된 저장소 프로젝트를 열어 import/Console/Scene 확인 |
| 좌표·simulation·serialization 규칙 | Unity EditMode | 좁은 관련 테스트부터 실행 |
| 카메라·선택·배치·View·UI 상호작용 | Unity Play Mode | CityPrototype fixture와 PlayMode/smoke 테스트 |
| 플랫폼 차이·배포 설정·출시 후보 | Windows/macOS Player build | Phase gate의 build smoke, P9 release build와 장시간 검증 |

일상적인 시각·UI 확인은 대부분 Unity Editor의 Scene/Game View와 Play Mode에서 한다. 매 변경마다 standalone build를 만들 필요는 없지만, Editor에서 보인다는 사실만으로 플랫폼·배포 검증을 대체하지 않는다.

- UI: 대표 aspect ratio, 100/125/150% scale, keyboard/mouse/trackpad, focus와 resize PlayMode smoke
- 대비: 실제 낮/밤 월드 위 screenshot에서 텍스트, 상태색과 overlay 확인
- 의미: UI 문구·icon·overlay가 같은 기간의 query/ViewModel과 일치하는지 fixture로 검증
- 월드: 전체 도시, block, 건물 근접 거리의 비교 capture
- 에셋: hash/license/provenance, texture 누락, scale/pivot/footprint, material inventory validator
- 성능: 같은 benchmark fixture의 CPU/GPU/GC/draw call 전후 수치
- 결정성: presentation과 variant를 끄거나 교체해도 simulation checksum이 같은지 확인
- 플랫폼: Windows와 macOS에서 import, build, material/shader, 대소문자 경로와 LFS pointer smoke

AI 시안이나 Blender viewport만으로 완료를 판정하지 않는다. UI와 production art의 최종 증거는 실제 Unity game render와 재현 가능한 fixture에서 남긴다.

## 9. 확정 전 결정 항목

- P3-06 이전: 한글 font, icon source와 라이선스, 최소 type scale
- P9-01 이전: Standard만 사용할지 Source tier가 필요한지, 파생 에셋 배포 방식
- P8-02 이후: 모델별 triangle/material/LOD 기준과 target quality tier
- P9-02 이전: 제품 로고, 메인 메뉴 배경, 지원 언어와 현지화 범위
- 출시 후보 이전: 각 vendor·first-party asset의 provenance와 attribution 요구 최종 감사

이 항목이 확정되기 전에는 임시 값을 사용할 수 있지만, 임시 선택을 production 기준으로 문서화하거나 미구현 기능을 화면에 노출하지 않는다.
