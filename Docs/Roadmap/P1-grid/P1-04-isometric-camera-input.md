# [P1-04] 아이소메트릭 카메라·입력 구현

- Phase: `P1`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2 days`
- Branch: `feature/p1-04-isometric-camera`
- Depends on: `P0-02, P1-02`

## Goal

64x64 월드를 벗어나지 않는 이동·줌·90도 회전과 포인터 입력을 구현하고 카메라 조작과 도구 입력을 분리한다.

## Scope

- `Runtime/Gameplay/Camera`, Input System action
- 카메라 rig와 월드 bounds clamp
- 키보드·마우스 기본 조작과 PlayMode 테스트

## Mac steps

- 입력 action과 gameplay command 경계가 UI/Simulation에 역참조를 만들지 않는지 검토한다.
- 트랙패드 줌과 키 입력을 macOS에서 smoke 검증한다.
- 다른 화면 비율에서 bounds 계산 테스트를 실행한다.

## Windows steps

- 카메라 rig를 Editor 생성 도구로 구성하고 수동 Scene YAML 편집을 피한다.
- WASD/edge 또는 drag 이동, wheel zoom, Q/E 90도 회전을 구현한다.
- 16:9, ultrawide, 작은 창에서 clamp와 near/far clipping을 확인한다.
- 카메라 조작 중 포인터 click이 건설 명령으로 새지 않도록 입력 상태를 분리한다.

## Contracts and constraints

- 카메라는 simulation state를 수정하지 않는다.
- 월드 크기는 query/read model로 받고 Scene 이름 탐색을 사용하지 않는다.
- 매 프레임 `FindObjectOfType`나 불필요한 할당을 만들지 않는다.

## Verification

- 이동·줌 최소/최대·4회 회전
- 서로 다른 aspect ratio PlayMode test
- 포인터와 카메라 drag 충돌 smoke

## Done when

- [ ] 카메라가 월드를 안정적으로 탐색하고 경계를 벗어나지 않는다.
- [ ] 회전 후에도 셀 선택 좌표 변환 계약을 유지한다.
- [ ] Mac 입력 smoke가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 시네마틱 카메라
- 모바일·게임패드 입력
