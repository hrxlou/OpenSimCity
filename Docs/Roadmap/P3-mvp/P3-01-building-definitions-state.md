# [P3-01] 건물 정의·런타임 상태 계약 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2 days`
- Branch: `feature/p3-01-building-contracts`
- Depends on: `P2-04`

## Goal

정적 BuildingDefinition과 저장 가능한 BuildingState를 stable ID로 분리하고 저밀도 R/C/I 성장 시스템의 공통 계약을 만든다.

## Scope

- BuildingDefinition ScriptableObject schema와 ID 검증
- BuildingState, footprint, parcel link, occupancy/job capacity
- definition catalog/query와 serialization-ready DTO 테스트

## Mac steps

- 정적 값과 런타임 값을 분류해 schema를 구현한다.
- definition ID 중복·누락과 state 참조 무결성 validator를 추가한다.
- BuildingState 생성·조회·제거와 stable ordering 테스트를 작성한다.
- Simulation이 ScriptableObject를 직접 소유하지 않도록 application catalog 경계를 둔다.

## Windows steps

- R/C/I 각 최소 definition asset을 Editor 도구로 생성한다.
- definition ID, footprint, prefab key validator를 실행한다.
- placeholder prefab이 없어도 simulation test가 통과하는지 확인한다.

## Contracts and constraints

- ScriptableObject에는 현재 점유율·전력·건설 진행도를 저장하지 않는다.
- Prefab/GameObject reference는 Simulation에 전달하지 않고 presentation key로 변환한다.
- definition ID는 이름 변경과 asset 이동에도 유지된다.

## Verification

- 중복/알 수 없는 definition ID
- BuildingState lifecycle와 ordering
- assembly dependency
- Windows asset validator

## Done when

- [ ] 정적 정의와 런타임 상태가 명확히 분리된다.
- [ ] 모든 BuildingState가 stable definition ID로 해석된다.
- [ ] 향후 저장 DTO에 Unity object가 들어가지 않는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 건물 성장
- production prefab
- 점유율 계산
