# [P2-02] 도로 그래프·외부 연결·도달성 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p2-02-road-connectivity`
- Depends on: `P2-01`

## Goal

교차로·막다른 길·철거를 반영하는 도로 연결성과 지도 경계의 외부 연결 도달성을 결정적으로 계산한다.

## Scope

- 도로 인접 그래프 또는 셀 연결 표현
- graph version과 dirty region
- `ExternalConnectionState`와 도달 가능성 query
- 연결성 EditMode fixture

## Mac steps

- 도로 변경 집합에서 graph version을 한 번 증가시키는 규칙을 정한다.
- 교차로, 막다른 길, 분리·재연결, 경계 gate의 도달성을 구현한다.
- 안정 순회와 queue ordering을 명시해 플랫폼별 탐색 결과 차이를 막는다.
- 작은 fixture로 연결 component와 external reachable 결과를 검증한다.

## Windows steps

- 디버그 overlay에서 connection mask와 외부 연결 component를 색으로 확인한다.
- 설치/철거 반복 후 stale edge나 잘못된 dirty 영역이 없는지 smoke 검증한다.
- 동일 fixture의 component ID를 Mac 결과와 비교한다.

## Contracts and constraints

- component의 내부 번호가 저장 계약이 되지 않도록 stable entity ID와 구분한다.
- Presentation은 graph를 수정하지 않고 read model만 사용한다.
- 성능 개선 주장은 benchmark 전까지 하지 않는다.

## Verification

- 교차로·막다른 길·고립망·경계 연결 테스트
- 철거 후 component split
- graph version·dirty region 정확성
- 양쪽 checksum 비교

## Done when

- [ ] 고립망과 외부 연결망을 정확히 구분한다.
- [ ] 그래프 변경 시 version과 dirty 영역이 정확히 갱신된다.
- [ ] 외부 연결 query가 후속 이주·시장 시스템에서 사용할 수 있다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 차량 경로 탐색
- 차선·신호
- 교통량
