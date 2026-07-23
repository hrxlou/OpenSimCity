# [P4-04] 신규 도시 초기 수요·첫 성장 bootstrap 구현

- Phase: `P4`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p4-04-new-city-growth-bootstrap`
- Depends on: `P4-03`

## Goal

완전히 비어 있는 신규 도시가 숨은 fixture나 직접 state 주입 없이 공개된 도로·구역·전력 명령만으로
첫 일자리·주거 건설과 첫 인구·고용까지 도달하도록 초기 수요 deadlock을 해소한다.

## Scope

- 신규 도시 전용의 결정적 초기 시장/RCI bootstrap 상태와 설정
- 빈 도시→외부 연결 도로→R/C/I 구역→전력→첫 건설→첫 인구·고용 fixture
- 외부 연결·전력 차단 reason과 정상 command 복구
- bootstrap 도중 save/load 연속성과 다음 기간 결과
- EditMode 결정성 테스트와 `CityPrototype` 공개 UI PlayMode smoke

## Mac steps

- 초기 시장 압력의 단위·상한·감쇠와 일반 월간 RCI로의 인계 규칙을 검토한다.
- 서로 다른 두 도로/구역 배치가 같은 seed와 ordered command stream에서 재현되는 fixture를 만든다.
- 신규 생성과 snapshot restore 경로를 분리해 load가 bootstrap을 다시 적용하지 않는지 검증한다.
- view/read model 관찰 on/off에서 첫 건설·인구·고용 월과 checksum이 같은지 확인한다.

## Windows steps

- 기본 `CityPrototype`의 빈 도시에서 `수요 없음`이 영구 유지되는 현재 deadlock을 PlayMode로 고정한다.
- 도로·구역·발전소의 기존 application command만 사용해 6개월 이내 첫 인구까지 도달하는 smoke를 구현한다.
- 외부 관문 미연결과 전력 미공급을 HUD/overlay에서 확인하고 정상 배치 명령 뒤 다음 기간 회복을 검증한다.
- 첫 건설 전과 진행 중에 저장→도시 변경→불러오기를 수행해 날짜·RCI·reason·site·다음 월 결과를 비교한다.

## Contracts and constraints

- bootstrap은 새로운 `CityRuntime`을 만들 때 한 번만 적용하며 snapshot load/restore에서는 재적용하지 않는다.
- 초기 압력은 기존 RCI state와 월간 smoothing을 통해 소진되는 유한한 시작 조건이다. 영구 demand floor나
  플레이 시간 기반 보너스를 두지 않는다.
- Scene·Prefab·GameObject 또는 Editor 전용 코드가 숨은 `BuildingState`, 점유 인구, 일자리를 직접 심지 않는다.
- 플레이어 도시 변경은 기존 application command와 simulation system을 통하며 UI가 배열을 직접 수정하지 않는다.
- 초기 상태와 이후 결과는 seed와 ordered command stream에만 의존하고 frame rate·배속·view 관찰 여부와 독립적이다.
- 기존 snapshot v1의 RCI·growth·clock state로 충분히 왕복하며 새 저장 필드나 version을 추가하지 않는다.

## Verification

- 빈 default runtime의 첫 양수 수요와 첫 건설
- 서로 다른 두 공개 command layout의 6개월 이내 첫 인구·고용
- 외부 연결/전력 차단 reason과 정상 command 복구
- bootstrap 전·건설 중 save/load와 이후 월별 checksum
- `CityPrototype` 공개 UI만 사용하는 PlayMode smoke

## Experience acceptance

- 새 도시에서 debugger, golden fixture, 직접 state 주입 없이 도로→구역→전력→첫 성장을 완료한다.
- 필수 조건이 빠지면 HUD/overlay가 외부 연결 또는 전력 reason과 실행 가능한 다음 행동을 표시한다.
- bootstrap 도중 저장하고 불러와도 초기 수요가 중복 적용되거나 첫 성장 timeline이 다시 시작되지 않는다.

## Done when

- [ ] 기본 `CityPrototype`에서 공개 UI만으로 6개월 이내 첫 인구와 고용이 발생한다.
- [ ] 서로 다른 두 배치가 같은 seed/commands에서 같은 첫 성장 timeline과 checksum을 만든다.
- [ ] 외부 연결·전력 차단과 정상 command 복구가 다음 기간 결과로 검증된다.
- [ ] bootstrap 전·건설 중 save/load가 초기 압력을 재적용하지 않고 다음 기간 결과를 보존한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- P9 onboarding task·튜토리얼·출시 메뉴
- 난이도·시나리오별 시작 조건
- milestone·unlock
- 첫 성장 이후의 장기 밸런스 조정
- 교통 수요·경로·차량
