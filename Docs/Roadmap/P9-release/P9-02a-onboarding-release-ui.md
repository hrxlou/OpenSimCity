# [P9-02A] onboarding·출시 메뉴·문제 연결

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p9-02a-onboarding-ui`
- Depends on: `P9-01`

## Goal

기존 command/query와 문제 표시를 연결해 신규 사용자가 첫 15분에 도로→구역→전력→첫 성장과 대표 실패 복구를 완료하도록 한다.

## Scope

- Continue/New/Load/Settings 중심 출시 메뉴
- onboarding task와 contextual hint
- `ProblemIndicator`, tooltip, datamap/context inspector cross-link
- 새 프로필·중단·재개 상태
- 실제 Unity game render 기반 UI polish

## Mac steps

- 튜토리얼이 simulation mutation을 우회하지 않고 application command/query만 사용하는지 검토한다.
- keyboard/trackpad와 Retina에서 task·tooltip·cross-link 흐름을 smoke한다.

## Windows steps

- 첫 15분 onboarding과 전력/외부 연결 실패 회복 fixture를 구현한다.
- 구현되지 않은 News, Community, Mods, Scenarios와 온라인 계정 항목을 노출하지 않는다.
- 실제 낮/밤 World View 위 panel/text/status 대비와 focus 순서를 확인한다.

## Contracts and constraints

- 핵심 시스템의 첫 진단 채널을 P9에서 새로 만들지 않고 기존 ViewModel과 경험 시나리오를 연결한다.
- tutorial task 완료는 명시적 command/result 또는 query state로 판정하며 Scene 이름이나 특정 GameObject를 찾지 않는다.
- 사진·AI 시안·임시 수치와 icon을 production asset으로 사용하지 않는다.

## Verification

- 새 프로필 onboarding completion PlayMode test
- 중단/재개와 save 상태
- 문제 indicator→상세→정상 command 복구
- 대표 해상도·focus·양 플랫폼 game render smoke

## 2026-09-05 UI shape pass

- 전역 색·spacing·radius·type token을 갱신하고 Global HUD, Tool Rail, Build Palette, Context Inspector와
  Simulation Controls를 같은 floating surface 언어로 통일했다.
- Tool Rail 명령은 상징·한글 label·단축키를 분리하고 overlay를 별도 chip group으로 재구성했다.
- 기능 노출과 command/query 경계는 유지하며, 대표 해상도 PlayMode smoke의 gutter와 palette 높이 계약을
  새 12px floating layout에 맞췄다.

## 2026-09-06 commercial reference pass

- SimCity 공식 매뉴얼의 하단 범주 메뉴와 데이터 맵 확장, Cities: Skylines II의 compact toolbar와
  요약→선택→상세 관리 흐름, Civilization VII의 map readability 우선순위를 비교했다.
- 상시 좌측 rail을 604px 하단 command dock으로 이동하고 선택 도구가 없을 때 Build Palette를 숨긴다.
- overlay 6종은 `데이터` popover, 관리 panel은 `도시 관리` drawer, 배치 상세는 preview 시에만 표시하여
  도시 월드가 화면의 기본 상태가 되게 했다.
- 고유 상표·아이콘·픽셀 배치는 복제하지 않고 정보 위계와 progressive disclosure 패턴만 채택한다.

## 2026-09-06 angular game-HUD pass

- 공식 `Pretendard 1.3.9` variable font로 교체하고 caption/body/strong를 13/15/16px로 높였다.
- 모든 HUD surface를 0–2px radius, 88% charcoal, 얇은 first-party grain과 1px 경계로 통일했다.
- Inspector의 primary/secondary/status 행을 같은 높이와 좌우 inset으로 정렬하고 긴 한글 안내는 줄바꿈한다.
- 선택 도구가 없으면 palette와 inspector를 닫고, 전역 debug grid와 혼잡 overlay도 기본 비활성화해 월드가
  기본 화면이 되게 한다.

## Done when

- [ ] 첫 15분 핵심 성장 루프와 대표 실패 복구가 공개 UI만으로 완료된다.
- [ ] task와 hint가 command/query 상태와 일치한다.
- [ ] 출시 메뉴가 구현된 오프라인 기능만 표시한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- key remap·UI scale·color alternative
- audio·day/night
- 온라인·커뮤니티·모드 UI

## 2026-09-09 UI·광원·표면 수정

사용자 확인에 따라 곡률 0을 유지하고 장식 사이드 강조선을 제거했다. 무광 graphite 결·얇은 명암 경계, 독립 HUD 지표군, 도구 이름, Pretendard 정적 Regular/SemiBold TextCore SDF를 적용했다.

세부 재현 계약은 `Docs/DESIGN.md`의 2026-09-09 절, 출처는 `Docs/Art/VISUAL_REFERENCES_2026-09-09.md`를 따른다. 최종 실행 검증 기록은 후속 검증 절에 추가한다.

### macOS 검증 결과

Unity 6000.5.3f1: EditMode 380 통과/실패 0/Windows 전용 1 제외, PlayMode 56 통과/실패 0. macOS Standalone build 및 native 1920×1080 실제 리뷰 렌더 확인. v08 export는 Blender Cycles에서도 검수했다. 상세 파일은 ignored `Artifacts/VisualPolish/2026-09-09/VERIFICATION.md` 및 XML·PNG·app. Windows 실행 증거는 이번 Mac 작업에 포함되지 않으며, P9 전체의 양 플랫폼 출시 승인을 의미하지 않는다.

전용 초기 타이틀 후속: 자체 게임 도시 전경을 full-screen 배경으로 사용하고 큰 제목·세로 액션을 좌측에 배치했다. 초기 HUD를 숨기며 새 도시/불러오기 성공 시 진입한다. pause 메뉴는 이전 속도를 복원하며, 설정/불러오기 영역에 돌아가기 및 작은 화면 전환을 추가했다. 초기 화면·세션 action·pause·100/125/150% 화면 경계 PlayMode smoke를 추가했다.
