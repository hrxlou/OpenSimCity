# [P4-03] 저장 UI·자동 저장·교차 플랫폼 복원 검증

- Phase: `P4`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `codex/p4-03-save-ui-cross-platform-roundtrip`
- Depends on: `P4-02`

## Goal

저장/불러오기 UI와 자동 저장을 연결하고 Windows와 macOS가 서로 만든 성장 도시 저장 파일을 동일 상태로 복원하는 P4 gate를 통과한다.

## Scope

- save slot/list metadata ViewModel과 UI
- manual save/load, autosave trigger, progress/error feedback
- load 후 cache/view 재구축
- cross-platform golden save와 PlayMode smoke
- load 전후 플레이어용 reason/ViewModel과 다음 기간 결과 비교

## Mac steps

- Windows save를 load해 state checksum과 다음 월 결과를 비교한다.
- load 전후 `DevelopmentStatusViewModel`의 기간·상위 reason·실행 가능 필지 수를 비교한다.
- macOS save를 만들어 Windows 검증용 artifact로 제공한다.
- 손상/unknown version 메시지가 안전하고 이해 가능한지 검토한다.

## Windows steps

- UI action을 application persistence command에 연결한다.
- load 중 입력/clock을 안전하게 중지하고 성공 후 view/cache를 재구축한다.
- autosave가 temp file과 UI 상태를 올바르게 처리하는지 검증한다.
- Mac save를 load하고 다음 월까지 진행해 checksum을 기록한다.

## Contracts and constraints

- UI는 파일을 직접 해석하지 않는다.
- load 후 GameObject/View instance ID를 복원하려 하지 않는다.
- 자동 저장은 simulation tick 경계에서 일관된 snapshot을 취한다.

### Implemented contract

- Application의 `CityPersistenceController`가 `manual-1`/`autosave` slot metadata ViewModel,
  manual save/load command, 진행·오류 feedback과 매월 autosave cadence를 소유한다. UI는 파일이나
  JSON을 직접 읽지 않는다.
- persistence request는 즉시 clock과 construction input을 pause하고 Platform host의 다음 update에서
  실행한다. autosave는 `SimulationClock.CurrentTick`이 다음 tick으로 commit된 월 경계에서만 예약한다.
- load는 decode·validation·runtime restore가 모두 성공하기 전 기존 도시를 유지한다. 성공하면
  `CityRuntimeHost.ReplaceRuntime`이 새 context/world bounds를 publish하고 중앙 pooled road/building/
  power renderer가 stable-ID read model에서 view/cache를 다시 만든다.
- 손상·unknown version·잠금/권한/쓰기 오류는 partial apply 없이 player message로 매핑하며
  `다시 시도`와 `현재 도시 계속하기`를 제공한다.
- v1 Windows gate fixture는 `p4-v1-roundtrip-windows.json`이다. 실제 save payload는 `5,664 bytes`,
  FNV-1a `6039FE7670E3C298`, 정상 전력 복구 command와 다음 월 뒤 checksum은
  `68BE53564A1F46A7`이다.

## Verification

- Windows↔Mac 교차 load와 다음 월 checksum
- manual/autosave/overwrite
- 손상·unknown version UI
- load 전후 view cache 재구축

## Experience acceptance

- 성장 정지 reason이 표시된 도시를 저장하고 반대 플랫폼에서 불러왔을 때 같은 기간·reason·개발 가능 필지 수를 표시한다.
- load 뒤 저장 전과 같은 정상 복구 command를 실행하면 같은 다음 기간에 같은 의미의 성장 재개 결과가 나온다.
- 손상되거나 지원하지 않는 저장은 도시를 부분 적용하지 않고 플레이어가 이해 가능한 오류와 복구 선택을 제공한다.

## Done when

- [ ] 양쪽 플랫폼이 서로 만든 저장 파일을 동일 상태로 연다.
- [ ] 손상 파일과 알 수 없는 버전을 안전하게 알린다.
- [ ] 저장 전후 공개 원인·가능 명령·다음 기간 결과가 의미상 동일하다.
- [ ] P4 gate 증거와 save fixture 버전이 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Windows implementation evidence (2026-07-23)

- OS: Windows 11 Enterprise `10.0.26200` build `26200` (`x64`)
- Unity: `6000.5.3f1` (`c2eb47b3a2a9`)
- Branch: `codex/p4-03-save-ui-cross-platform-roundtrip`
- ProjectSettings index baseline guard: passed
- P4-03 EditMode fixture:
  `CityPersistenceControllerTests` → `4/4` passed
- P4-03 PlayMode smoke:
  `SaveRoundTripPlayModeTests` → `2/2` passed
- Full Unity regression:
  `Tools/CI/run-unity-tests.ps1 all` → EditMode `195/195`, PlayMode `34/34` passed
- Windows v1 fixture: `5,664 bytes`, FNV-1a `6039FE7670E3C298`
- 정상 전력 복구 command와 다음 월 뒤 checksum: `68BE53564A1F46A7`
- manual save/overwrite, 매월 tick-boundary autosave, manual load, load 중 clock/input/세율 command
  차단, context 교체와 pooled road view 재구축, 손상/unknown version의 partial-apply 방지 및
  recovery 선택을 자동 fixture로 검증했다.
- Commit/PR은 publish 후 최종 evidence commit에서 이 절에 기록한다.

## Remaining verification / blocked

- macOS와 Unity `6000.5.3f1`에서 Windows fixture를 load해 기간·상위 reason·실행 가능 필지 수,
  recovery command preview와 다음 월 checksum `68BE53564A1F46A7`을 재현해야 한다.
- macOS가 같은 scenario의 v1 `.osc-save`를 만들어 byte count/checksum과 함께 전달하고,
  Windows가 이를 load→다음 월까지 검증해야 양방향 항목과 P4 gate를 완료할 수 있다.
- 현재 작업 환경에는 macOS 장비가 없으므로 위 두 항목은 이 PR에서 Blocked다.

## Out of scope

- Steam Cloud
- 여러 사용자 profile
- 저장 압축 최적화
