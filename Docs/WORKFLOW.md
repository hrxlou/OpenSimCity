# 개발 및 협업 방식

이 문서는 설치 이후의 일상적인 작업 방식을 정의한다. PC와 Mac의 최초 설치 및 프로젝트 연결 절차는 [SETUP.md](SETUP.md)를 따른다.

## 1. 두 기기 운영

장비별 브랜치를 만들지 않는다. 기능별 짧은 브랜치를 사용하고 작업을 옮기기 전에 커밋과 push를 완료한다.

- 데스크탑: Unity/Blender 통합, 대형 Scene, Windows 빌드, GPU 프로파일링
- MacBook: C# 로직, 문서, EditMode 테스트, 간단한 Unity 작업, macOS/iOS 빌드
- `Library`, `Temp`, `Logs`, `Obj`, `UserSettings`는 동기화하지 않는다.
- 같은 브랜치를 두 기기에서 동시에 수정하지 않는다.
- Unity Editor 버전과 패키지 lock 파일을 반드시 커밋한다.
- 현재 환경에서 Git LFS가 설치되어 있는지 `git lfs version`으로 확인한 뒤 최초 1회 `git lfs install`을 실행한다.

## 2. Git 전략

개인 또는 소규모 개발에는 `main`과 짧은 기능 브랜치만 기본으로 사용한다. 항상 유지되는 `develop` 브랜치는 통합 비용이 실제로 필요해질 때 추가한다.

```text
main
├─ feature/grid-selection
├─ feature/road-placement
├─ fix/save-migration-v2
└─ art/residential-low-set
```

규칙:

- `main`은 열리고 테스트가 통과하는 상태로 유지한다.
- 브랜치 수명은 짧게 유지하고 한 브랜치에 한 기능만 둔다.
- Unity Scene, Prefab, ScriptableObject가 겹치는 작업은 동시에 진행하지 않는다.
- 바이너리 아트 파일은 Git LFS와 필요시 LFS lock을 사용한다.
- `.meta`는 원본 자산과 항상 함께 이동·삭제·커밋한다.
- Unity에서 Asset Serialization은 Force Text, Version Control은 Visible Meta Files로 둔다.

## 3. 커밋 단위

좋은 커밋은 하나의 의도를 갖고 프로젝트가 컴파일된다.

```text
feat(grid): add cell and chunk coordinate conversion
feat(roads): build graph from placed road cells
test(demand): cover unemployment and tax penalties
art(residential): add low-density house set A
docs(architecture): record save snapshot boundary
```

자동 포맷이나 대량 `.meta` 변경은 기능 변경과 분리한다.

## 4. 기능 완료 정의

모든 기능 PR/병합 전에 확인한다.

- 요구 동작과 실패 조건이 명확하다.
- 새 핵심 규칙에 EditMode 테스트가 있다.
- Scene/Input/UI 변경에는 필요한 PlayMode 또는 smoke test가 있다.
- Unity Console에 새 warning/error가 없다.
- 정상 플레이 루프에서 불필요한 프레임별 할당을 만들지 않는다.
- 저장 대상 상태라면 save DTO와 round-trip 테스트를 갱신한다.
- 사용자에게 보이는 변경은 Prototype Scene에서 재현된다.
- 아키텍처나 작업 방식이 변했다면 문서도 함께 수정한다.

## 5. Unity 편집 규칙

- 런타임 상태를 Inspector에 흩어진 MonoBehaviour 필드로 보관하지 않는다.
- 건물·차량마다 `Update()`를 두지 않는다.
- `FindObjectOfType`, 문자열 기반 Scene 탐색, 전역 mutable singleton을 핵심 경로에서 사용하지 않는다.
- Scene/Prefab 생성은 가능한 한 `Assets/OpenSimCity/Editor` 도구로 재현한다.
- ScriptableObject는 정적 definition 용도로 제한한다.
- Addressables는 MVP 필수가 아니다. 로딩/메모리 요구가 확인된 후 도입한다.
- 최적화는 Profiler capture 또는 benchmark 숫자와 함께 제출한다.

## 6. Blender와 Unity 계약

- 단위: 1 Blender unit = 1 m = 1 Unity unit
- 축/내보내기 preset을 팀 공용 Blender script로 고정
- pivot: 건물 바닥 중심을 기본으로 하며 필지 정렬 규칙을 별도 metadata로 둔다.
- 적용 scale: `(1, 1, 1)`
- collider: 단순 box 우선
- 동일 건물군은 material/texture atlas를 공유
- `.blend`를 Unity가 직접 참조하지 않고 FBX 또는 glTF를 명시적으로 내보낸다.
- 파일 이름 예: `BLD_R_LOW_House_A_LOD0.fbx`, `VEH_Car_A.fbx`

에셋 승인 흐름:

```text
Blender/Source
  -> Blender/Export
  -> scale/pivot/naming 검증
  -> Assets/OpenSimCity/Content/Art
  -> material/prefab/LOD 구성
  -> Benchmark Scene 확인
```

## 7. 테스트 계층

### EditMode

- 그리드와 좌표
- RCI와 경제 공식
- 전력망 연결
- 도로 그래프와 경로
- 개발 조건
- 저장 직렬화와 마이그레이션

### PlayMode

- Bootstrap 및 Scene 초기화
- 셀 선택과 건설 도구
- View 생성/회수
- UI 명령 연결
- 차량 보간

### Benchmark

고정 시드 fixture로 지도 크기, 건물 수, 차량 수, 게임 속도를 바꾸며 다음을 기록한다.

- 평균/최악 frame time
- simulation tick과 monthly tick 시간
- GC allocation
- 메모리
- draw call과 GPU frame time
- 저장 크기와 로드 시간

저장소 루트에서 공통 테스트 실행 스크립트를 사용한다.

```bash
./Tools/CI/run-unity-tests.sh edit
./Tools/CI/run-unity-tests.sh play
./Tools/CI/run-unity-tests.sh all
```

다른 Unity 설치 경로를 사용할 때는 `UNITY_EDITOR` 환경 변수에 실행 파일 경로를 지정한다.

## 8. Codex 작업 요청 형식

한 요청에는 하나의 수직 기능과 명확한 수정 범위를 준다.

```text
목표: 64x64 WorldState와 셀 조회를 구현한다.

범위:
- Runtime/Core, Runtime/Simulation/World
- EditMode 테스트

조건:
- UnityEngine에 의존하지 않는 좌표 타입
- 범위 밖 조회는 명시적 실패 결과
- 프레임별 할당 없음

완료 확인:
- 관련 EditMode 테스트 실행
- 변경 파일과 테스트 결과 보고
```

Scene이나 Prefab이 필요한 경우 직접 YAML을 편집하라는 요청 대신 생성/검증 Editor 도구와 실행 절차를 함께 요청한다.

## 9. 로드맵 이슈와 장비 인계

실행 순서와 장비별 체크리스트는 [Roadmap 이슈 인덱스](Roadmap/README.md)에서 관리한다. 각 로컬 문서는 동일 ID의 GitHub 이슈와 1:1로 대응한다.

- Primary 장비에서 구현과 좁은 테스트를 마치면 commit/push하고 칸반 상태를 `Verify on Other Device`로 옮긴다.
- Verify 장비는 동일 커밋을 `pull --ff-only`로 받아 import, compile, 관련 테스트와 수동 smoke를 수행한다.
- 검증 결과에는 OS, Unity 버전, 테스트 종류, 성공/실패, 로그 또는 캡처 위치를 남긴다.
- 반대 장비에서 실패하면 원래 이슈를 다시 열어 같은 브랜치에서 수정한다. 플랫폼별 수정 이슈로 즉시 분리하지 않는다.
- 두 장비 검증이 필요 없는 문서 전용 작업은 이슈에 이유를 적고 Verify 단계를 생략할 수 있다.
