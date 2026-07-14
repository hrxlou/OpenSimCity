# [P0-01] macOS·Windows 프로젝트 기준선 확정

- Phase: `P0`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `1 day`
- Branch: `chore/p0-01-cross-platform-baseline`
- Depends on: `None`

## Goal

동일한 commit을 Unity `6000.5.3f1`로 열었을 때 macOS와 Windows가 같은 패키지, 프로젝트 설정, 테스트 결과를 사용하도록 기준선을 확정한다.

## Scope

- `Packages`, `ProjectSettings`, `.gitignore`, Git LFS 상태 점검
- 각 OS의 Unity 최초 import·compile 결과와 설치 체크리스트 기록

## Mac steps

- 현재 남아 있는 URP/Graphics/Quality 설정 변경의 의도를 확인하고 하나의 기준 commit으로 정리한다.
- `git lfs version`, Unity 버전, 패키지 버전, Console 오류 유무를 기록한다.
- `./Tools/CI/run-unity-tests.sh all`을 실행하고 결과 경로를 이슈에 남긴다.

## Windows steps

- 기준 commit을 새 clone 또는 clean working tree에 받아 `git lfs pull`을 실행한다.
- Unity Hub에서 정확한 에디터 버전으로 열고 package resolve와 import가 끝날 때까지 기다린다.
- Console compile error, 누락 shader/material, line-ending으로 인한 대량 변경이 없는지 확인한다.
- EditMode와 PlayMode 샘플 테스트를 실행하고 Windows 결과를 이슈에 첨부한다.

## Contracts and constraints

- `Library`, `Temp`, `Logs`, `Obj`, `Build`, `UserSettings`는 공유하지 않는다.
- 플랫폼이 자동 생성한 `ProjectSettings` 차이는 원인을 확인하기 전 커밋하지 않는다.
- `.meta`와 원본 자산은 항상 함께 이동한다.

## Verification

- Mac과 Windows의 `git status --short` 비교
- 양쪽 Unity Console에서 compile error 0
- 양쪽 EditMode/PlayMode 샘플 테스트 통과

## Done when

- [ ] 정확한 Unity·패키지 버전이 문서와 저장소에서 일치한다.
- [ ] 양쪽에서 같은 commit이 오류 없이 열린다.
- [ ] 양쪽 테스트 결과와 환경 정보가 GitHub 이슈에 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 도시 기능 구현
- production 아트 제작
