# P0-03: Windows CI baseline and standalone build

## 목적

PR과 `main` 병합을 자동으로 검증하고, Windows Standalone 빌드를 자동으로 만들어
다운로드 가능한 artifact로 보관하는 최소 CI/CD 기반을 만든다.

역할 분담(초기 단계):

- **Mac**: Core/Simulation/Application 코드, EditMode 테스트, 문서
- **Windows**: Unity 통합 테스트, Windows 빌드, GitHub Actions, Blender 에셋 파이프라인
- **GitHub Actions**: 반복 가능한 자동 검증

플랫폼별로 입력, 파일 경로, 셰이더, 그래픽 설정 문제가 생길 수 있으므로 다음 3단 구조를 유지한다.

- Windows: Windows 빌드와 PlayMode 검증
- Mac: EditMode와 최소 smoke test 재확인
- GitHub Actions: 반복 가능한 자동 검증

## 범위

포함:

1. PR마다 컴파일 오류 확인 + EditMode 테스트 실행
2. PlayMode smoke test 실행
3. `main` 병합 시 Windows Standalone 빌드
4. 빌드 결과를 GitHub Actions artifact로 저장
5. 태그(`v*`) 생성 시에만 릴리스 후보 빌드 생성

제외(MVP 안정화 이후로 미룸):

- Steam / itch.io 업로드
- 코드 서명
- 자동 릴리스 / 자동 배포

초기 CD는 "Windows 빌드를 자동으로 만들어 다운로드 가능하게 보관"하는 수준까지만 다룬다.

## 구성 파일

```
.github/workflows/
  ci.yml               # PR/기능 브랜치: EditMode + PlayMode 테스트
  build-windows.yml    # main push + 태그: StandaloneWindows64 빌드 artifact

Tools/CI/
  run-unity-tests.sh   # macOS/Linux 테스트 실행 (기존)
  run-unity-tests.ps1  # Windows 테스트 실행 (동일 인터페이스)
  build-windows.ps1    # 로컬 Windows 빌드

Assets/OpenSimCity/Editor/
  BuildScript.cs       # 빌드 단일 진입점 (CI/로컬 공통)

Docs/Roadmap/P0/
  P0-03-windows-ci.md  # 이 문서
```

## 실행 방식에 대한 결정

빌드는 `game-ci`를 사용해 **Linux 컨테이너(`ubuntu-latest`)에서 `StandaloneWindows64`로
크로스 컴파일**한다. 별도 Windows 러너 없이 Windows 실행 파일을 만들 수 있어 CI 비용이 낮고
설정이 단순하다. Windows 고유 문제(입력, 경로, 셰이더)는 Windows 기기의 로컬 빌드
(`Tools/CI/build-windows.ps1`)와 PlayMode 검증으로 별도 확인한다.

로컬과 CI는 모두 `OpenSimCity.Editor.BuildScript.PerformWindowsBuild`를 진입점으로 사용해
동일한 빌드 방식을 재현한다.

## 필요한 GitHub Secrets

라이선스나 인증 정보는 저장소에 두지 않고 리포지토리 Secrets로만 주입한다.
(Settings → Secrets and variables → Actions)

| Secret | 설명 |
|---|---|
| `UNITY_LICENSE` | Unity Personal `.ulf` 라이선스 파일 내용. `UNITY_EMAIL`/`UNITY_PASSWORD`로 발급받아 등록한다. |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |

`game-ci` 라이선스 활성화 절차는 GameCI 문서(Activation)를 따른다.
Secrets가 설정되기 전에는 워크플로가 라이선스 단계에서 실패한다.

## 로컬 실행

```powershell
# Windows
./Tools/CI/run-unity-tests.ps1 all
./Tools/CI/build-windows.ps1
```

```bash
# macOS / Linux
./Tools/CI/run-unity-tests.sh all
```

`UNITY_EDITOR` 환경변수로 Unity 실행 파일 경로를 재정의할 수 있다.

## 완료 조건

- [ ] PR에서 EditMode 테스트가 자동 실행된다.
- [ ] PlayMode smoke test가 자동 실행된다.
- [ ] `main` 병합 시 Windows 빌드 artifact가 생성된다.
- [ ] 실패 시 Unity 로그가 Actions artifact에 남는다.
- [ ] Mac과 Windows에서 같은 명령으로 테스트를 실행할 수 있다.
- [ ] Unity 버전(`ProjectVersion.txt`)과 패키지 lock(`packages-lock.json`)이 CI에 고정된다.
- [ ] 라이선스나 인증 정보가 저장소에 노출되지 않는다(Secrets만 사용).
- [ ] 태그(`v*`) push 시 릴리스 후보 빌드가 별도 보관된다.

## 후속 작업

- MVP 안정화 후 코드 서명 및 릴리스 자동화 추가
- Blender → Unity 에셋 파이프라인(프리팹/LOD 통합)을 Windows에서 구축
