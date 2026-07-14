# [P9-02] 출시 UX·접근성·presentation 통합 gate

- Phase: `P9`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `1 day gate review`
- Branch: `feature/p9-02-release-ux-gate`
- Depends on: `P9-02A, P9-02B, P9-02C`

## Goal

onboarding, 접근성/입력 설정, 오디오/낮밤을 각각 작은 이슈로 검증한 뒤 첫 15분 경험과 설정 지속성, 결정성 불변을 하나의 출시 UX gate로 판정한다.

## Execution issues

- [P9-02A onboarding·메뉴·문제 연결](P9-02a-onboarding-release-ui.md)
- [P9-02B 접근성·입력·UI 설정](P9-02b-accessibility-input-settings.md)
- [P9-02C 오디오·낮밤 presentation](P9-02c-audio-day-night-presentation.md)

## Scope

- 세 하위 이슈의 test/capture/settings evidence 검토
- 첫 15분 신규 프로필 end-to-end smoke
- 전력 또는 외부 연결 실패 진단→정상 command 복구
- Windows/macOS 설정 저장과 결정성 checksum 비교

## Mac steps

- Retina/trackpad/audio와 저장된 UI·입력·오디오 설정을 release candidate 설정으로 확인한다.
- 같은 tutorial command log의 checksum이 presentation 설정과 무관한지 검증한다.

## Windows steps

- 새 프로필에서 메뉴→새 도시→도로→구역→전력→첫 성장과 대표 실패 복구를 한 흐름으로 smoke한다.
- 대표 해상도와 mouse/keyboard/controller 설정에서 하위 이슈 결과가 함께 동작하는지 확인한다.

## Contracts and constraints

- 이 gate에서 새 UI, simulation 기능이나 첫 진단 채널을 구현하지 않는다. 실패하면 소유 하위 이슈로 되돌린다.
- 튜토리얼과 presentation 설정은 application command/query 경계를 우회하지 않고 simulation 결과를 바꾸지 않는다.
- AI 시안의 사진·logo·수치·문구를 production asset으로 사용하지 않는다.

## Verification

- P9-02A/B/C 완료 증거와 알려진 문제
- 새 프로필 first-15-minute 양 플랫폼 smoke
- UI/input/audio/day-night 설정 round-trip
- presentation on/off와 tutorial replay checksum

## Done when

- [ ] 신규 사용자가 첫 15분 안에 핵심 성장 루프와 대표 실패 복구를 완료한다.
- [ ] 주요 문제를 색상만이 아닌 두 채널 이상에서 이해한다.
- [ ] 접근성·입력·오디오·낮밤 설정이 저장되고 양 플랫폼에서 동작한다.
- [ ] presentation 설정 전후 simulation checksum이 같다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- gate 중 신규 기능 구현
- 현지화 전체 언어
- News·Community·Mods·Scenarios와 온라인 기능
- 마케팅 영상
