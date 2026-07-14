# [P1-05] 셀 선택·하이라이트·디버그 그리드 완성

- Phase: `P1`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p1-05-grid-selection`
- Depends on: `P1-02, P1-03, P1-04`

## Goal

카메라 각도와 화면 비율에 관계없이 64x64 모든 셀을 정확히 선택하고 선택 결과를 디버그 그리드와 하이라이트로 표시한다.

## Scope

- 화면 ray→월드 평면→`CellCoord` 변환
- 기본 tool state machine과 selection query
- pooled 또는 단일 highlight view, debug grid renderer
- PlayMode 정확도·Scene smoke 테스트

## Mac steps

- 좌표 변환의 순수 계산 부분과 경계 테스트를 EditMode로 보강한다.
- Retina scaling과 창 크기 변화에서 corner cell 선택을 smoke 검증한다.
- Core/Simulation이 presentation 타입을 참조하지 않는지 dependency를 확인한다.

## Windows steps

- CityPrototype Scene에서 debug grid와 highlight view를 생성 도구로 연결한다.
- 회전 0/90/180/270도, 줌 최소/최대, 화면 모서리에서 셀 선택을 확인한다.
- UI 위 click과 camera drag 중 선택이 발생하지 않게 tool state를 구현한다.
- PlayMode smoke에서 대표 좌표와 화면 비율을 자동 검증한다.

## Contracts and constraints

- 선택은 조회 상태이며 simulation 배열을 직접 수정하지 않는다.
- 셀 변경 시 highlight를 새 GameObject로 계속 생성하지 않는다.
- 그리드 렌더링 방식은 simulation 결과에 영향을 주지 않는다.

## Verification

- 네 카메라 회전에서 모서리·중앙 셀 선택
- UI pointer 차단과 drag conflict
- Bootstrap/CityPrototype smoke
- 양쪽 OS에서 대표 좌표 일치

## Experience acceptance

- 네 카메라 회전과 최소/최대 줌에서 같은 화면 지점을 반복 선택하면 예측 가능한 같은 셀을 가리킨다.
- UI 위 click과 camera drag가 도시 셀 선택으로 오인되지 않는다.

## Done when

- [ ] 사용자가 64x64 모든 셀을 정확히 선택할 수 있다.
- [ ] 카메라 조작과 선택이 충돌하지 않는다.
- [ ] P1 experience acceptance가 양쪽 입력 환경에서 통과한다.
- [ ] P1 관련 EditMode/PlayMode 테스트가 양쪽에서 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 도로 드래그 배치
- 최종 아트 그리드
