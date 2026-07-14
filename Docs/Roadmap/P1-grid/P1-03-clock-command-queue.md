# [P1-03] SimulationClock·명령 queue 구현

- Phase: `P1`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2 days`
- Branch: `feature/p1-03-clock-command-queue`
- Depends on: `P1-01, P1-02`

## Goal

프레임 속도와 분리된 고정 simulation tick, 배속 제어, tick/sequence 순서의 application command queue를 구현한다.

## Scope

- `Runtime/Application/Bootstrap`, `Commands`
- pause/1x/2x/4x 속도 상태와 catch-up 제한
- 명령 검증·정렬·실행 결과 테스트

## Mac steps

- clock에 실제 시간 누적과 고정 tick 소비를 분리한다.
- 동일 tick 명령은 sequence로 안정 정렬하고 중복 stamp는 거부한다.
- 명령이 simulation 배열을 직접 노출하지 않도록 handler/facade 경계를 만든다.
- pause, 배속, 큰 delta, 명령 순서 EditMode 테스트를 추가한다.

## Windows steps

- 낮은/높은 editor frame rate에서 tick 수가 기대 범위인지 PlayMode smoke로 확인한다.
- 창 focus 변화 뒤 무제한 catch-up이 발생하지 않는지 확인한다.
- 동일 command fixture의 최종 state checksum을 Mac과 비교한다.

## Contracts and constraints

- Gameplay/View는 simulation 배열을 직접 수정하지 않는다.
- 표현 프레임 수가 simulation 결과를 바꾸지 않아야 한다.
- 시간 누적에 platform clock의 절대값이나 locale을 저장하지 않는다.

## Verification

- 고정 delta 분할 방식이 달라도 동일 tick 수와 결과
- 동일 tick sequence 순서
- pause 상태에서 simulation 불변
- catch-up 상한

## Done when

- [ ] 정해진 command stream이 frame pacing과 무관하게 같은 결과를 낸다.
- [ ] 배속 변경이 command ordering을 깨지 않는다.
- [ ] 양쪽 장비 테스트 결과가 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 실제 도로 배치 명령
- 월간 경제 scheduler
