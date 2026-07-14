# [P8-01] 128/256 맵 벤치마크 fixture·성능 예산 확정

- Phase: `P8`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `3 days`
- Branch: `perf/p8-01-benchmark-budgets`
- Depends on: `P7-05`

## Goal

고정 seed 128x128·256x256 fixture와 CPU/GPU/GC/메모리/저장 성능 측정 절차를 만들고 플랫폼별 목표 예산을 기록한다.

## Scope

- Benchmark Scene/fixture generator
- simulation tick, monthly tick, frame, GC, memory, draw call markers
- selection, placement preview, command result, HUD/query refresh 응답 측정
- Mac/Windows hardware-specific baseline docs

## Mac steps

- headless 또는 view-off simulation tick과 save size/load time baseline을 측정한다.
- macOS 하드웨어·해상도·quality 설정과 결과를 별도 표로 기록한다.
- fixture checksum으로 Windows와 논리 상태가 같은지 확인한다.

## Windows steps

- 건물 2,000개·표시 차량 500대 fixture를 재현한다.
- Profiler capture로 CPU/GPU frame, GC, draw calls, memory를 기록한다.
- 256x256 stress run에서 실패 지점과 bottleneck marker를 분류한다.
- 목표 fixture에서 입력→preview와 command→공개 UI 갱신 시간을 반복 측정한다.

## Contracts and constraints

- 서로 다른 하드웨어 숫자를 직접 평균내지 않는다.
- 측정 전 warm-up, sample duration, quality, resolution을 고정한다.
- 최적화 전 baseline과 동일 fixture/checksum을 보존한다.

## Verification

- fixture 재생성 checksum
- 3회 반복 측정 분산
- view-off/on 분리
- save size/load time
- 입력/명령 feedback latency

## Experience acceptance

- 목표 fixture에서 selection과 placement preview가 정한 응답 예산 안에 표시된다.
- 도시 명령 뒤 공개 HUD/reason이 해당 simulation 기간과 UI refresh 예산 안에 갱신된다.
- stress fixture가 예산을 넘으면 기능 실패와 구분해 한계 및 완화 설정을 기록한다.

## Done when

- [ ] 플랫폼별 재현 가능한 baseline과 목표 예산이 문서화된다.
- [ ] 병목이 profiler marker와 수치로 특정된다.
- [ ] 후속 최적화 우선순위가 근거와 함께 정해진다.
- [ ] 문제 식별과 명령 feedback의 응답 예산이 플랫폼별로 정의된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 근거 없는 Jobs/Burst 도입
- production graphics tuning
- 출시 hardware matrix
