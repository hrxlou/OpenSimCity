# [P7-03] 사건 기반 출동 차량·서비스 UI 통합

- Phase: `P7`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `feature/p7-03-dispatch-service-ui`
- Depends on: `P7-01, P7-02`

## Goal

사건이 있을 때만 출동 차량을 샘플링해 표시하고 서비스 예산/용량/범위 UI를 통합해 P7 서비스·밀도 capability를 검증한다.

## Scope

- incident/event read model and sampled dispatch route
- shared vehicle pool integration
- service budget panels, alerts, coverage maps
- PlayMode and cross-platform scenarios

## Mac steps

- 출동 view 비활성 상태에서도 service simulation checksum이 같은지 확인한다.
- service/density 전체 EditMode suite와 scenario replay를 실행한다.
- 예산 UI가 보낸 command log를 검토한다.

## Windows steps

- incident event에서만 차량을 pool에서 가져오고 종료 시 회수한다.
- 서비스 예산, capacity, alerts, overlay를 한 패널 흐름으로 통합한다.
- 차량 0/기본 표시와 scene load/unload를 profiler·PlayMode로 확인한다.

## Contracts and constraints

- 사건 vehicle은 simulation 결과를 결정하지 않는다.
- 차량마다 Update를 두지 않고 기존 중앙 scheduler를 사용한다.
- UI는 service query와 command facade만 참조한다.

## Verification

- incident 없는 상태 차량 0
- pool lifecycle·route invalidation
- budget UI command
- view on/off checksum

## Experience acceptance

- 서비스 예산을 낮추면 다음 적용 월에 capacity/coverage와 공개 원인 표시가 단계적으로 바뀐다.
- 예산 또는 시설 용량을 복구하면 같은 query 계약을 통해 coverage가 회복된다.
- 차량 표시 0/기본값에서 원인, 적용 기간, simulation 결과가 같다.

## Done when

- [ ] 출동 차량은 사건 발생 시에만 표시된다.
- [ ] 서비스 UI가 원인·용량·예산 효과를 정확히 설명한다.
- [ ] P7 서비스·밀도 capability 증거가 양쪽에서 검증된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 차량 AI
- production emergency vehicles
- 상세 사건 시뮬레이션
