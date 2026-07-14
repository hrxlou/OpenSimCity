# [P7-05] 특성화 UI·비교 밸런스·특성화 경험 게이트

- Phase: `P7`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p7-05-specialization-experience`
- Depends on: `P7-04`

## Goal

P7-04의 특성화 상태와 query를 플레이어용 UI에 연결하고, 일반 도시와 특성화 도시가 서로 다른 실행 가능한 운영 방식과 trade-off를 제공하는지 고정 fixture와 30분 시나리오로 검증한다.

## Scope

- 특성화 진행·병목·수익·외부효과 ViewModel과 UI
- 일반 RCI 도시와 특성화 도시 고정 fixture
- 수익·노동·오염·교통·서비스 pressure 비교 기준
- 문제→원인→수정→회복 PlayMode/smoke
- 특성화 capability/experience evidence

## Mac steps

- 일반/특성화 fixture의 seed, command log, 시작 자금과 검증 기간을 고정한다.
- 두 도시가 같은 기간에 파산하지 않고 서로 다른 수입·압력 구성을 갖는지 비교한다.
- 단일 생산 단계 반복이 다른 시스템을 무시하는 무한 수익 전략을 만들지 않는 경계 fixture를 검토한다.
- ViewModel의 reason ordering, 기간, 수익·비용 집계가 simulation query와 일치하는지 테스트한다.

## Windows steps

- 특성화 진행, 병목 원인, 수익과 외부효과를 한 패널 흐름에서 확인하게 한다.
- 30분 일반 도시와 특성화 도시를 각각 플레이해 다음 투자 선택과 회복 경로를 기록한다.
- 원료 부족, 교통 병목, 오염 누적 중 하나를 공개 UI로 확인하고 정상 command로 완화한다.
- 다른 화면 비율과 배속에서 패널 기간·reason·명령 feedback을 PlayMode 검증한다.

## Contracts and constraints

- UI는 특성화 수익·가동률·병목을 재계산하지 않고 P7-04 query/ViewModel을 표시한다.
- 특성화는 체크박스로 도시 정체성을 부여하지 않고 실제 건설·생산·거래 결과에서 파생한다.
- 일반 도시를 실패 상태로 만들거나 특성화를 유일한 흑자 전략으로 만들지 않는다.
- Balance 변경은 고정 fixture의 전후 수치와 함께 기록한다.

## Verification

- ViewModel/query와 공개 UI 일치
- 일반/특성화 30분 비교 시나리오
- 한 가지 생산 병목 완화와 다음 기간 회복
- 단일 단계 무한 수익 경계
- 양쪽 replay와 save/load 후 동일 experience state

## Experience acceptance

- 일반 RCI 중심 도시와 특성화 도시가 모두 30분 동안 실행 가능한 재정·성장 상태를 유지한다.
- 특성화 도시는 더 높은 잠재 수익과 함께 노동·오염·교통·서비스 중 하나 이상의 관찰 가능한 비용을 가진다.
- 플레이어가 생산 중단의 상위 원인을 공개 UI에서 확인하고 정상 건설·예산·도로 command로 적어도 한 병목을 완화한다.
- 30분 뒤 두 fixture의 다음 투자 선택과 주요 압력이 서로 달라 도시 정체성이 수치와 화면에 드러난다.
- 단일 생산 단계 반복만으로 다른 시스템을 무시하는 무한 수익 전략이 발견되면 gate 실패로 기록한다.

## Done when

- [ ] 특성화 progress·병목·수익·외부효과를 공개 UI에서 같은 기간 기준으로 확인한다.
- [ ] 일반 도시와 특성화 도시가 서로 다른 실행 가능한 운영 방식을 제공한다.
- [ ] 한 가지 병목의 문제→원인→수정→회복 시나리오가 양쪽에서 재현된다.
- [ ] 특성화 capability/experience evidence와 알려진 밸런스 문제가 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 두 번째 특성화 테마
- 도시 간 멀티플레이·공동 시장
- 동적 글로벌 가격 경제 전체
- 대역사급 프로젝트
- production 특성화 아트
