# [P2-05] 도로·구역 배치 UX와 P2 교차 검증

- Phase: `P2`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p2-05-placement-overlays`
- Depends on: `P2-01, P2-02, P2-03, P2-04`

## Goal

도로·구역·필지의 preview, 실패 사유, overlay를 하나의 도구 흐름으로 통합하고 P2 gate를 양쪽 플랫폼에서 검증한다.

## Scope

- construction tool state machine
- 직선 도로와 zone brush preview·cancel·commit
- 도로 connection, zone, parcel overlay
- CityPrototype PlayMode/smoke fixture

## Mac steps

- query/read model이 UI와 Presentation에 필요한 정보만 노출하는지 검토한다.
- macOS에서 트랙패드·Retina·창 resize 입력 smoke를 수행한다.
- P2 EditMode 전체와 CityPrototype PlayMode 테스트를 실행한다.

## Windows steps

- 도로/구역/철거 도구 전환, drag, 우클릭/Escape 취소를 구현한다.
- 유효/무효 preview 색과 비용·실패 사유를 표시한다.
- overlay toggle과 카메라/selection 충돌을 검증한다.
- 대표 도시 fixture를 재현하는 Editor 메뉴 또는 테스트 setup을 만든다.

## Contracts and constraints

- preview는 simulation state를 변경하지 않는다.
- overlay는 read model/event만 소비하고 매 셀 MonoBehaviour Update를 만들지 않는다.
- Scene/Prefab 구성은 Editor 도구로 재현한다.

## Verification

- 취소 후 state와 재정 불변
- preview/commit 결과 일치
- 고립망과 frontage 표시
- Windows/macOS CityPrototype smoke

## Done when

- [ ] 플레이어가 도로와 구역을 설치·취소·철거할 수 있다.
- [ ] 고립 도로망과 개발 불가 필지를 화면에서 구분한다.
- [ ] P2 모든 완료 조건과 양쪽 검증 증거가 이슈에 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 건물 성장
- 전력
- 최종 VFX·아트
