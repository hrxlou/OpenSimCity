# [P3-02] 발전소·연결 요소 기반 전력 공급 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p3-02-power-network`
- Depends on: `P2-02, P3-01`

## Goal

발전소 용량과 연결된 도로/전력 전달 요소를 이용해 건물의 전력 공급 여부를 결정적으로 계산한다.

## Scope

- UtilityDefinition과 power producer/state
- 연결 component별 공급·수요 배분
- dirty network 갱신과 부족 reason
- 전력 EditMode fixture와 overlay read model

## Mac steps

- 초기 전력 전달 규칙과 component 경계를 명시한다.
- 용량 부족 시 공급 우선순위를 stable ID 순서 등 결정적 규칙으로 고정한다.
- 발전소 설치/철거, 연결 분리, 과부하 테스트를 추가한다.
- 건물 성장에서 사용할 power eligibility query를 제공한다.

## Windows steps

- 발전소 placeholder와 placement command UI를 연결한다.
- 전력 공급/미공급 overlay 및 건물 상태 표시를 검증한다.
- 빠른 설치·철거 후 stale power view가 없는지 확인한다.

## Contracts and constraints

- 화면 효과나 케이블 view 수가 전력 결과를 바꾸지 않는다.
- component cache는 저장하지 않거나 재구축 가능한 상태로 구분한다.
- 발전소 정의는 정적이고 현재 생산량/예산은 runtime state다.

## Verification

- 연결·분리·용량 부족·철거 테스트
- stable allocation order
- overlay/query 일치
- 양쪽 fixture checksum

## Done when

- [ ] 도로·전력 조건이 없는 필지는 성장 자격을 얻지 못한다.
- [ ] 공급 부족 결과가 결정적이고 이유를 조회할 수 있다.
- [ ] Windows overlay가 상태를 정확히 표시한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 수도
- 예산별 발전 효율
- 송전선 세부 모델
