# [P2-05] 도로·구역 배치 UX와 P2 교차 검증

- Phase: `P2`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p2-05-placement-overlays`
- Depends on: `P2-00, P2-01, P2-02, P2-03, P2-04`

## Goal

도로·구역·필지의 preview, 실패 사유, overlay를 하나의 도구 흐름으로 통합하고 P2 gate를 양쪽 플랫폼에서 검증한다.

## Scope

- construction tool state machine
- 직선 도로와 zone brush preview·cancel·commit
- 도로 connection, 외부 접근성 band, zone, parcel overlay
- `ToolRail`, `BuildPalette`, `ContextInspector`, `DataOverlayLegend`의 P2 최소 상태
- CityPrototype PlayMode/smoke fixture

## Mac steps

- query/read model이 UI와 Presentation에 필요한 정보만 노출하는지 검토한다.
- macOS에서 트랙패드·Retina·창 resize 입력 smoke를 수행한다.
- P2 EditMode 전체와 CityPrototype PlayMode 테스트를 실행한다.

## Windows steps

- 도로/구역/철거 도구 전환, drag, 우클릭/Escape 취소를 구현한다.
- 유효/무효 preview 색과 비용·실패 사유를 표시한다.
- [디자인 기준](../../DESIGN.md)의 panel·text·accent·상태 토큰을 적용하고 focus/selected/disabled 상태를 구현한다.
- 접면 없음, 외부 연결 없음, 도달 가능하지만 먼 필지의 reason/band를 플레이어용 overlay에서 구분한다.
- overlay toggle과 카메라/selection 충돌을 검증한다.
- 대표 도시 fixture를 재현하는 Editor 메뉴 또는 테스트 setup을 만든다.

## Contracts and constraints

- preview는 simulation state를 변경하지 않는다.
- overlay는 read model/event만 소비하고 매 셀 MonoBehaviour Update를 만들지 않는다.
- P2에 구현되지 않은 성장·재정·교통·서비스 항목은 toolbar, inspector, legend에 표시하지 않는다.
- 성공·경고·실패와 접근성 band는 색 외에 icon, pattern, outline 또는 text 중 하나 이상의 대체 채널을 제공한다.
- Scene/Prefab 구성은 Editor 도구로 재현한다.

## Verification

- 취소 후 state와 재정 불변
- preview/commit 결과 일치
- 고립망과 frontage 표시
- 외부 접근성 band와 parcel query 일치
- 대표 낮/밤 월드와 창 resize에서 panel·text·overlay 판독성 screenshot
- keyboard focus, 100/125/150% UI scale과 창 resize smoke
- Windows/macOS CityPrototype smoke

## Experience acceptance

- 플레이어가 설치 전 preview에서 비용과 성공/실패를 확인하고 취소하면 도시 상태와 재정이 변하지 않는다.
- 접면 없음, 외부 연결 없음, 도달 가능하지만 먼 필지를 debug inspector 없이 구분한다.
- 도로를 연결하거나 끊으면 영향 필지의 자격 reason과 접근성 band가 같은 갱신 안에서 바뀐다.

## Done when

- [ ] 플레이어가 도로와 구역을 설치·취소·철거할 수 있다.
- [ ] 고립 도로망, 개발 불가 필지, 접근성 band를 화면에서 구분한다.
- [ ] P2 UI가 디자인 token과 고정 화면 슬롯을 사용하고 색 이외의 상태 구분을 제공한다.
- [ ] P2 capability/experience gate가 양쪽에서 통과한다.
- [ ] P2 모든 완료 조건과 양쪽 검증 증거가 이슈에 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 건물 성장
- 전력
- 최종 VFX·아트
