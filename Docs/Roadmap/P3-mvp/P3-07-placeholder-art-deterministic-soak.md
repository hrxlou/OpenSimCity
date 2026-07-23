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

### Implemented contract

- `BuildingDefinition`은 R/C/I 각각 stable ID와 presentation key가 다른 A/B/C 세 종류를
  제공한다. `PlaceholderArtSetup`이 건물 9종, 건설 중, 발전소, 도로 prefab과 공용 material,
  `PlaceholderArtCatalog`를 primitive로 재생성하고 definition ID/key/prefab/pivot/scale/material
  validator가 참조를 확인한다.
- `BuildingViewReadModel`은 Unity object 없는 value snapshot만 제공한다.
  `BuildingViewRenderer`, `RoadViewRenderer`, `PowerProducerViewRenderer`가 중앙에서 version을
  관찰하고 view를 pool로 생성·회수한다. 개별 prefab에는 `MonoBehaviour`가 없다.
- `MvpSoakFixture`는 seed `0x307`, ordered command log, 단순 격자/간선-가지형 layout을 기본
  clock 18,000 tick(60개월, simulation 30분) 재생하고 매월 P3 state checksum을 기록한다.
  presentation snapshot 관찰 on/off replay는 같은 checksum을 만든다.
- 발전소 철거/재배치와 external gate road 철거/재배치는 command log에 포함된다. 외부 연결
  판정은 enabled gate만이 아니라 실제 reachable road를 요구해 HUD/성장/이주 snapshot 기간을
  일치시킨다.

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

- [x] 첫 피드백 루프를 simulation 30분 동안 치명적 오류 없이 가속 Player soak한다.
- [x] 같은 seed와 command 기록으로 같은 월별 통계를 재현한다.
- [x] 성장 정지 원인을 공개 UI에서 확인하고 정상 command로 복구한다.
- [x] 서로 다른 두 도로 배치가 기본 성장 루프를 완주한다.
- [x] 30분 뒤 적어도 하나의 설명 가능한 다음 행동이 남는다.
- [x] P3 gate 증거와 알려진 문제 목록을 이슈에 기록한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Windows implementation evidence (2026-07-23)

- OS: Windows 11 Enterprise `10.0.26200` (`x64`)
- Unity: `6000.5.3f1` (`c2eb47b3a2a9`)
- Branch: `feature/p3-07-mvp-soak`
- EditMode:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 edit`
  → `172/172` passed
- PlayMode:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 play`
  → `30/30` passed
- 최종 전체 회귀:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CI\run-unity-tests.ps1 all`
  → EditMode `172/172`, PlayMode `30/30` passed
- 단순 격자 60개월 final checksum: `B4E6E15547236FCE`
- 간선-가지형 60개월 final checksum: `726257FF395A38BB`
- 두 layout의 60개 월별 checksum 전체는 `TestResults/editmode.xml`의
  `ThirtyMinuteReplayProducesIdenticalMonthlyChecksums` output에 기록된다.
- 가속 Player soak: 두 layout 모두 첫 인구·고용·세수 feedback과 설명 가능한 다음 action을
  만들고 fatal log `0`; Unity total allocated memory 관측은 두 run 합산 전후
  `435,552,507 → 435,957,963` bytes (`+405,456`)였다. 이는 profiler benchmark나 성능
  개선 근거가 아니다.
- CityPrototype PlayMode는 실제 prefab reference, 중앙 road/building/power view 생성·회수,
  공개 HUD의 외부 도로 단절 문구와 road placement tool 복구를 검증했다.

## Remaining verification / known issues

- macOS Verify 장비에서 동일 commit의 import/compile, EditMode/PlayMode, 두 layout의 60개 월별
  checksum과 placeholder material/prefab reference를 비교해야 한다.
- Windows의 30분 wall-clock 수동 Editor 플레이, 입력/UI 반복 조작, 실제 game render 위
  contrast·한글 fallback·100/125/150% OS scale capture는 자동 가속 soak와 별도로 남아 있다.
- 메모리 수치는 PlayMode 전후 관측치일 뿐 leak 판정이나 성능 budget 증거가 아니다. P3-07은
  FPS/성능 개선을 주장하지 않는다.
- Commit/PR과 GitHub 이슈 링크는 publish 뒤 이 기록과 GitHub 이슈에 추가한다.

## Out of scope

- production low-poly 세트
- Downtown City MegaKit 전체 import, material 정리와 한국 도시화 세트
- 차량·시민 표현
- 출시 수준 UX
