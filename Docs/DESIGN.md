# OpenSimCity 디자인·아트 기준

이 문서는 OpenSimCity의 월드 비주얼, UI, 외부 에셋 활용과 검증 기준을 정의하는 저장소의 기준 문서다. 외부에서 작성한 기획안과 AI 시안은 아이디어 입력으로 사용하되, 구현 범위와 충돌할 때는 이 문서와 [실행 로드맵](Roadmap/README.md)을 우선한다.

## 2026-09-09 사용자 확정 수정

이 절은 아래 초기 수치·스타일과 충돌할 때 우선한다.

- 모든 UI 모서리 곡률은 `0`이다. 각진 계기판 형태를 유지한다.
- 옆면 색 강조선은 알림·온보딩처럼 의미가 있는 영역에만 사용한다. 일반 HUD·메뉴의 장식용 사이드바는 사용하지 않는다.
- 남색 단색 창 대신 중성 흑연색 무광 표면, 미세한 결, 밝은 상단 1px·어두운 하단 1px 경계로 재질감을 만든다. 색상·질감이 글자를 압도하면 안 된다.
- Pretendard v1.3.9 정적 Regular/SemiBold를 명시적인 TextCore SDF 에셋으로 사용한다. 임의 시스템 fallback·가변 폰트의 합성 bold에 의존하지 않는다. 64px sampling, 6px padding, 2048px dynamic atlas에 실제 UI 한글을 예열한다.
- HUD는 최대 72px의 항목명/수치 계층으로 분리하며, 운영 지표와 개발 진단이 독립 표면을 갖는다. 숨긴 필드는 caption을 포함한 group 전체를 숨긴다.
- 건설 도구에는 icon과 짧은 한글 이름을 함께 표시한다. 문구가 생략된 전체 이름과 단축키는 tooltip에 유지한다.
- 캡처는 native resolution으로 저장해 실제 글자 렌더링을 확인한다. 고배율 확대 캡처를 폰트 품질 증거로 사용하지 않는다.

UI Toolkit 기준 해상도는 최소 지원 화면인 `1280×720`이다. 1920×1080 기준에서 작은 창의 15px 본문이 10px로 줄어들던 문제를 방지한다. 100/125/150% 배율과 작은 창의 HUD·도구 패널 경계 검사를 함께 유지한다.

### 광원·표면의 실행 계약

- 카메라 offset 100을 고려해 URP shadow distance는 180, cascade split은 0.35/0.52/0.72를 사용한다. 과거 100-unit cutoff가 도시 중심 그림자를 지웠던 문제를 방지한다. 성능 개선 주장이 아니며 GPU 비용은 별도 측정한다.
- CityPrototype을 **활성 장면으로 설정한 뒤** RenderSettings를 기록하고 원래 활성 장면을 복원한다. 카메라 후처리·ACES/약한 bloom/white balance는 first-party `UrbanDaylightProfile`에서 재현한다.
- 새 도시의 일주기 표현은 정오에서 시작한다. +500 permille은 query projection에만 적용하며 simulation tick/날짜/checksum은 변경하지 않는다. 장면 전환마다 낮밤 controller를 설치하고 Trilight sky/equator/ground와 reflection intensity를 함께 갱신·복원한다.
- Poly Haven `green_point_park` CC0 HDRI는 사전 필터링한 reflection cubemap으로만 사용한다. 다른 도시 사진을 월드 배경처럼 표시하지 않는다.
- Blender Generated 좌표는 FBX UV로 전달되지 않는다. UV가 없는 Seoul 메시만 import 시 4m box projection과 tangent를 생성하고 기존 UV는 보존한다.
- 거칠기 PNG는 선형 값으로 읽어 URP metallic(R=0)/smoothness(A=1-roughness)로 변환한다. alpha를 투명도나 sRGB로 처리하지 않는다.
- 여의도 v08 Photoreal Integrated v2의 수목 2종·벤치·휴지통을 원본 저장 없이 추출한다. 수목은 export 복사본만 감량하고 출처/정점 수를 report로 남긴다. 가로등·신호등·벤치는 도로 방향을 따르며 나무만 장식 회전을 허용한다.

## 1. 디자인 목표

2026-09-05 사용자 수정: 최종 목표는 실사에 가까운 도시 시뮬레이션이다. 단색 primitive와 과장된 비율은 임시 상태이며 승인된 제품 아트가 아니다. 건물·도로·보도·차량의 실제 축척, PBR 표면, 창호 깊이와 접지 그림자를 검증한다. Blender 검수 렌더와 Unity 플레이 렌더를 별도로 확인한다. 이 방향이 아래 초기 스타일라이즈드 기준보다 우선한다.

제품 건물의 Blender detail LOD는 일반 플레이 줌 전체에서 유지되도록 threshold `0.003`, 마지막 LOD는 줌아웃 때문에 도시가 사라지지 않도록 culling threshold `0`을 사용한다. 옥상 부품은 실제 지붕 면의 위치와 폭에 맞춰 배치한다.

도시 렌더 기준은 `UrbanRenderQualitySetup`에서 HDR, 4x MSAA, soft shadow, 4096 shadow atlas와 4 cascade로 재현한다. 성능 개선을 뜻하지 않으며 실제 GPU 비용은 별도 측정한다. `ProductArtPreviewCapture -osc-street`는 건물군을 마주 보는 두 줄과 도로·보도로 구성해 전체 거리 관계를 검수한다. 이 에디터 검수 장면은 실제 gameplay smoke를 대체하지 않는다.

도로 축척은 건설 등급으로 읽혀야 한다. 일반 도로는 끊기지 않는 왕복 2차선 중앙선을,
대로는 사용자 Blender 라이브러리의 왕복 4차선 모듈을, 광로는 왕복 6차선 모듈을 쓴다.
횡단보도는 연결된 교차로 진입로에만 나타나며 정지선과 줄무늬가 차도 폭 전체를 덮는다.
비주얼 검수 fixture의 수변은 평면 단색이 아니라 반복 ripple normal, 양안 산책로,
교대와 바닥까지 이어진 사용자 제작 Seoul 교량 메시를 함께 검증한다.

Pretendard HUD 텍스트는 폰트 metric 중심이 아니라 실제 cap-height가 보이는 시각 중심으로
정렬한다. 현재 목표 카드는 하단 월드 위에 떠 있는 모달처럼 두지 않고 상단 HUD 바로 아래
좌측에 고정해, 임시 안내 정보라는 위계를 분명하게 한다.

OpenSimCity는 도시가 주인공인 2.5D 시뮬레이션이다. 화면은 정보를 많이 보여주는 것보다 플레이어가 현재 상태, 원인, 가능한 다음 행동을 빠르게 이해하도록 돕는 데 우선순위를 둔다.

- 실재 도시의 축척과 재료 반응을 우선하는 사실적 3D 월드와 제한형 아이소메트릭 카메라를 기본으로 한다.
- 실제 한국 도시에서 익숙한 밀도, 옥상 설비, 간판, 주차, 골목과 가로 시설의 인상을 사용하되 특정 도시·건물·상표를 그대로 복제하지 않는다.
- 사진 배경으로 품질을 속이지 않고 gameplay 월드 자체가 도로변·필지·스카이라인 배경 디테일을 만든다.
- UI는 월드를 가리지 않는 운영 도구이며, 장식용 대시보드가 아니다.
- 시뮬레이션에 없는 상태를 시각적으로 암시하지 않는다. 쇠퇴, 교통, 서비스, 정책 같은 표현은 해당 시스템이 구현된 Phase부터 사용한다.
- 같은 시뮬레이션 상태는 플랫폼과 저장 왕복 뒤에도 같은 안정 ID를 통해 같은 의미의 표현을 선택한다.

## 2. 시각 언어

### 2.1 월드

- 형태: 실제 층고·창호 비례·건축 부재를 유지하면서 멀리서도 용도와 성장 단계를 구분하는 실루엣
- 표면: albedo·normal·roughness·AO를 갖춘 PBR 재질과 실제 크기 기준의 texel density, 반복 가능한 facade 모듈
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

### 2.3 P3 primitive placeholder

P3 제품 화면은 production 아트 방향을 확정하지 않는 primitive placeholder를 사용한다.
저밀도 주거·상업·공업은 각각 A/B/C 세 실루엣으로 구분하고, 발전소·도로·건설 중 상태도 공용
색과 굵은 형태로만 표현한다. prefab root는 바닥 중심 pivot, identity rotation, scale
`(1, 1, 1)`을 사용하며 runtime footprint scale은 중앙 renderer가 적용한다.

- 주거: 녹색 계열과 지붕/저층·중층 높이 차
- 상업: 청색 계열과 차양·간판 mass
- 공업: 황토색 계열과 창고·굴뚝·tank mass
- 발전소: 보라색 공공시설 mass와 stack
- 도로: 어두운 surface와 밝은 중앙 marking
- 건설 중: 노란 foundation/frame

색은 용도 식별의 보조 채널이며 실루엣과 naming을 함께 사용한다. 이 세트는 P3-08
representative performance slice나 P9 production/Korean-city set을 대신하지 않는다.

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
| `canvas` | `#0D1115` | 메뉴·빈 화면의 가장 어두운 바탕 | - |
| `panel` | `rgba(12, 17, 22, 0.88)` | 월드 위 floating HUD·도구 패널 | 월드 render smoke로 검증 |
| `elevated` | `rgba(31, 39, 46, 0.94)` | 선택·popover·chip·상위 panel | - |
| `border` | `#48545E` | divider와 비활성 윤곽 | - |
| `text-primary` | `#F2F4F5` | 제목·주요 값 | panel 기준 AA 이상 |
| `text-secondary` | `#AAB2B9` | 설명·보조 값 | panel 기준 AA 이상 |
| `accent` | `#62A8D1` | 선택·활성 도구·focus | panel 기준 AA 이상 |
| `info` | `#86B9D8` | 정보·외부 연결 | panel 기준 AA 이상 |
| `success` | `#7DBB83` | 유효·공급·개선 | panel 기준 AA 이상 |
| `warning` | `#D7AD63` | 주의·중간 상태 | panel 기준 AA 이상 |
| `danger` | `#D97872` | 무효·단절·악화 | panel 기준 AA 이상 |
| `special` | `#A995C4` | 정책·특성화 등 제한된 강조 | panel 기준 AA 이상 |

overlay 색은 월드 material과 별도 토큰으로 관리한다. 유효/무효 preview, 접근성 band, zone은 색 외에도 윤곽, hatch, icon 또는 label로 구분한다.

### 3.3 화면 슬롯

게임 화면은 다음 슬롯을 안정적으로 유지한다. 모든 슬롯을 항상 채우지는 않는다.

| 위치 | 역할 | 최초 적용 |
|---|---|---|
| 상단 Global HUD | 날짜, 속도, 잔액, 인구와 현재 구현된 핵심 상태 | P3 |
| 하단 Command Dock | 도로·구역·철거 같은 상위 도구 전환, 도시 관리 진입 | P2 |
| Command Dock 위 Build Palette | 선택 도구의 subtype, 비용, 단축키 | P2 |
| 우측 Context Inspector | 선택 대상, 실패 사유, 상세 상태와 가능한 행동 | P2 |
| 우하단 Simulation Controls | pause와 배속 | P3 |
| 중앙 World View | 선택, preview, 배치, datamap의 주 작업 공간 | P1 |

화면이 좁을 때는 Inspector와 Build Palette를 접거나 교대로 표시한다. UI scale과 창 크기를 바꿔도 World View의 선택 지점과 입력 결과가 바뀌지 않아야 한다.

P3의 `GlobalHud`는 World View를 위한 상단의 compact single-row floating status bar다. 재정·인구·RCI는 좌측,
개발 상태·원인·세율·작은 action/feedback은 우측에 둔다. 별도 진단 행이나 세로 action stack으로 월드
작업 공간을 점유하지 않는다. GlobalHud와 하단 Build Palette도 화면 가장자리에서 12px 떨어진 각진 floating
surface로 바꾸며, Build Palette는 선택 도구·단축키·비용을 한 행으로 표시하고 실제 subtype 항목이 추가될
때만 높이가 늘어난다. Tool Rail은 좌측 상시 점유를 끝내고 하단 중앙 `Command Dock`이 된다. 아이콘 도구는
tooltip으로 이름·단축키를 제공하고, overlay는 `데이터` 버튼을 눌렀을 때만 별도 tray로 펼친다. Build Palette와
Context Inspector도 선택·preview가 있을 때만 노출한다. 패널은 0–2px radius, 88% charcoal 표면, 얇은 금속성
grain과 1px 경계를 사용해 웹 카드가 아닌 게임 HUD처럼 읽히되 월드보다 먼저 보이지 않게 한다.
GlobalHud의 현재 날짜는 `년 월 일`을 표시한다. 하루는 별도 simulation cadence가 아니라 현재 월의
저장 가능한 clock tick 진행률을 30일에 결정적으로 매핑한 표시 단위이며, 월간 system 실행과 정산은
기존 월 경계에서만 발생한다.

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

#### P4-03 저장 panel

저장/불러오기는 별도 대시보드나 출시 메뉴를 앞당기지 않고 Simulation Controls의
`저장/불러오기` 진입점과 우하단 elevated panel로 제공한다. panel은 `수동 저장`과 `자동 저장`
두 slot의 기간·format version·저장 시간을 Application ViewModel 그대로 표시한다. 작업 중에는
버튼과 world command 입력을 함께 disable하고 `저장 중`/`불러오는 중` 문구를 표시한다.
손상·unknown version은 danger 색만 사용하지 않고 원인 문구, 기존 도시 유지 설명,
`다시 시도`와 `현재 도시 계속하기` label을 함께 제공한다. UI가 save bytes나 경로를 읽거나
GameObject 복원을 지시하지 않는다.

### 3.5 타이포그래피·아이콘

한글과 숫자가 함께 읽히는 글꼴을 사용한다. P3-06에서 실제 font asset을 가져오기 전에 다음을 확정한다.

- 한글/라틴/숫자 glyph 범위와 fallback
- 배포·임베딩·수정 허용 범위가 명확한 라이선스
- 100%, 125%, 150% UI scale에서 본문·caption의 최소 크기
- 숫자 표의 tabular figure 지원 여부 또는 정렬 방식

아이콘도 같은 시점에 라이선스와 source를 기록한다. 아이콘만으로 기능을 설명하지 않고 tooltip 또는 label을 제공한다.
건설 도구 아이콘은 `UiIconSetup`이 생성하는 first-party 96px mono-line PNG를 사용한다. 모든 아이콘은 같은
stroke와 optical box를 공유하고 USS tint를 통해 hover/selected 상태를 표현하므로 운영체제 글꼴에 의존하지 않는다.

#### P3-06 구현 기록 (2026-07-19, 2026-09-06 갱신)

- 기본 한글/라틴 UI 글꼴은 공식 배포본 `Pretendard 1.3.9` variable TTF이며 SIL OFL 1.1로 포함한다.
  source URL, license, SHA-256은 `Assets/ThirdParty/Fonts/Pretendard`에 함께 고정한다.
  건설 도구 아이콘은 프로젝트가 직접 생성·소유하며 외부 라이선스나 provenance가 필요하지 않다.
- 한글 glyph는 macOS player render로 검증하고 Windows player render는 계속 release gate로 남긴다. 현재 token의
  100% 기준 최소 크기는 caption `13px`, 본문 `15px`, 주요 값 `16px`이고 PanelSettings scale이
  125%/150%를 적용한다.
- HUD의 숫자 값은 별도 value label과 `N0` 형식으로 정렬한다. variable font의 숫자 폭에 의존하지 않고
  value label 정렬을 유지하며, 125%/150% scale과 Windows rasterization은 release gate에서 다시 확인한다.

#### 상용 도시·전략 게임 레퍼런스 원칙 (2026-09-06)

- [SimCity 공식 매뉴얼](https://akamai.cdn.ea.com/eadownloads/u/f/manuals/GAME-SIMCITY/SimCity_2013.pdf)은 도시 성장 도구를 화면 하단 메뉴에 모으고, 시스템 정보와 데이터 맵을 각 범주에서 확장한다. OpenSimCity도 하단 command dock을 기본 진입점으로 사용한다.
- [Cities: Skylines II 공식 UI 업데이트](https://www.paradoxinteractive.com/games/cities-skylines-ii/news/upcoming-visual-updates)는 더 표현적인 아이콘, 현대화된 toolbar, 강조된 demand bar를 목표로 명시한다. 아이콘은 형태를 통일하되 선택·수요·경고의 우선순위를 분리한다.
- [Cities: Skylines II Production Tab 공식 설명](https://www.paradoxinteractive.com/games/cities-skylines-ii/news/work-in-motion)은 자원 목록을 compact하게 만들고 상세 생산 관계를 선택 후 표시한다. 관리 화면은 요약→선택→상세 순서로 열며 월드 화면에 모두 상주시지 않는다.
- [Civilization VII 공식 UI 개선 공지](https://civilization.2k.com/en-GB/civ-vii/news/message-from-firaxis-games/)는 직관적인 상호작용과 map readability를 최우선 개선점으로 든다. UI surface 수와 채도를 줄이고 map/world 판독을 첫 품질 기준으로 둔다.
- 참고는 정보 구조·위계·상호작용 패턴에 한정한다. 각 게임의 상표, 고유 아이콘, 레이아웃 픽셀 값과 아트는 복제하지 않는다.

#### 시각 오버홀 구현 기록 (2026-09-06)

- 초기 디버그 셸의 화면 가장자리 고정 막대와 과장된 청록색·큰 radius를 폐기하고 0–2px radius의
  반투명 textured charcoal surface, 44px hit target, muted border와 Pretendard typography로 통일했다.
- Tool Rail은 9개 first-party mono-line icon을 가진 하단 command dock이며, 이름·단축키는 tooltip으로 제공한다.
  overlay 6종은 `데이터` tray에, feature panel은 `도시 관리` drawer에 단계적으로 숨긴다.
- CityPrototype은 빈 카메라 배경 위 grid만 그리지 않고 Editor setup이 생성하는 matte ground backdrop,
  warm directional light, soft shadow와 거리 fog를 사용한다. 전역 debug grid와 혼잡 overlay는 기본 비활성화한다.
- P3 placeholder의 gameplay key와 footprint는 유지하면서 창호·출입구·차양·옥상 설비·curb·차선 등
  중거리에서 읽히는 geometry layer를 추가했다. 이는 production catalog 완성을 뜻하지 않는다.
- 제품 catalog는 주거 3종의 full-building detail과 상업 3종의 brick facade detail, 공업 3종·발전소의
  mechanical detail을 first-party wrapper로 결합한다. 10개 wrapper 모두 근거리 `Detail`과 원거리
  `Distance` LOD를 가지며 simulation key와 footprint는 바꾸지 않는다.
- 중·원거리 구역 판독성을 위해 상업은 밝은 청록, 공업은 황토, 발전소는 보라를 사용하며 지면과의
  명도 차이를 확보한다. 지붕·콘크리트·유리는 같은 계열 안에서도 서로 다른 명도와 smoothness를 유지한다.
- 도로 view는 application `ConnectionMask`를 사용해 북·동·남·서 연석과 중앙선 조각을 선택적으로 노출한다.
  직선·막다른 길·모서리·T자·십자 교차로가 같은 prefab을 공유하면서도 연결 방향을 가리지 않아야 한다.
- 일반 차량과 버스는 사용자 제작 한국형 taxi/bus mesh를 우선 사용한다. 시각 차량은 중앙 pool에서 48대로 제한하고
  안정 ID로 두 차선에 분산한다. prefab은 `Resources`에서 로드하고 collider와 per-vehicle behaviour를 포함하지 않으며,
  pooled vehicle 하나당 renderer 하나를 유지한다.
- 별도 Blender 작업 공간의 사용자 제작 서울 에셋은 재현 가능한 headless Blender exporter로
  선택 collection만 읽기 전용 추출한다. 저층 혼합상가 6종, 차량·버스, 가로수·가로등, 횡단보도를 bottom-center
  pivot과 단일 renderer FBX로 정규화하고 first-party `Assets/OpenSimCity` 하위에 둔다. 원본 `.blend`는 수정하지 않는다.
- release 시작 화면은 560px hero card, 명확한 primary/secondary action 계층, 반투명 city preview로 구성한다.
- 대중교통·서비스·도시 데이터 패널은 기본 닫힘이며 `도시 관리` 서랍에서 탭 하나만 노출한다.
- 온보딩 카드는 좌하단에서 command dock 위에 배치하여 도시와 건설 도구를 가리지 않는다.

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

2026-07-23에 공식 배포 페이지에서 취득한 무료 파일은 `Downtown City MegaKit[Standard].zip`이며 SHA-256은 `5b1a945576d54cdbb4ccc9c3d52711e6d530da74c74c586407ecc28b165335da`다. 이전 검토본의 SHA-256 `499dc648a2451686baa11d4519192f07339fdf66c8bd23739c931ca4667ef1f2`와 다르므로 서로 대체 가능한 archive로 취급하지 않는다. P3-08 acquisition manifest가 현재 archive와 선별 entry 각각의 hash를 고정한다. ZIP 내부 라이선스는 CC0 1.0으로 확인했다.

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
| `Building_Large_2` | `23.97 × 19.97 × 28.00` | `45,122` | `12` |
| `Brick_Plain_3` | `2.00 × 0.20 × 3.00` | `6` | `2` |
| `Street_2Lane` | `6.00 × 12.00 × 0.15` | `220` | `3` |
| `Prop_ACUnit` | `0.89 × 0.35 × 0.60` | `136` | `1` |

표본은 Blender 5.1.2에서 scale `0.01`, X rotation `90°`가 적용되지 않은 transform으로 들어왔다. 이는 성능 budget이 아니라 source 관찰값이다. 8m grid 기준 완성 건물은 정규화 후 대략 2×2, 2×2, 3×3 footprint 후보지만, 실제 footprint는 validator와 배치 smoke로 확정한다.

### 6.3 P3-08 representative fixture

공용 Blender importer/validator는 위 6개 source와 필요한 texture 17개만 archive에서
선별하고, basename texture relink, 1 unit=1m, Unity Y-up/-Z forward,
bottom-center pivot과 8m footprint를 재현한다. 원본과 호환성 정규화 FBX/texture,
license/report는 `Assets/ThirdParty`에 두고 stable asset ID/prefab key를 가진
material/prefab/definition/catalog은 `Assets/OpenSimCity`에 둔다.

`RepresentativeArtBenchmarkFixture`는 6개 binding을 4회 반복하고 production-like와
P3 placeholder mode만 교체한다. 이 fixture는 `Benchmark` 전용이며 `CityPrototype`,
simulation state, 저장 catalog와 LOD/collider 계약을 바꾸지 않는다. Windows 기준
renderer/material/texture memory/draw call/import 관찰값은
[P3-08 baseline](Benchmarks/P3-08-production-like-art-baseline.md)에 기록한다.

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

- P3-06 후속 Windows 검수 전: Noto Sans KR glyph rasterization, 125%/150% scale, tabular figure 또는 대체 정렬
- P9-01 이전: Standard만 사용할지 Source tier가 필요한지, 파생 에셋 배포 방식
- P8-02 이후: 모델별 triangle/material/LOD 기준과 target quality tier
- P9-02 이전: 제품 로고, 메인 메뉴 배경, 지원 언어와 현지화 범위
- 출시 후보 이전: 각 vendor·first-party asset의 provenance와 attribution 요구 최종 감사

이 항목이 확정되기 전에는 임시 값을 사용할 수 있지만, 임시 선택을 production 기준으로 문서화하거나 미구현 기능을 화면에 노출하지 않는다.

현재 실내처럼 검게 막히는 그림자 영역을 줄이기 위해 Trilight sky `(0.57,0.62,0.67)`, equator `(0.38,0.41,0.44)`, ground `(0.17,0.19,0.18)`와 ACES contrast `4`를 사용한다. 직접광·반사 대비를 남기면서 그늘에서도 건물 형태를 읽도록 한다.

### 2026-09-09: 전용 타이틀과 실제 크기 가독성

초기 진입은 전체 화면 도시 전경, 좌측 대형 OpenSimCity 타이틀, 세로 액션으로 구성한다. 가운데 작은 대화상자와 뒤에 비치는 게임 HUD를 사용하지 않는다. `title-city.png`는 프로젝트 자체 게임 렌더이며 저장된 현재 도시를 의미하지 않는다. 방향성 scrim은 별도 UI mesh로 그려 배경과 텍스트 대비를 분리한다. 초기에는 새 도시/저장 불러오기가 성공해야 게임 HUD를 보여주고, 아직 도시를 열지 않았을 때 복귀 버튼을 노출하지 않는다.

플레이 중 메뉴는 별도 graphite pause panel이며 열 때 이전 시뮬레이션 속도를 보관하고 닫으면 복원한다. 설정/불러오기는 스크롤 가능한 별도 영역과 돌아가기 동작을 갖는다. 좁은 레이아웃에서는 메뉴 대신 설정을 표시해 겹침을 방지한다. 첫 화면과 pause 모두 네 모서리를 각지게 유지한다.

PanelSettings reference는 1280×720이다. 100/125/150% 접근성 확대는 루트 transform이 아니라 PanelSettings scale에 적용하여 사용 가능한 레이아웃을 다시 계산한다. 작은 논리 화면에서는 제목/행 높이를 줄이며, 설정은 화면 안에서 스크롤한다. 접근성·오디오 설정 binder는 Bootstrap에서 CityPrototype으로 전환한 후에도 설치되어야 한다.
