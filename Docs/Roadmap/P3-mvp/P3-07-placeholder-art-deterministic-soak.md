# [P3-07] 최소 placeholder 아트·결정성·30분 MVP 검증

- Phase: `P3`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p3-07-mvp-soak`
- Depends on: `P3-02, P3-05, P3-06`

## Goal

저밀도 R/C/I 각 3종 placeholder와 발전소·도로 표현을 통합하고 같은 command log 결정성, 30분 플레이 안정성, P3 핵심 루프의 설명·정지·회복 경험을 검증한다.

## Scope

- placeholder building/utility/road prefab과 material
- 중앙 scheduler·pooled building views
- 고정 seed MVP fixture와 command log replay
- 30분 soak checklist와 양쪽 PlayMode smoke
- 전력/외부 연결 차단→원인 표시→복구→성장 재개 시나리오
- 단순 격자와 간선-가지형 도로 배치 fixture

## Mac steps

- 아트 없이 replay 가능한 simulation fixture를 실행해 월별 checksum을 기록한다.
- 30분 동안 clock, 성장, 이주, 경제 불변식과 Console 오류를 확인한다.
- 전력과 외부 연결 복구 command 뒤 정해진 다음 기간에 성장이 재개되는지 snapshot으로 확인한다.
- 두 도로 배치가 같은 규칙으로 기본 성장 루프를 완주하고 각각의 월별 상태를 기록한다.
- macOS에서 placeholder import, material, prefab reference 누락을 검증한다.

## Windows steps

- Blender production 작업 대신 primitive 기반 R/C/I 3종씩과 발전소를 만든다.
- pivot, scale, naming, definition ID/prefab key validator를 통과시킨다.
- 건물 view 생성·회수를 중앙에서 처리하고 per-building Update가 없는지 확인한다.
- 대표 도시를 30분 플레이하며 Console, 메모리 증가, 입력/UI 오류를 기록한다.
- 공개 HUD에서 성장 정지 reason을 확인하고 debug inspector 없이 정상 명령으로 복구한다.
- 30분 종료 시 플레이어가 선택할 수 있는 성장·재정·입지 action과 그 근거가 적어도 하나 표시되는지 기록한다.

## Contracts and constraints

- placeholder는 아트 방향 확정으로 간주하지 않는다.
- P3 제품 화면은 primitive 기반 placeholder를 사용한다. Downtown City MegaKit Standard는 P3-08의 Benchmark 전용 representative slice까지 사용할 수 있으며 CityPrototype prefab 교체, catalog 확장과 한국 도시화 production 통합은 P9-01 범위다.
- 표시 building/view를 비활성화해도 simulation checksum이 같아야 한다.
- 성능 개선이나 목표 FPS 달성 주장은 profiler/benchmark 전까지 하지 않는다.
- P3의 회복은 성장 정지와 재개를 뜻한다. 쇠퇴·폐건물·재개발 회복은 P6 범위다.

## Verification

- 동일 seed/ordered command replay의 월별 checksum
- view enabled/disabled 결과 비교
- 30분 Windows·Mac soak
- 전력/외부 연결 차단·복구의 reason/UI/query/다음 기간 결과 일치
- 두 도로 배치의 기본 성장 루프 완주
- R/C/I 각 definition/prefab validator

## Experience acceptance

- 전력 차단과 외부 연결 단절 각각에서 플레이어가 공개 HUD로 성장 정지 원인을 확인한다.
- 정상 placement/road command로 원인을 복구하면 정해진 다음 기간에 표시 reason이 사라지고 건설 후보가 다시 생긴다.
- 단순 격자와 간선-가지형 fixture가 모두 30분 안에 인구·일자리·세수의 첫 피드백 루프를 만든다. 동일한 성능이나 인구를 요구하지 않는다.
- 30분 종료 시 다음 행동이 전혀 설명되지 않는 dead state면 crash가 없어도 experience gate 실패로 기록한다.

## Done when

- [ ] 첫 피드백 루프를 30분 동안 치명적 오류 없이 플레이한다.
- [ ] 같은 seed와 command 기록으로 같은 월별 통계를 재현한다.
- [ ] 성장 정지 원인을 공개 UI에서 확인하고 정상 command로 복구한다.
- [ ] 서로 다른 두 도로 배치가 기본 성장 루프를 완주한다.
- [ ] 30분 뒤 적어도 하나의 설명 가능한 다음 행동이 남는다.
- [ ] P3 gate 증거와 알려진 문제 목록을 이슈에 기록한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- production low-poly 세트
- Downtown City MegaKit 전체 import, material 정리와 한국 도시화 세트
- 차량·시민 표현
- 출시 수준 UX
