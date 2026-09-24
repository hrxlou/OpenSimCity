# PC·Mac 개발 환경 설정

이 문서는 두 장비가 같은 Unity 프로젝트와 Git 저장소를 안전하게 공유하기 위한 최초 설정 체크리스트다. 일상적인 브랜치, 커밋, 에셋 작업 규칙은 [WORKFLOW.md](WORKFLOW.md)를 참고한다.

## 1. 공통 원칙

- Unity Editor는 `ProjectSettings/ProjectVersion.txt`에 기록된 정확한 버전을 양쪽에 설치한다.
- Blender도 팀 기준 버전을 하나 정하고, 버전 변경은 두 장비에서 함께 한다.
- `Assets`, `Packages`, `ProjectSettings`와 모든 `.meta`를 Git으로 공유한다.
- `Library`, `Temp`, `Logs`, `Obj`, `Builds`, `UserSettings`는 장비마다 다시 생성한다.
- 프로젝트 이동은 Git commit/push/pull로만 한다. 클라우드 드라이브로 Unity 작업 폴더를 동기화하지 않는다.
- 대형 바이너리는 Git LFS로 관리하고 양쪽에서 `git lfs install`을 실행한다.

## 2. Mac 설정

대상 역할: C# 로직, 문서, Codex, EditMode 테스트, 간단한 Unity 작업, macOS/iOS 빌드.

### 필수 도구

- [x] Xcode Command Line Tools
- [x] Homebrew
- [x] Git
- [x] Git LFS
- [x] GitHub CLI 또는 GitHub Desktop
- [x] Unity Hub
- [x] 프로젝트가 고정한 Unity 6 Editor (`6000.5.3f1`, arm64)
- [x] Blender 기준 버전
- [x] VS Code와 C# 확장

권장 설치·확인 명령:

```bash
xcode-select --install
brew install git-lfs gh
git lfs install
git --version
git lfs version
gh auth login
gh auth status
```

iOS 빌드가 필요할 때만 전체 Xcode와 Unity의 iOS Build Support를 추가한다. 일반 C#·Unity 개발만 한다면 초기에 iOS 모듈을 설치할 필요는 없다.

### Mac 확인 사항

- [x] `git config --global user.name`과 `user.email` 설정
- [x] GitHub HTTPS 인증 완료
- [x] Unity Hub 설치 (`3.19.5`)
- [x] Unity Editor 설치 (`6000.5.3f1`, arm64)
- [x] VS Code 및 C# 확장 설치
- [ ] Unity Preferences에서 외부 스크립트 편집기를 VS Code로 확인
- [x] Blender 설치 (`5.1.2`)
- [x] Blender에서 Metal GPU 사용 확인 (`CYCLES_DEVICE_TYPE=METAL`)
- 권장 운영: 16GB 메모리를 고려해 Unity·Blender·브라우저 동시 실행을 최소화한다.

## 3. Windows PC 설정

대상 역할: Unity 메인 편집, Blender, 대형 Scene, Windows 빌드, GPU·메모리 프로파일링.

### 필수 도구

- [ ] RTX 3060용 최신 안정 NVIDIA Studio Driver (현재 드라이버 버전은 확인했지만 Studio 채널과 최신 여부는 별도 확인 필요)
- [x] Git for Windows
- [x] Git LFS
- [x] GitHub CLI 또는 GitHub Desktop
- [x] Unity Hub
- [x] Unity Editor `6000.5.3f1` (x86_64)
- [x] Windows Build Support (IL2CPP; 필요하면 Mono도 추가)
- [x] VS Code와 C# 확장 또는 Visual Studio 2022의 Unity 워크로드/Rider
- [x] Blender `5.1.2`

PowerShell 또는 Git Bash에서 확인한다.

```powershell
git --version
git lfs install
git lfs version
gh auth login
gh auth status
git config --global user.name "hrxlou"
git config --global user.email "YOUR_GITHUB_NOREPLY_ADDRESS"
```

프로젝트의 `Packages/manifest.json`에 고정된 패키지는 양쪽에서 따로 설치하지 않는다. Git으로 받은 뒤 Unity가 자동으로 해석한다.

```text
URP:           17.5.0
Input System:  1.19.0
Test Framework: 1.7.0
```

### Windows 확인 사항

- [x] `git config --global user.name`과 `user.email` 설정
- [x] GitHub SSH 또는 HTTPS 인증 완료
- [x] VS Code/C# 확장 또는 Visual Studio/Rider 설치
- [ ] Unity Preferences에서 외부 스크립트 편집기 확인
- [x] Blender Preferences에서 RTX 3060용 OptiX 활성화
- [x] 저장소 clone 후 `git lfs pull` 실행
- [x] Windows 장기 경로 문제가 발생하면 Git/OS long path 설정 검토
- [x] 프로젝트와 Unity 캐시는 여유 공간이 충분한 SSD에 배치
- [ ] 고부하 테스트 시 전원 모드를 성능 우선으로 설정

이 PC는 32GB RAM이므로 Unity·Blender·브라우저 동시 실행 여유가 Mac보다 크다. 다만 Blender 렌더링과 Unity 플레이 테스트를 동시에 장시간 실행할 때는 GPU VRAM과 SSD 캐시 여유를 확인한다.

### 2026-07-14 Windows 점검 결과

| 항목 | 상태 |
|---|---|
| Git / Git LFS / GitHub CLI | 설치 및 GitHub HTTPS 인증 확인 (`git 2.51.0`, `git-lfs 3.7.0`, `gh 2.92.0`) |
| Unity Hub | 설치됨 (`3.19.4`) |
| Unity Editor | 설치됨 (`6000.5.3f1`, x86_64) |
| Windows Build Support | IL2CPP 설치 확인 |
| VS Code C# 확장 | 설치됨 (`ms-dotnettools.csharp 2.140.9`) |
| Blender | 설치됨 (`5.1.2`), RTX 3060 OptiX 활성화 확인 |
| NVIDIA 드라이버 | RTX 3060과 드라이버 `610.62` 확인; Studio 채널/최신 여부는 미확인 |
| Unity 외부 스크립트 편집기 | Unity Preferences에서 GUI 확인 필요 |
| 저장 공간 | C: 드라이브 여유 약 90GiB 확인 |
| 전원 모드 | 현재 Balanced; 고부하 테스트 전 성능 우선 설정 필요 |

Unity의 `ProjectSetup.ConfigureProject` 실행이 완료되었고, 최신 프로젝트 기준 EditMode 테스트 5개가 모두 통과했다.

## 4. Unity 프로젝트 최초 생성

현재 저장소에는 기준 Mac에서 Unity 프로젝트 파일까지 생성되어 있다. 최초 생성은 한 장비에서만 수행하고, 다른 장비는 Git으로 동일한 `Assets`, `Packages`, `ProjectSettings`를 받는다.

1. 기준 장비에서 Unity Hub로 프로젝트를 생성하거나, 현재 저장소의 생성 절차를 사용한다.
2. Unity를 종료한다.
3. 임시 프로젝트의 `Assets`, `Packages`, `ProjectSettings`를 이 저장소 루트로 옮긴다.
4. 저장소 루트에서 아래 스크립트를 실행한다.

```bash
./Tools/Bootstrap/create-project-directories.sh
```

Windows에서 Git Bash가 없다면 스크립트의 디렉터리 목록을 기준으로 생성하거나, Mac에서 한 번 실행해 생성된 Unity `.meta`까지 커밋한다.

5. Unity Hub의 Add/Open으로 저장소 루트를 연다.
6. Editor Settings에서 Asset Serialization을 Force Text, Version Control을 Visible Meta Files로 설정한다.
7. 프로젝트가 오류 없이 열린 뒤 Unity를 종료한다.
8. `Assets`, `Packages`, `ProjectSettings`, `.meta`를 포함해 첫 Unity 초기화 커밋을 만든다.

최초 생성은 두 장비에서 따로 하지 않는다. 각각 생성하면 패키지 버전과 ProjectSettings가 달라질 수 있다.

## 5. 다른 장비에서 프로젝트 받기

```bash
git clone <repository-url> OpenSimCity
cd OpenSimCity
git lfs install
git lfs pull
```

그 다음 Unity Hub에서 `ProjectVersion.txt`와 같은 Editor 버전을 설치하고 저장소 루트를 연다. 처음 생성되는 `Library` 때문에 첫 실행은 오래 걸릴 수 있다.

## 6. 장비 간 이동 체크리스트

작업을 끝내는 장비:

- [ ] Unity와 Blender 파일 저장
- [ ] Unity Console 오류 확인
- [ ] 관련 테스트 실행
- [ ] `git status`에서 의도하지 않은 파일 확인
- [ ] `.meta` 누락 여부 확인
- [ ] commit과 push

작업을 시작하는 장비:

- [ ] 현재 브랜치와 미커밋 변경 확인
- [ ] `git pull --ff-only`
- [ ] `git lfs pull`
- [ ] Unity Hub에서 고정 버전으로 열기
- [ ] 패키지 해석과 컴파일 완료 후 작업 시작

## 7. 현재 Mac 점검 결과

2026-07-14 현재 이 저장소를 기준으로 확인된 상태다.

| 항목 | 상태 |
|---|---|
| Git | 설치됨 (`/usr/bin/git`) |
| Homebrew | 설치됨 |
| Xcode | 설치됨 |
| GitHub CLI | 설치 및 로그인됨 |
| Git 작성자 이름/이메일 | 설정됨 (공개 저장소에서는 GitHub 비공개 이메일 주소 사용 권장) |
| Git LFS | 설치 및 초기화됨 |
| Unity Hub | 설치됨 (`3.19.5`) |
| Unity Editor | 설치됨 (`6000.5.3f1`, arm64) |
| Unity 모듈 | Mac Standalone, WebGL 설치됨 |
| Unity 프로젝트 | 저장소 루트에서 열림 |
| URP | 활성화됨 (`17.5.0`) |
| Test Framework | 활성화됨 (`1.7.0`) |
| Input System | 활성화됨 (`1.19.0`, 새 Input System) |
| Editor 설정 | Force Text, Visible Meta Files 적용 |
| Blender | 설치됨 (`5.1.2`) |
| VS Code C# 확장 | 설치됨 (`ms-dotnettools.csharp`) |
| iOS/Windows Build Support | 아직 설치하지 않음 |

Windows PC 상태는 위의 2026-07-14 점검 결과를 기준으로 한다. Studio 드라이버 채널, Unity 외부 스크립트 편집기, 고부하 테스트용 전원 모드는 해당 환경에서 추가 확인한다.

현재 Mac에서 남은 단계는 Unity Hub에서 저장소를 Add/Open해 첫 GUI 실행을 확인하고, Blender Metal 사용 여부를 확인하는 것이다. iOS 또는 Windows 빌드가 필요해지는 시점에 해당 Build Support 모듈을 추가한다.

## 8. GitHub 저장소 운영

현재 저장소에는 문서·설정과 결정성 시뮬레이션 기반 코드·테스트가 포함되어 있다. 비밀키 패턴이나 50MB 초과 파일이 없는지 확인하고, 변경을 업로드하기 전에 작성자 정보와 Git LFS를 설정한다.

```bash
git config --global user.name "<your-name>"
git config --global user.email "<your-email>"
brew install git-lfs
git lfs install
```

저장소를 새로 만들 때는 초기에는 비공개 저장소를 권장한다. 현재 프로젝트는 이미 `hrxlou/OpenSimCity` 비공개 저장소에 연결되어 있다.

```bash
git add .
git status
git commit -m "docs: add OpenSimCity project blueprint"
gh repo create OpenSimCity --private --source=. --remote=origin --push
```

이미 빈 GitHub 저장소를 만들었다면 해당 URL을 연결한다.

```bash
git remote add origin <repository-url>
git push -u origin main
```

`Library`, `Temp`, IDE 설정 파일이 staged 목록에 나타나면 커밋하지 말고 `.gitignore` 적용 상태부터 확인한다. Unity 프로젝트 설정과 시뮬레이션 기반 코드가 변경되면 `Assets`, `Packages`, `ProjectSettings`, `.meta` 및 관련 테스트를 검토한 뒤 커밋한다.
