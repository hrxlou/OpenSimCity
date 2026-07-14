# [P0-02] Scene·프로젝트 설정 재현 도구 완성

- Phase: `P0`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `1–2 days`
- Branch: `tooling/p0-02-reproducible-scenes`
- Depends on: `P0-01`

## Goal

Bootstrap, CityPrototype, Benchmark Scene과 필수 프로젝트 설정을 수동 YAML 편집 없이 Editor 메뉴로 생성·검증할 수 있게 한다.

## Scope

- `Assets/OpenSimCity/Editor/ProjectSetup.cs` 생성·검증 메뉴
- Scene root, URP, 입력, build settings 검증
- 생성 결과를 확인하는 PlayMode smoke test

## Mac steps

- Editor 도구가 Runtime assembly에서 분리되는지 asmdef 참조를 검토한다.
- 생성 규칙을 EditMode에서 가능한 부분까지 테스트하고 문서 명령을 확인한다.
- Windows가 만든 commit을 받아 Scene diff가 안정적인지 확인한다.

## Windows steps

- 빈 상태 또는 테스트용 복제본에서 `OpenSimCity/Setup/Configure Project`를 실행한다.
- 세 Scene의 필수 root와 build index가 재실행 후 중복 생성되지 않는지 확인한다.
- URP renderer, Input System, serialization 설정을 검증 메뉴에 포함한다.
- Scene을 열어 카메라·조명·Bootstrap root가 정상인지 smoke 확인한다.

## Contracts and constraints

- 큰 Scene/Prefab YAML을 직접 편집하지 않는다.
- Editor 코드는 `OpenSimCity.Editor`에만 두고 Runtime에서 참조하지 않는다.
- 도구는 여러 번 실행해도 같은 결과를 만드는 idempotent 동작이어야 한다.

## Verification

- Editor 도구 2회 연속 실행 후 두 번째 실행에 의도치 않은 diff 없음
- Bootstrap Scene PlayMode smoke test
- Mac 재실행 후 Scene 참조 누락 없음

## Done when

- [x] 네 Scene(Bootstrap, MainMenu, CityPrototype, Benchmark)과 build settings를 메뉴로 재현할 수 있다.
- [x] 생성·검증 실패가 Console에 구체적인 원인으로 표시된다.
- [x] Windows 생성 결과가 Mac에서 동일하게 열린다.
- [x] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 게임플레이 카메라와 셀 선택
- 수동 Scene 미장센
