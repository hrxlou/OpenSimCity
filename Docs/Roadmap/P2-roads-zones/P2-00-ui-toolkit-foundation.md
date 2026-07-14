# [P2-00] UI Toolkit·디자인 토큰·입력 포커스 기반

- Phase: `P2`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `1–2 days`
- Branch: `feature/p2-00-ui-foundation`
- Depends on: `P1-05`

## Goal

P2 배치 UX와 P3 HUD가 같은 기술·토큰·scale·입력 포커스 계약을 재사용하도록 최소 UI 기반을 먼저 고정한다.

## Scope

- runtime UI 기술을 Unity UI Toolkit으로 확정
- `Assets/OpenSimCity/Runtime/UI/Foundation`의 document/bootstrap과 focus/input boundary
- `Assets/OpenSimCity/Content/UI/Styles`의 color, spacing, type scale, border와 state USS token
- panel scale, safe area, 창 resize와 pointer picking 계약
- `ToolRail`, `BuildPalette`, `ContextInspector`, `DataOverlayLegend`의 빈 shell과 상태 fixture
- 디자인 token·component validator와 PlayMode smoke

## Mac steps

- UI assembly가 Core/Application만 참조하고 Simulation·Presentation 구현을 역참조하지 않는지 확인한다.
- Retina, 100/125/150% scale과 창 resize에서 type/spacing token을 smoke 검증한다.
- keyboard focus 순서와 트랙패드/포인터가 World selection으로 새지 않는지 확인한다.

## Windows steps

- [디자인 기준](../../DESIGN.md)의 token을 USS custom property와 공통 class로 정의한다.
- 공용 PanelSettings/UIDocument 생성 절차를 Editor 도구로 재현한다.
- World View 위 panel picking, focus capture/release와 Escape 취소의 입력 경계를 구현한다.
- 네 component shell의 default/hover/focus/selected/disabled/warning/error fixture를 만든다.

## Contracts and constraints

- runtime UI는 Unity UI Toolkit을 사용한다. world-space grid·도로·datamap처럼 대량 geometry가 필요한 표현은 Presentation renderer가 담당하고 UI Toolkit이 simulation을 재계산하지 않는다.
- USS token과 공통 component를 우회한 화면별 임의 색·간격을 만들지 않는다.
- P2-00은 shell과 interaction foundation만 제공하며 성장·재정·교통·서비스 값을 가짜로 표시하지 않는다.
- 최종 한글 font/icon asset은 P3-06 이전 라이선스 gate에서 확정하되, type scale과 fallback 동작은 여기서 검증한다.
- Scene/Prefab/UIDocument 구성은 가능한 한 Editor 도구로 재현한다.

## Verification

- USS token 누락·중복 validator
- UI 위 click/drag와 World selection 분리 PlayMode test
- keyboard focus traversal와 Escape focus release
- 16:9, ultrawide, 작은 창과 100/125/150% scale smoke
- Windows/macOS shell render 비교

## Done when

- [ ] runtime UI 기술과 source 경로가 UI Toolkit으로 확정된다.
- [ ] 공통 color/spacing/type/state token과 네 component shell이 재사용 가능하다.
- [ ] UI pointer/focus가 카메라·셀 선택으로 누출되지 않는다.
- [ ] 양 플랫폼의 scale·resize smoke가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 실제 도로·구역 배치 화면 연결
- P3 HUD 데이터 연결
- 최종 font/icon set과 출시 polish
- 세계 공간 overlay geometry 구현
