# [P2-04] 필지 생성·도로 접면·개발 자격 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p2-04-parcels`
- Depends on: `P2-02, P2-03`

## Goal

구역 셀을 도로 접면과 외부 연결 조건으로 단순 필지에 묶고 개발 후보 여부, 실패 이유, 외부 접근성 band를 조회할 수 있게 한다.

## Scope

- `ParcelState` stable ID, 셀 집합, frontage 정보
- zone 변경과 road graph 변경에 따른 재계산 범위
- 개발 자격 query, 실패 reason, 외부 접근성 거리/band
- EditMode fixture

## Mac steps

- 초기 필지 크기/분할 규칙과 stable ID 생성 규칙을 명시한다.
- 도로 접면, zone 일치, 외부 연결 도달성을 개발 자격으로 계산한다.
- 도달 가능한 필지는 frontage에서 외부 gate까지의 최소 정수 거리와 band를 진단 factor로 보존한다.
- 거리 차이는 이 단계에서 자격을 추가로 박탈하지 않고 P3 용도별 입지 factor가 소비하게 한다.
- dirty 영역 밖 parcel을 재생성하지 않도록 변경 계약을 만든다.
- 고립망, 접면 없음, 교차로 주변, zone 해제 테스트를 추가한다.

## Windows steps

- parcel/debug overlay에서 경계, frontage 방향, 접근성 band를 확인한다.
- 도로 설치·철거와 zone 변경 후 overlay가 한 프레임/이벤트 내 갱신되는지 검증한다.
- 대표 fixture에서 표시된 자격 사유가 query와 일치하는지 확인한다.

## Contracts and constraints

- 필지는 simulation 데이터이며 GameObject instance ID를 식별자로 쓰지 않는다.
- 재계산 결과는 셀 순회 순서와 무관하게 결정적이어야 한다.
- 외부 접근성은 정수 query이며 Presentation이 world-space 거리를 다시 계산하지 않는다.
- 아직 건물 prefab 크기에 필지 규칙을 맞추지 않는다.

## Verification

- 도로 접면·외부 연결·zone 일치 조건
- 같은 component 안의 서로 다른 접근성 거리와 여러 frontage 중 최소값
- dirty region 밖 stable ID 유지
- 설치/철거 후 재생성
- overlay/query 일치

## Experience acceptance

- 플레이어가 접면 없음, 외부 연결 없음, 도달 가능하지만 먼 필지를 서로 다른 공개 reason/band로 구분한다.
- 도로 연결을 수정하면 필지의 자격과 접근성 표시가 같은 갱신 안에서 일치한다.

## Done when

- [x] 도로에 접하지 않거나 외부 연결이 없는 구역은 개발 후보가 아니다.
- [x] 도달 가능한 필지는 외부 접근성 거리와 band를 조회할 수 있다.
- [x] 필지 stable ID와 dirty 갱신 규칙이 테스트로 고정된다.
- [ ] Windows overlay가 Mac fixture 결과를 정확히 표현한다.
- [x] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Mac implementation evidence

- Date: `2026-07-16`
- OS: `macOS 26.5.2 (25F84), Apple Silicon arm64`
- Unity: `6000.5.3f1 (c2eb47b3a2a9)`
- Stacked branch: `feature/p2-03-zoning` (`b9b4514`) → `feature/p2-04-parcels`
- Commit/PR: this implementation commit on the local stacked branch; PR and Windows handoff pending
- Test: `./Tools/CI/run-unity-tests.sh all` → EditMode `95/95`, PlayMode `16/16` passed
- Result XML/log: `TestResults/editmode.xml`, `TestResults/editmode.log`,
  `TestResults/playmode.xml`, `TestResults/playmode.log` (local generated artifacts)
- Covered fixtures: deterministic `1x1~2x2` same-zone partition, distinct-zone split, no-frontage and
  disconnected-frontage reasons, multiple-frontage minimum distance and `Far` band, intersection
  frontage directions, road split/reconnect refresh, zone clear, stable ID/object reuse outside the
  exact dirty set, and Application query parity
- Remaining verify: P2-05 parcel/debug overlay boundary, frontage direction, band/reason parity and
  Windows road/zone refresh smoke

## Out of scope

- 실제 성장 확률
- 복합·비정형 필지
- 건물 배치
