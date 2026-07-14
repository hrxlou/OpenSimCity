# [P3-07] 최소 placeholder 아트·결정성·30분 MVP 검증

- Phase: `P3`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p3-07-mvp-soak`
- Depends on: `P3-02, P3-05, P3-06`

## Goal

저밀도 R/C/I 각 3종 placeholder와 발전소·도로 표현을 통합하고 같은 command log 결정성 및 30분 플레이 안정성을 검증한다.

## Scope

- placeholder building/utility/road prefab과 material
- 중앙 scheduler·pooled building views
- 고정 seed MVP fixture와 command log replay
- 30분 soak checklist와 양쪽 PlayMode smoke

## Mac steps

- 아트 없이 replay 가능한 simulation fixture를 실행해 월별 checksum을 기록한다.
- 30분 동안 clock, 성장, 이주, 경제 불변식과 Console 오류를 확인한다.
- macOS에서 placeholder import, material, prefab reference 누락을 검증한다.

## Windows steps

- Blender production 작업 대신 primitive 기반 R/C/I 3종씩과 발전소를 만든다.
- pivot, scale, naming, definition ID/prefab key validator를 통과시킨다.
- 건물 view 생성·회수를 중앙에서 처리하고 per-building Update가 없는지 확인한다.
- 대표 도시를 30분 플레이하며 Console, 메모리 증가, 입력/UI 오류를 기록한다.

## Contracts and constraints

- placeholder는 아트 방향 확정으로 간주하지 않는다.
- 표시 building/view를 비활성화해도 simulation checksum이 같아야 한다.
- 성능 개선이나 목표 FPS 달성 주장은 profiler/benchmark 전까지 하지 않는다.

## Verification

- 동일 seed/ordered command replay의 월별 checksum
- view enabled/disabled 결과 비교
- 30분 Windows·Mac soak
- R/C/I 각 definition/prefab validator

## Done when

- [ ] 첫 피드백 루프를 30분 동안 치명적 오류 없이 플레이한다.
- [ ] 같은 seed와 command 기록으로 같은 월별 통계를 재현한다.
- [ ] P3 gate 증거와 알려진 문제 목록을 이슈에 기록한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- production low-poly 세트
- 차량·시민 표현
- 출시 수준 UX
