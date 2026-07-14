# [P1-02] 64x64 WorldState·셀·청크 저장 구조 구현

- Phase: `P1`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2 days`
- Branch: `feature/p1-02-world-grid`
- Depends on: `P1-01`

## Goal

MonoBehaviour 없이 생성·조회 가능한 64x64 WorldState와 셀/청크 인덱스를 구현하고 범위 실패를 명시적으로 처리한다.

## Scope

- `Runtime/Simulation/World`의 `WorldState`, `GridCell`, chunk index
- 초기화, 좌표↔index 변환, 범위 검사, dirty chunk 표식
- EditMode 테스트와 작은 fixture

## Mac steps

- 맵 크기와 seed를 생성자/DTO 입력으로 받고 배열을 한 번 할당한다.
- 셀 조회는 예외 남발 대신 명시적 `Try` 결과 또는 검증된 접근 API로 나눈다.
- row-major 순회와 chunk 크기 계약을 문서화한다.
- 64x64 초기화, 모서리, 잘못된 좌표, chunk 경계 테스트를 추가한다.

## Windows steps

- Unity Editor에서 64x64 fixture 생성 시 compile·GC 문제를 확인한다.
- 같은 seed로 직렬화 전 상태 checksum 또는 테스트 snapshot이 같은지 확인한다.
- EditMode 전체 suite로 assembly reference 누수를 검증한다.

## Contracts and constraints

- Simulation은 GameObject, Scene, UI, Material을 참조하지 않는다.
- 런타임 상태는 ScriptableObject에 보관하지 않는다.
- 향후 저장을 위해 상태 필드는 안정 ID와 serializable DTO로 옮길 수 있어야 한다.

## Verification

- 64x64 셀 수와 좌표 round-trip
- chunk 경계와 dirty 표식
- 범위 밖 접근 실패 결과
- 반복 초기화의 할당·상태 누수 점검

## Done when

- [ ] 모든 셀과 청크를 결정적 순서로 조회할 수 있다.
- [ ] 범위 밖 접근이 명시적으로 실패한다.
- [ ] Windows에서 같은 테스트 fixture 결과가 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 지형 높이와 바이옴
- 도로·구역 데이터
