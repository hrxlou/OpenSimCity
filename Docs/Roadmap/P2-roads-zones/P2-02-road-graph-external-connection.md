# [P2-02] 도로 그래프·외부 연결·도달성 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p2-02-road-connectivity`
- Depends on: `P2-01`

## Goal

교차로·막다른 길·철거를 반영하는 도로 연결성과 지도 경계의 외부 연결 도달성·결정적 네트워크 거리를 계산한다.

## Scope

- 도로 인접 그래프 또는 셀 연결 표현
- graph version과 dirty region
- `ExternalConnectionState`, 도달 가능성 query, `NetworkDistanceToExternalGate`
- 정수 거리 band와 unreachable reason
- 연결성 EditMode fixture

## Mac steps

- 도로 변경 집합에서 graph version을 한 번 증가시키는 규칙을 정한다.
- 교차로, 막다른 길, 분리·재연결, 경계 gate의 도달성을 구현한다.
- 고정 edge cost와 안정적인 queue ordering으로 각 도로 셀의 가장 가까운 외부 gate까지 정수 거리를 계산한다.
- 도달 불가 sentinel, 같은 거리 tie, 여러 gate, graph 변경 뒤 거리 무효화 규칙을 테스트한다.
- 안정 순회와 queue ordering을 명시해 플랫폼별 탐색 결과 차이를 막는다.
- 작은 fixture로 연결 component와 external reachable 결과를 검증한다.

## Windows steps

- 디버그 overlay에서 connection mask, 외부 연결 component, 접근성 거리 band를 색으로 확인한다.
- 설치/철거 반복 후 stale edge나 잘못된 dirty 영역이 없는지 smoke 검증한다.
- 동일 fixture의 component ID와 거리 band를 Mac 결과와 비교한다.

## Contracts and constraints

- component의 내부 번호가 저장 계약이 되지 않도록 stable entity ID와 구분한다.
- Presentation은 graph를 수정하지 않고 read model만 사용한다.
- 네트워크 거리는 P3의 입지 근사이며 OD 통행, 혼잡, 차선, 실제 통근시간을 의미하지 않는다.
- 거리 단위와 unreachable sentinel은 공개 query 계약으로 고정한다. 거리 cache는 road state에서 재구축할 수 있으며 저장 필수 상태로 만들지 않는다.
- 성능 개선 주장은 benchmark 전까지 하지 않는다.

## Verification

- 교차로·막다른 길·고립망·경계 연결 테스트
- 철거 후 component split
- 여러 gate·동일 거리 tie·unreachable 거리 fixture
- graph version·dirty region·거리 무효화 정확성
- 양쪽 checksum 비교

## Experience acceptance

- 연결된 도로, 먼 도로, 고립 도로를 overlay의 서로 다른 접근성 band와 reason으로 구분한다.
- 도로를 연결하거나 끊으면 영향 영역의 band만 다음 graph 갱신에 바뀐다.
- 이 단계에서는 거리 차이가 성장 결과를 바꾸지 않아도 된다. P3-05가 이 query를 소비한다.

## Done when

- [x] 고립망과 외부 연결망을 정확히 구분한다.
- [x] 가장 가까운 외부 gate까지의 정수 거리와 band가 결정적으로 계산된다.
- [x] 그래프 변경 시 version과 dirty 영역이 정확히 갱신된다.
- [x] 외부 연결 query가 후속 이주·시장 시스템에서 사용할 수 있다.
- [ ] 접근성 overlay와 query가 같은 값을 표시한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Mac implementation evidence

- Date: `2026-07-16`
- OS: `macOS 26.5.2 (25F84), Apple Silicon arm64`
- Unity: `6000.5.3f1 (c2eb47b3a2a9)`
- Stacked branch: `feature/p2-01-road-commands` → `feature/p2-02-road-connectivity`
- Commit/PR: this implementation commit on the local stacked branch; PR and Windows handoff pending
- Test: `./Tools/CI/run-unity-tests.sh all` → EditMode `78/78`, PlayMode `16/16` passed
- Result XML: `TestResults/editmode.xml`, `TestResults/playmode.xml` (local generated artifacts)
- Connectivity fixture checksum: `0x437e8c870287f6db`
- Covered fixtures: intersection/dead end, isolated component, boundary gate, split/reconnect,
  multi-gate tie, distance bands, invalid gates, graph version, exact dirty cells, Application
  read-model parity
- Remaining verify: P2-05 actual overlay consumption and the Windows steps/checksum comparison

## Out of scope

- 차량 경로 탐색
- OD 통행과 혼잡
- 차선·신호
- 교통량
