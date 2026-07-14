# [P1-01] 좌표·안정 ID·결정성 기반 구현

- Phase: `P1`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `1–2 days`
- Branch: `feature/p1-01-core-determinism`
- Depends on: `P0-03`

## Goal

UnityEngine 없이 셀·청크 좌표, 안정 ID, tick/sequence, 시스템별 결정적 난수를 제공하는 Core 기반을 완성한다.

## Scope

- `Runtime/Core/Mathematics`, `Determinism`, `Time`
- 좌표 연산, 경계 없는 값 타입, 안정 순회에 필요한 비교 규칙
- EditMode 단위 테스트

## Mac steps

- `CellCoord`와 신규 `ChunkCoord`의 값 동등성, 정렬, hash 규칙을 확정한다.
- tick/sequence overflow 정책과 command stamp 순서를 구현한다.
- seed와 subsystem key에서 RNG stream을 분리하고 고정 벡터 테스트를 추가한다.
- Core asmdef가 다른 Runtime assembly와 UnityEngine을 참조하지 않는지 확인한다.

## Windows steps

- 동일 commit으로 EditMode 테스트를 실행한다.
- Mono/IL2CPP 및 플랫폼 차이에 민감한 hash, float, culture 의존 코드가 없는지 검토한다.
- Unity domain reload 후 고정 RNG 벡터가 같은지 확인한다.

## Contracts and constraints

- 결정성 경로에서 `GetHashCode()`를 영속 seed로 사용하지 않는다.
- 순회 순서는 명시적으로 정렬하거나 안정 컬렉션으로 보장한다.
- 좌표 값 타입은 presentation 좌표 변환을 포함하지 않는다.

## Verification

- 좌표 동등성·정렬·음수·경계 테스트
- 같은 seed/stream의 고정 RNG 출력 테스트
- Core assembly reference 검사

## Done when

- [ ] 동일 seed와 입력에서 양쪽 OS의 고정 벡터가 같다.
- [ ] Core 타입이 Unity presentation에 의존하지 않는다.
- [ ] 새 규칙을 다루는 EditMode 테스트가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- WorldState 저장 구조
- Unity 월드 좌표 변환
