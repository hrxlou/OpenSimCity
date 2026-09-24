# [P9-02B] 접근성·입력·UI 설정

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p9-02b-accessibility-settings`
- Depends on: `P2-00, P9-01`

## Goal

UI scale, 색상 대체, focus, key remap과 입력 설정을 저장 가능한 presentation 설정으로 제공한다.

## Scope

- 100/125/150% UI scale와 대표 aspect ratio
- 색상 외 icon/pattern/text 대체 채널
- keyboard focus traversal와 focus visibility
- mouse/keyboard 기본 binding과 key remap
- presentation settings DTO·저장·reset

## Mac steps

- Retina, trackpad, keyboard navigation과 설정 저장 경로를 검증한다.
- 입력/UI 설정 전후 simulation checksum과 command 의미가 같은지 확인한다.

## Windows steps

- P2-00 UI Toolkit token/focus 기반에 scale와 accessibility variant를 연결한다.
- binding conflict, reset, 잘못된 binding과 controller/mouse-keyboard 전환을 smoke한다.
- 대표 낮/밤 월드에서 text/status contrast와 color alternative를 캡처한다.

## Contracts and constraints

- 접근성 설정은 UI/Presentation과 Input mapping만 바꾸며 simulation tick, command ordering과 결과를 바꾸지 않는다.
- 주요 상태는 색상만으로 구분하지 않는다.
- 설정 DTO는 도시 save state와 분리하고 누락·구버전 값을 안전한 default로 복원한다.

## Verification

- scale·focus·color alternative PlayMode test
- binding conflict·remap·reset
- settings serialization round-trip
- 설정 variant별 simulation checksum
- Windows/macOS input smoke

## Done when

- [ ] UI scale, focus, color alternative와 key remap이 저장·복원된다.
- [ ] 주요 문제와 조작 상태에 색 외의 대체 채널이 있다.
- [ ] 설정 변경이 simulation 결과를 바꾸지 않는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 전체 screen reader 지원
- 현지화 전체 언어
- onboarding 내용 제작
- audio/day-night 설정

2026-09-09: Pretendard Regular/SemiBold TextCore, 1280×720 기준 크기 적용. UI 확대는 root transform에서 PanelSettings scale로 교체해 화면 경계를 다시 계산한다. 접근성/오디오 설정은 scene transition 이후 다시 설치된다. 전용 타이틀 설정 스크롤과 100/125/150% 경계 검사를 `ReleaseTitlePlayModeTests`에 추가했다.
