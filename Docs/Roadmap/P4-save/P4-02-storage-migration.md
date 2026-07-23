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

### Implemented contract

- `CityPersistenceService`는 ASCII save key와 bytes 기반 `ICitySaveStorage`,
  `ICitySnapshotPayloadCodec` 포트만 소비한다. 경로·확장자·파일 API·Unity JSON은
  `OpenSimCity.Platform` adapter 안에 남는다.
- 저장은 destination과 같은 디렉터리의 `.tmp`에 write→disk flush한 뒤 첫 저장은 move,
  기존 저장은 replace하고 직전 파일을 `.bak`으로 보존한다. write/flush/replace 실패는
  기존 destination을 보존하고 temp cleanup을 시도한 뒤 공통 error result를 반환한다.
- codec output은 저장소 호출 전에 version probe와 decode를 다시 통과한다. 손상 binary,
  truncated JSON, unknown version, 잠금과 I/O 실패는 예외나 경로를 Application 계약으로
  노출하지 않는다.
- dispatcher는 명시적으로 등록된 forward step만 실행하고 caller bytes의 clone을 전달한다.
  v1→vNext fixture migration은 source DTO와 별개의 target DTO를 만들며 source bytes 불변,
  golden bytes와 target header를 검증한다. 제품 snapshot version은 P4-01의 v1을 유지한다.
- fixture는 `Assets/OpenSimCity/Tests/Fixtures/Persistence`의 UTF-8(BOM 없음)/LF immutable
  source·target pair로 관리한다. 새 migration은 기존 파일을 수정하지 않고 pair를 추가한다.

## Verification

- 정상 save/load
- write/replace 실패와 backup
- 손상·unknown version
- Mac→Windows fixture load

## Done when

- [x] 원자적 교체와 안전한 오류 결과가 테스트된다.
- [x] migration dispatcher와 최소 fixture가 존재한다.
- [x] 플랫폼별 경로/잠금 차이가 application 계약을 새지 않는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Windows implementation evidence (2026-07-23)

- OS: Windows 11 Enterprise `10.0.26200` build `26200` (`x64`)
- Branch: `codex/p4-02-storage-migration`
- Unity: `6000.5.3f1` (`c2eb47b3a2a9`)
- ProjectSettings index baseline guard: passed
- P4-02 EditMode fixture: `11/11` passed
- Final full regression:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 all`
  → EditMode `191/191`, PlayMode `32/32` passed
- 실제 Unity user temporary cache 아래에서 정상 save/load, Windows exclusive lock,
  기존 파일 replace와 backup, write/flush/replace 강제 실패 뒤 destination 보존과 temp cleanup을
  검증했다.
- 저장 round-trip fixture: UTF-8 `2,884 bytes`, FNV-1a `17862EDE36A0B76D`
- migration source: `118 bytes`, FNV-1a `B30596797608932D`
- migration target: `142 bytes`, FNV-1a `0C8655DB08CDA28C`

## Remaining verification / blocked

- Primary인 macOS에서 같은 commit과 Unity `6000.5.3f1`로 EditMode와 fixture checksum을 재현하고,
  macOS save fixture를 Windows에서 load→save해 동일한 결과인지 확인해야 한다.
- commit과 PR URL은 publish 후 이 문서와 GitHub 이슈에 기록한다.
- save slot UI, 자동 저장 일정, cloud sync는 P4-03 또는 명시된 후속 범위이며 구현하지 않았다.

## Out of scope

- save slot UI
- 자동 저장 일정
- cloud sync
