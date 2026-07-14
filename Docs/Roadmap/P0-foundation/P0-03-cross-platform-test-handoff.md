# [P0-03] 양쪽 장비 테스트·인계 루틴 확정

- Phase: `P0`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `1 day`
- Branch: `tooling/p0-03-test-handoff`
- Depends on: `P0-01, P0-02`

## Goal

Mac 구현 → Windows 검증 또는 그 반대 흐름을 반복 가능한 명령과 증거 형식으로 고정해 P1부터 장비 전환 비용을 낮춘다.

## Scope

- Unity test runner 스크립트의 OS별 실행법
- 결과 XML·로그 위치와 이슈 코멘트 형식
- 장비 인계 및 실패 복귀 절차

## Mac steps

- 기존 shell test runner가 edit/play/all 범위를 정확히 전달하는지 확인한다.
- Unity 경로 override, 결과 XML, exit code 처리 방법을 문서화한다.
- 샘플 commit을 push하고 Windows 검증 요청 형식을 이슈에 남긴다.

## Windows steps

- PowerShell 또는 Git Bash에서 실행 가능한 명령을 확정한다.
- batchmode EditMode/PlayMode 실행 결과와 로그 위치를 확인한다.
- 실패 테스트가 non-zero exit code를 반환하는지 의도적으로 확인한 뒤 원상 복구한다.

## Contracts and constraints

- 검증 장비는 구현 commit을 수정하지 않고 먼저 그대로 재현한다.
- 실패 시 OS, commit SHA, 명령, 첫 오류를 기록한다.
- 테스트 결과물이나 Build 출력은 지정된 generated 디렉터리로 두고 커밋하지 않는다.

## Verification

- 양쪽에서 edit/play/all 중 최소 edit와 play 실행
- handoff checklist를 실제 commit 1회에 적용
- 실패 로그가 원인 추적에 충분한지 검토

## Done when

- [ ] Mac과 Windows 실행 명령이 `Docs/WORKFLOW.md` 또는 테스트 도구에 기록된다.
- [ ] 같은 commit의 양쪽 테스트 결과가 이슈에 남는다.
- [ ] P0 gate를 통과하고 P1-01을 Ready로 옮길 수 있다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 클라우드 CI 구축
- 성능 벤치마크
