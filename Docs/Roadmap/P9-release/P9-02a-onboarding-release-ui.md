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

## Done when

- [ ] 첫 15분 핵심 성장 루프와 대표 실패 복구가 공개 UI만으로 완료된다.
- [ ] task와 hint가 command/query 상태와 일치한다.
- [ ] 출시 메뉴가 구현된 오프라인 기능만 표시한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- key remap·UI scale·color alternative
- audio·day/night
- 온라인·커뮤니티·모드 UI
