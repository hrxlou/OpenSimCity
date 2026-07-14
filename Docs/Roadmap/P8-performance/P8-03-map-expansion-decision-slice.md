# [P8-03] 플레이어 맵 확장 결정·한 단계 수직 슬라이스

- Phase: `P8`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–5 days`
- Branch: `feature/p8-03-map-expansion`
- Depends on: `P4-03, P8-01, P8-02`

## Goal

128x128·256x256 성능 증거를 바탕으로 첫 출시의 고정 소형 맵 또는 인접 권역 확장 중 하나를 명시적으로 결정하고, 확장을 채택하면 64x64 시작 도시에서 인접 권역 한 개를 해금하는 수직 슬라이스를 검증한다.

## Scope

- 성능·메모리·저장 크기와 장기 목표 관점의 제품 결정 기록
- 고정 맵이면 출시 크기·경계·향후 migration 정책 명시
- 확장 맵이면 stable region ID, 잠금 상태, 비용/조건, 한 번의 인접 권역 해금
- 확장 뒤 도로·전력·교통·서비스 cache invalidation과 저장 왕복
- 잠금/확장 가능 영역 overlay와 장기 목표 UI

## Mac steps

- P8 benchmark와 P4 저장 크기를 비교해 고정 맵/확장 맵 decision record를 작성한다.
- 확장을 채택하면 비활성 권역이 시뮬레이션 tick과 후보 집합에 들어오지 않게 한다.
- 인접성, 해금 비용, command ordering, cell/region 좌표, 외부 연결 경계 규칙을 테스트한다.
- 확장 전후 저장 왕복과 road/utility/service/traffic cache 재구축을 검증한다.

## Windows steps

- 잠긴 영역, 해금 가능 영역, 비용·조건 preview와 확인 command를 연결한다.
- 확장 전후 카메라 경계·선택·건설·overlay·pool/LOD가 새 영역을 정확히 반영하는지 smoke 검증한다.
- P8 profiler fixture에서 확장 직후 spike와 안정 상태 예산을 각각 기록한다.
- 고정 맵을 선택하면 경계 UI와 출시 설명이 확장을 암시하지 않는지 검증한다.

## Contracts and constraints

- benchmark용 대형 fixture가 존재한다는 사실만으로 플레이어 맵 확장을 약속하지 않는다.
- 잠긴 권역은 인구·수요·통행·서비스 결과를 만들지 않는다.
- 확장은 application command이며 시드·명령 stream에서 재현 가능해야 한다.
- P8 예산을 통과하지 못하면 기능을 숨긴 채 남기지 않고 고정 맵 범위를 제품 계약으로 기록한다.

## Verification

- 제품 decision record와 수치 근거
- region adjacency·unlock validation·비활성 영역 불변
- 저장 왕복·cache rebuild·checksum
- 확장 직후/안정 상태 profiler와 UI smoke

## Experience acceptance

- 확장을 채택하면 플레이어가 현재 영역에서 다음 권역 해금 조건과 비용을 이해하고 장기 목표로 추적한다.
- 권역 해금 뒤 새로운 공간이 기존 도시 문제를 자동으로 삭제하지 않으며 기반 시설 연결이라는 추가 선택을 요구한다.
- 고정 맵을 선택하면 플레이어 UI와 출시 문서가 맵 경계를 명확히 설명한다.

## Done when

- [ ] 고정 맵 또는 한 단계 확장 중 하나가 benchmark 근거와 함께 명시적으로 결정된다.
- [ ] 확장 채택 시 잠긴 영역 불변·해금 command·저장·cache·UI 시나리오가 통과한다.
- [ ] 고정 맵 채택 시 크기와 경계가 제품·저장 계약에 기록된다.
- [ ] 양쪽 플랫폼에서 선택한 범위의 성능·사용성 증거를 남긴다.

## Out of scope

- 25/81타일 규모 목표
- 멀티리전·도시 간 시장
- 무제한 맵과 실시간 지형 변형
- 확장과 동시에 모든 외부 연결 자동 생성
