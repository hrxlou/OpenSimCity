# [P4-01] 버전형 도시 snapshot DTO·직렬화 구현

- Phase: `P4`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p4-01-snapshot-dto`
- Depends on: `P3-07`

## Goal

Unity presentation 객체를 포함하지 않는 버전형 전체 도시 snapshot을 정의하고 runtime state와 round-trip 변환을 구현한다.

## Scope

- snapshot header/version, world·road·zone·parcel·building·economy 상태 DTO
- RNG state, current tick/sequence, scheduler next-run 저장
- definition ID validation과 round-trip/EditMode fixture

## Mac steps

- 각 runtime state의 저장 필드와 재구축 가능한 cache를 분류한다.
- DTO field order, numeric representation, version 정책을 문서화한다.
- growing city fixture의 state→DTO→state deep comparison을 구현한다.
- GameObject, View, Material, ScriptableObject reference가 DTO에 없는지 reflection validator를 추가한다.

## Windows steps

- 대표 도시를 Editor에서 snapshot으로 추출하고 크기·누락 필드를 확인한다.
- domain reload 뒤 restore fixture가 compile·reference 오류 없이 동작하는지 검증한다.
- Windows serialization 결과를 Mac에서 읽는 교차 fixture를 준비한다.

## Contracts and constraints

- DTO는 stable ID와 serializable scalar/collection만 가진다.
- cache와 presentation instance는 저장하지 않고 load 후 재구축한다.
- 버전 추가는 기존 필드 의미를 조용히 변경하지 않는다.

### Implemented contract

- `CitySnapshotDto` v1은 header/world/timeline 뒤에 sparse road·zone, parcel identity/topology,
  external connection, building, power producer, growth, population/employment/migration,
  market/RCI, municipal finance를 고정 field order로 둔다.
- stable ID·sequence·RNG·seed는 `ulong`, tick/UTC tick은 `long`, 좌표·cell index·수량·
  permille·enum code는 `int`, clock delta/accumulator만 `double`이다. 배열은 row-major 또는
  stable ID/월 오름차순이다.
- 저장 상태는 진행 중 공사비와 예약 세율, growth RNG state, clock accumulator/speed,
  current tick, next command sequence, scheduler next month/next run tick까지 포함한다.
- road graph/component/distance, parcel frontage/accessibility/dirty/version, power allocation,
  read model과 Unity presentation reference는 저장하지 않는다. parcel DTO의 ID/zone/anchor/크기는
  load 후 재구축 결과를 검증하는 topology 계약이며 frontage cache는 포함하지 않는다.
- loader는 정확한 `FormatVersion=1`만 받고 migration을 실행하지 않는다. snapshot의 road/zone/
  building/utility definition ID와 building/parcel/aggregate reference를 상태 적용 전에 검증한다.
- `SnapshotContractValidator`는 reflection으로 scalar/string/DTO/DTO array만 허용하고
  Presentation/Gameplay/UI/Editor/Definitions 계층과 View/Unity object 계열을 거부한다.
- `CitySnapshotSerializer`는 DTO capture/restore만 담당한다. JSON/file I/O, 압축, cloud와 실제
  migration 정책은 후속 이슈 범위다.

## Verification

- 전체 state round-trip
- 알 수 없는 definition ID
- RNG/tick/sequence/scheduler 복원
- 금지 타입 reflection 검사

## Done when

- [x] 성장 중인 도시 상태가 DTO round-trip 후 동일하다.
- [x] 저장 대상과 재구축 cache 경계가 문서화된다.
- [ ] 양쪽 플랫폼에서 동일 fixture를 읽을 수 있다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Windows implementation evidence (2026-07-23)

- OS: Windows 11 Enterprise `10.0.26200` (`x64`)
- Unity: `6000.5.3f1` (`c2eb47b3a2a9`)
- Branch: `codex/p4-01-snapshot-dto-serialization`
- 성장 도시 canonical JSON: UTF-8 `7,444 bytes`, FNV-1a `262B9801635F35BD`
- EditMode:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 edit`
  → `180/180` passed
- 최종 전체 회귀:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 all`
  → EditMode `180/180`, PlayMode `32/32` passed
- fixture는 state→DTO→JSON→DTO→state의 deep JSON equality, rebuild된 road/parcel/power 결과,
  RNG/tick/sequence/scheduler와 이후 12 tick 연속 결과, unknown definition/version 거부,
  금지 타입 reflection 검사를 포함한다.

## Remaining verification / blocked

- macOS에서 같은 commit과 Unity `6000.5.3f1`로 canonical fixture checksum
  `262B9801635F35BD` 및 EditMode 전체를 재현해야 양쪽 플랫폼 완료 조건을 체크할 수 있다.
- domain reload 뒤 restore의 compile/reference 검증은 batchmode의 fresh script reload와
  EditMode restore fixture로 확인했다. 실제 Editor UI에서 수동 domain reload 후 관찰은 남아 있다.
- 파일 I/O/UI/migration은 P4-02/P4-03 범위이므로 이 이슈에서 구현하지 않았다.

## Out of scope

- 파일 I/O와 UI
- 압축·클라우드 저장
- migration 실제 정책
