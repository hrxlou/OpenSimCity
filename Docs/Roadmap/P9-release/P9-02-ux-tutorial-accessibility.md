# [P9-02] UX·튜토리얼·접근성·오디오·낮밤 통합

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3+ days`
- Branch: `feature/p9-02-release-ux`
- Depends on: `P9-01`

## Goal

신규 사용자가 설명 없이 도로→구역→전력→성장 루프를 수행하도록 UX를 다듬고 주요 문제 설명, 접근성, 오디오, 낮/밤을 통합한다.

## Scope

- onboarding/tutorial tasks and contextual hints
- problem indicators, tooltip, datamap cross-links
- UI scale, remapping, color alternatives, audio controls
- day/night and audio presentation with settings persistence

## Mac steps

- 키보드/트랙패드, Retina scale, macOS audio/build에서 접근성을 검증한다.
- 튜토리얼이 command/query만 사용하고 simulation을 직접 우회하지 않는지 검토한다.
- 신규 사용자 task checklist와 문제 설명 coverage를 테스트한다.

## Windows steps

- 첫 15분 onboarding 흐름과 실패 회복 UX를 구현한다.
- 색상 외 icon/pattern, UI scale, key remap, volume 옵션을 연결한다.
- 낮/밤과 audio manager를 중앙 scheduler/settings로 구성한다.
- 대표 해상도와 controller/mouse-keyboard에서 PlayMode/사용성 smoke를 수행한다.

## Contracts and constraints

- 튜토리얼은 허용되지 않은 simulation mutation을 하지 않는다.
- 시각/오디오 설정은 결정성 결과에 영향을 주지 않는다.
- 주요 문제는 화면, 데이터맵, UI 중 두 곳 이상에서 설명한다.

## Verification

- 새 프로필 onboarding completion
- UI scale/color alternative/key remap
- audio/day-night on/off checksum
- 양쪽 build UX smoke

## Done when

- [ ] 신규 사용자가 핵심 성장 루프를 완료한다.
- [ ] 주요 도시 문제를 두 채널 이상에서 이해할 수 있다.
- [ ] 접근성·설정이 저장되고 양쪽 플랫폼에서 동작한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 현지화 전체 언어
- 마케팅 영상
- 온라인 기능
