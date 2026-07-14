# [P2-01] 도로 상태·설치·철거 명령 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p2-01-road-commands`
- Depends on: `P1-02, P1-03, P1-05`

## Goal

셀 기반 도로 상태와 비용 검증을 구현하고 플레이어의 설치·철거 요청이 application command를 통해서만 상태를 바꾸게 한다.

## Scope

- RoadDefinition ID와 `RoadNetworkState`의 셀 점유
- 직선 drag 경로 계산, 설치/철거 command와 결과 DTO
- 비용 미리보기와 실패 사유
- EditMode 규칙 테스트

## Mac steps

- 도로 셀 상태, definition ID, 건설비를 Unity 타입 없이 모델링한다.
- drag 시작/끝에서 수평 또는 수직 경로를 결정하고 경계·중복을 검증한다.
- 명령 검증과 실제 적용을 분리해 preview가 상태를 바꾸지 않게 한다.
- 자금 부족, 도로 중복, 범위 밖, 부분 철거 테스트를 추가한다.

## Windows steps

- 동일 command fixture를 Editor에서 실행해 결과 checksum을 비교한다.
- 마우스 drag 시작/취소/완료가 command 하나로 귀결되는지 입력 로그로 확인한다.
- 도로를 빠르게 반복 설치해 Console 오류와 view 누수를 smoke 검증한다.

## Contracts and constraints

- View와 Gameplay는 도로 배열을 직접 수정하지 않는다.
- ScriptableObject에는 정적 비용·표현 key만 두고 설치 상태는 stable ID로 저장한다.
- preview와 commit은 동일한 validation 규칙을 사용한다.

## Verification

- 직선 경로·경계·중복·철거 EditMode 테스트
- preview 후 state 불변
- 명령 순서 결정성
- Windows input smoke

## Done when

- [ ] 설치·철거가 application command로만 수행된다.
- [ ] 비용과 실패 사유가 preview와 commit에서 일치한다.
- [ ] 양쪽 장비의 command fixture 결과가 같다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 곡선 도로
- undo stack
- 도로 그래프 경로 탐색
