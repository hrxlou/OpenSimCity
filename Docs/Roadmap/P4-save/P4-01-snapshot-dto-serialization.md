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

## Verification

- 전체 state round-trip
- 알 수 없는 definition ID
- RNG/tick/sequence/scheduler 복원
- 금지 타입 reflection 검사

## Done when

- [ ] 성장 중인 도시 상태가 DTO round-trip 후 동일하다.
- [ ] 저장 대상과 재구축 cache 경계가 문서화된다.
- [ ] 양쪽 플랫폼에서 동일 fixture를 읽을 수 있다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 파일 I/O와 UI
- 압축·클라우드 저장
- migration 실제 정책
