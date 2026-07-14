# [P0-04] GitHub Actions CI/CD 기반 구축

- Phase: `P0`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `1–2 days`
- Branch: `feature/windows-ci-baseline`
- Depends on: `P0-01, P0-03`

## Goal

PR과 `main` 병합을 GitHub Actions로 자동 검증하고, Windows Standalone 빌드를 자동으로
만들어 다운로드 가능한 artifact로 보관하는 최소 CI/CD 기반을 갖춘다. `P0-03`이 정의한
수동 장비 인계 루틴에 반복 가능한 자동 검증 레이어를 추가한다.

## Scope

- `.github/workflows/ci.yml`: PR/기능 브랜치에서 EditMode + PlayMode 테스트
- `.github/workflows/build-windows.yml`: `main` 병합 시 Windows 빌드 artifact, 태그(`v*`) 시 릴리스 후보
- `Tools/CI/run-unity-tests.ps1`: 기존 `run-unity-tests.sh`와 동일 인터페이스의 Windows 버전
- `Tools/CI/build-windows.ps1` + `Assets/OpenSimCity/Editor/BuildScript.cs`: 로컬/CI 공통 빌드 진입점

Out of scope로 미루는 항목(MVP 안정화 이후):

- Steam / itch.io 업로드
- 코드 서명
- 자동 릴리스 / 자동 배포

초기 CD는 "Windows 빌드를 자동으로 만들어 다운로드 가능하게 보관"하는 수준까지만 다룬다.

## Windows steps

- `.github/workflows/ci.yml`, `build-windows.yml`을 작성하고 `game-ci`로 EditMode/PlayMode
  테스트와 `StandaloneWindows64` 빌드를 구성한다.
- 빌드는 `ubuntu-latest`에서 `game-ci/unity-builder`로 크로스 컴파일해 별도 Windows 러너 없이
  CI 비용을 낮춘다. Windows 고유 문제(입력, 경로, 셰이더)는 로컬 빌드와 PlayMode 검증으로 별도 확인한다.
- `Tools/CI/build-windows.ps1`로 로컬에서 `OpenSimCity.Editor.BuildScript.PerformWindowsBuild`를
  실행해 CI와 동일한 빌드 결과가 나오는지 확인한다.
- 리포지토리 Secrets(`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`)를 등록하고 워크플로가
  라이선스 단계를 통과하는지 확인한다.

## Mac steps

- `Tools/CI/run-unity-tests.ps1`이 `run-unity-tests.sh`와 같은 `edit|play|all` 인터페이스를
  제공하는지 diff로 확인한다.
- PR을 열어 `ci.yml`이 EditMode/PlayMode 결과와 Unity 로그를 artifact로 남기는지 확인한다.
- `BuildScript.cs`가 `OpenSimCity.Editor` asmdef에만 있고 Runtime에서 참조되지 않는지 확인한다.

## Contracts and constraints

- Unity 버전(`ProjectSettings/ProjectVersion.txt`)과 패키지 lock(`Packages/packages-lock.json`)을
  CI 캐시 키와 빌드 조건에 그대로 사용한다.
- 라이선스나 인증 정보는 저장소에 두지 않고 GitHub Secrets로만 주입한다.
- 로컬 빌드와 CI 빌드는 동일한 진입점(`BuildScript.PerformWindowsBuild`)을 사용해 재현성을 보장한다.

## Verification

- 워크플로 YAML 파싱과 PowerShell 스크립트 구문 검사(로컬)
- PR에서 `ci.yml`이 실제로 EditMode/PlayMode를 실행하고 결과를 남기는지 확인
- `main` 병합 후 `build-windows.yml`이 Windows 빌드 artifact를 생성하는지 확인
- 실패를 의도적으로 재현해 Unity 로그가 artifact에 남는지 확인

## Done when

- [ ] PR에서 EditMode 테스트가 자동 실행된다.
- [ ] PlayMode smoke test가 자동 실행된다.
- [ ] `main` 병합 시 Windows 빌드 artifact가 생성된다.
- [ ] 실패 시 Unity 로그가 Actions artifact에 남는다.
- [ ] Mac과 Windows에서 같은 명령(`run-unity-tests.sh` / `.ps1`)으로 테스트를 실행할 수 있다.
- [ ] Unity 버전과 패키지 lock 파일이 CI에 고정된다.
- [ ] 라이선스나 인증 정보가 저장소에 노출되지 않는다(Secrets만 사용).
- [ ] 태그(`v*`) push 시 릴리스 후보 빌드가 별도 보관된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 워크플로 실행 로그를 기록한다.

## Out of scope

- Steam / itch.io 업로드, 코드 서명, 자동 릴리스
- Blender → Unity 에셋 파이프라인(프리팹/LOD 통합) — 후속 이슈로 별도 진행
