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

- `.github/workflows/ci.yml`: 기능 브랜치에서 clean checkout 뒤 EditMode + PlayMode 테스트
- `.github/workflows/build-windows.yml`: `main`/태그의 동일 SHA를 clean checkout에서 테스트한 뒤 Windows 빌드 artifact 또는 릴리스 후보 생성
- `Library`는 source checkout과 분리된 branch/ref별 Actions cache로 관리
- `Tools/CI/run-unity-tests.ps1`: 기존 `run-unity-tests.sh`와 동일 인터페이스의 Windows 버전
- `Tools/CI/build-windows.ps1` + `Assets/OpenSimCity/Editor/BuildScript.cs`: 로컬/CI 공통 빌드 진입점

Out of scope로 미루는 항목(MVP 안정화 이후):

- Steam / itch.io 업로드
- 코드 서명
- 자동 릴리스 / 자동 배포

초기 CD는 "Windows 빌드를 자동으로 만들어 다운로드 가능하게 보관"하는 수준까지만 다룬다.

## 라이선스 방식에 대한 결정 (중요)

Unity는 2023년부터 Personal 라이선스의 수동 활성화(`.alf`/`.ulf` 업로드)를 막았고, 커뮤니티
우회 방법도 2024년 중반부터 동작하지 않는다([game-ci/documentation#408](https://github.com/game-ci/documentation/issues/408)).
그 결과 `game-ci/unity-builder`·`unity-test-runner`가 `ubuntu-latest`에서 `UNITY_LICENSE`
Secret으로 라이선스를 발급받는 방식은 **Pro/Plus 유료 라이선스에서만 동작**한다.

Personal 라이선스를 유지하는 동안은 GitHub 호스팅 러너 대신, 이미 Unity Hub로 라이선스가
활성화된 이 프로젝트 소유자의 **Windows PC를 self-hosted 러너로 등록**해 그 위에서 직접
Unity를 실행한다. `game-ci` 액션과 UNITY_LICENSE/EMAIL/PASSWORD Secrets는 전혀 필요 없다.
후에 Unity Plus/Pro로 전환하면 `ubuntu-latest` + `game-ci` 방식으로 되돌릴 수 있다.

## Windows steps

- 이 PC에 [GitHub Actions self-hosted runner](https://github.com/hrxlou/OpenSimCity/settings/actions/runners)를
  등록한다. 라벨은 `self-hosted, Windows, unity`를 포함해야 워크플로가 이 러너를 찾는다.
  실행 파일을 서비스로 설치해(`./svc.sh install` 상당의 Windows 절차 `./run.cmd` 또는
  `./svc install`) PC 재시작 후에도 자동으로 살아나게 한다.
- `.github/workflows/ci.yml`, `build-windows.yml`을 self-hosted 러너에서 기존
  `Tools/CI/run-unity-tests.ps1` / `build-windows.ps1`을 직접 호출하도록 작성한다
  (game-ci 액션 미사용).
- `Tools/CI/build-windows.ps1`로 로컬에서 `OpenSimCity.Editor.BuildScript.PerformWindowsBuild`를
  실행해 CI와 동일한 빌드 결과가 나오는지 확인한다.
- 러너가 오프라인이면 CI가 큐에서 대기만 하고 실패하지 않는지 확인한다(PC를 끈 상태로 push해본다).

## Mac steps

- `Tools/CI/run-unity-tests.ps1`이 `run-unity-tests.sh`와 같은 `edit|play|all` 인터페이스를
  제공하는지 diff로 확인한다.
- PR을 열어 `ci.yml`이 EditMode/PlayMode 결과와 Unity 로그를 artifact로 남기는지 확인한다.
- `BuildScript.cs`가 `OpenSimCity.Editor` asmdef에만 있고 Runtime에서 참조되지 않는지 확인한다.

## Contracts and constraints

- Unity 버전(`ProjectSettings/ProjectVersion.txt`)과 패키지 lock(`Packages/packages-lock.json`)을
  로컬과 CI가 동일하게 사용한다(같은 PC의 같은 Unity 설치이므로 자동으로 일치).
- 라이선스나 인증 정보를 저장소나 워크플로에 두지 않는다. self-hosted 러너 방식에서는 애초에
  Secrets 자체가 필요 없다.
- 로컬 빌드와 CI 빌드는 동일한 진입점(`BuildScript.PerformWindowsBuild`)을 사용해 재현성을 보장한다.
- checkout은 `clean: true`로 이전 branch의 untracked asset·생성물을 제거하고 `Library`만 같은 branch/ref의 별도 cache에서 복원한다.
- `main`/태그 build job은 같은 SHA의 EditMode·PlayMode tests job 성공을 `needs`로 요구한다.
- Editor Build Settings에는 Benchmark를 둘 수 있지만 release BuildScript는 Bootstrap/MainMenu/CityPrototype의 명시적 제품 Scene 목록만 사용한다.
- self-hosted 러너는 PR에서 들어온 코드를 그대로 실행하므로, 저장소가 Public으로 전환되기 전에
  fork PR 실행을 제한하는 설정(`pull_request_target` 미사용, Actions 권한을 Collaborator로 제한)을
  검토한다.

## Verification

- 워크플로 YAML 파싱과 PowerShell 스크립트 구문 검사(로컬)
- self-hosted 러너 등록 후 `Settings > Actions > Runners`에서 Idle 상태 확인
- PR에서 `ci.yml`이 실제로 EditMode/PlayMode를 실행하고 결과를 남기는지 확인
- `main` 병합 후 `build-windows.yml`이 같은 SHA의 EditMode/PlayMode를 먼저 통과하고 Windows 빌드 artifact를 생성하는지 확인
- 이전 branch에 untracked fixture를 남긴 뒤 다음 run의 clean checkout에 섞이지 않는지 확인
- release artifact에 Benchmark Scene이 포함되지 않는지 BuildScript EditMode test와 build log로 확인
- 실패를 의도적으로 재현해 Unity 로그가 artifact에 남는지 확인

## Done when

- [ ] PR에서 EditMode 테스트가 자동 실행된다.
- [ ] PlayMode smoke test가 자동 실행된다.
- [ ] `main` 병합 시 Windows 빌드 artifact가 생성된다.
- [ ] `main`과 태그의 정확한 build SHA에서 EditMode/PlayMode가 먼저 통과한다.
- [ ] self-hosted workspace는 clean checkout이고 Library만 별도 cache로 재사용한다.
- [ ] release Scene 목록에서 Benchmark가 제외된다.
- [ ] 실패 시 Unity 로그가 Actions artifact에 남는다.
- [ ] Mac과 Windows에서 같은 명령(`run-unity-tests.sh` / `.ps1`)으로 테스트를 실행할 수 있다.
- [ ] Unity 버전과 패키지 lock 파일이 로컬/CI에서 일치한다.
- [ ] 워크플로에 라이선스나 인증 정보가 전혀 포함되지 않는다(self-hosted 러너는 Secrets 불필요).
- [ ] self-hosted 러너가 서비스로 등록되어 PC 재시작 후에도 자동 복구된다.
- [ ] 태그(`v*`) push 시 릴리스 후보 빌드가 별도 보관된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 워크플로 실행 로그를 기록한다.

## Out of scope

- Steam / itch.io 업로드, 코드 서명, 자동 릴리스
- Blender → Unity 에셋 파이프라인(프리팹/LOD 통합) — 후속 이슈로 별도 진행
