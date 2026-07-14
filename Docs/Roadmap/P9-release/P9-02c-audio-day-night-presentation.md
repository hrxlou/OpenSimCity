# [P9-02C] 오디오·낮밤 presentation과 설정

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p9-02c-audio-day-night`
- Depends on: `P9-02B`

## Goal

도시 상태를 바꾸지 않는 중앙 audio/day-night presentation과 volume·visual 설정을 통합한다.

## Scope

- 중앙 AudioManager와 music/SFX/ambient bus
- simulation date/time query를 표현하는 day/night lighting
- master/music/SFX/ambient volume과 day/night presentation 설정
- pause·배속·scene lifecycle
- 양 플랫폼 audio/render smoke

## Mac steps

- macOS audio device 변경, mute/background와 설정 round-trip을 검증한다.
- audio/day-night on/off와 quality 변경 전후 simulation checksum을 비교한다.

## Windows steps

- per-building Update 없이 중앙 scheduler/read model에서 lighting과 ambient 상태를 갱신한다.
- pause/배속/scene 전환에서 중복 AudioSource와 지속 object 누수를 확인한다.
- day/night 양쪽에서 UI·overlay 판독성을 실제 game render로 검증한다.

## Contracts and constraints

- 낮밤은 P9에서 presentation이며 전력, 교통, 수요, 범죄 등 simulation rule을 변경하지 않는다.
- 개별 건물·차량에 audio/day-night `Update()`를 추가하지 않는다.
- audio asset은 license/provenance와 LFS 정책을 따른다.

## Verification

- volume/mute/settings round-trip
- pause·배속·scene lifecycle
- day/night UI/overlay contrast smoke
- audio/day-night on/off checksum
- Windows/macOS build smoke

## Done when

- [ ] audio와 day/night가 중앙 관리되고 설정이 저장된다.
- [ ] presentation 전환이 simulation 결과를 바꾸지 않는다.
- [ ] 양 플랫폼에서 audio lifecycle과 낮/밤 판독성 smoke가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 날씨 simulation과 계절
- 위치별 정교한 acoustic simulation
- gameplay 효과를 가진 밤 정책
- 전체 soundtrack 제작
