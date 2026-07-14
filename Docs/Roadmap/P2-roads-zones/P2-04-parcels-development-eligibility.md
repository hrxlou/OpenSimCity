# [P2-04] 필지 생성·도로 접면·개발 자격 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p2-04-parcels`
- Depends on: `P2-02, P2-03`

## Goal

구역 셀을 도로 접면과 외부 연결 조건으로 단순 필지에 묶고 개발 후보 여부와 실패 이유를 조회할 수 있게 한다.

## Scope

- `ParcelState` stable ID, 셀 집합, frontage 정보
- zone 변경과 road graph 변경에 따른 재계산 범위
- 개발 자격 query와 실패 reason
- EditMode fixture

## Mac steps

- 초기 필지 크기/분할 규칙과 stable ID 생성 규칙을 명시한다.
- 도로 접면, zone 일치, 외부 연결 도달성을 개발 자격으로 계산한다.
- dirty 영역 밖 parcel을 재생성하지 않도록 변경 계약을 만든다.
- 고립망, 접면 없음, 교차로 주변, zone 해제 테스트를 추가한다.

## Windows steps

- parcel/debug overlay에서 경계와 frontage 방향을 확인한다.
- 도로 설치·철거와 zone 변경 후 overlay가 한 프레임/이벤트 내 갱신되는지 검증한다.
- 대표 fixture에서 표시된 자격 사유가 query와 일치하는지 확인한다.

## Contracts and constraints

- 필지는 simulation 데이터이며 GameObject instance ID를 식별자로 쓰지 않는다.
- 재계산 결과는 셀 순회 순서와 무관하게 결정적이어야 한다.
- 아직 건물 prefab 크기에 필지 규칙을 맞추지 않는다.

## Verification

- 도로 접면·외부 연결·zone 일치 조건
- dirty region 밖 stable ID 유지
- 설치/철거 후 재생성
- overlay/query 일치

## Done when

- [ ] 도로에 접하지 않거나 외부 연결이 없는 구역은 개발 후보가 아니다.
- [ ] 필지 stable ID와 dirty 갱신 규칙이 테스트로 고정된다.
- [ ] Windows overlay가 Mac fixture 결과를 정확히 표현한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 실제 성장 확률
- 복합·비정형 필지
- 건물 배치
