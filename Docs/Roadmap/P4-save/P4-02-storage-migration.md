# [P4-02] 원자적 저장소·손상 처리·migration 골격 구현

- Phase: `P4`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p4-02-storage-migration`
- Depends on: `P4-01`

## Goal

application persistence port와 파일 저장 adapter를 분리하고 임시 파일→원자적 교체, 손상/알 수 없는 버전, migration 테스트 골격을 구현한다.

## Scope

- save/load ports와 platform file adapter
- atomic replace, backup/cleanup, error result
- version dispatcher와 migration fixture
- EditMode 또는 integration tests

## Mac steps

- application layer가 경로/API 세부사항을 모르게 persistence port를 설계한다.
- write-temp, flush, replace 순서와 실패 복구를 구현한다.
- 손상 JSON/binary, truncated file, unknown version 테스트를 추가한다.
- v1→vNext fixture 형식과 golden file 관리 규칙을 만든다.

## Windows steps

- Windows 파일 잠금, 경로, replace 동작을 실제 사용자 데이터 임시 폴더에서 검증한다.
- 강제 실패/중단 뒤 temp와 기존 save가 안전한지 확인한다.
- Mac이 만든 fixture를 load하고 다시 저장한 결과를 검증한다.

## Contracts and constraints

- 저장 파일 경로는 simulation에 들어가지 않는다.
- 손상 파일을 정상 파일 위에 덮어쓰지 않는다.
- migration은 원본 bytes를 수정하지 않고 새 DTO를 만든다.

## Verification

- 정상 save/load
- write/replace 실패와 backup
- 손상·unknown version
- Mac→Windows fixture load

## Done when

- [ ] 원자적 교체와 안전한 오류 결과가 테스트된다.
- [ ] migration dispatcher와 최소 fixture가 존재한다.
- [ ] 플랫폼별 경로/잠금 차이가 application 계약을 새지 않는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- save slot UI
- 자동 저장 일정
- cloud sync
