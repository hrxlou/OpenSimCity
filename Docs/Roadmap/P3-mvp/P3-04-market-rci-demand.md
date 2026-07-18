# [P3-04] 집계 시장·RCI 수요 구현

- Phase: `P3`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2–3 days`
- Branch: `feature/p3-04-market-rci`
- Depends on: `P3-03`

## Goal

한 종류의 상품 공급·소비력·사업체 가동률과 주거/상업/산업 수요를 월별로 계산하고 평활화한다.

## Scope

- UrbanEconomyState와 MarketState
- RCI raw pressure, clamp, smoothing, diagnostic factors
- 월별 실행 순서와 EditMode 시나리오

## Mac steps

- 상품 생산·공급·소비력의 단위와 보존/손실 정책을 명시한다.
- 빈 일자리·실업·빈집·빈 점포가 각 수요에 미치는 방향을 테스트로 고정한다.
- 세율 입력 port를 만들되 실제 재정 계산과 결합하지 않는다.
- 같은 입력에서 smoothing이 플랫폼 float 차이를 만들지 않도록 수치 규칙을 정한다.

## Windows steps

- debug HUD/inspector에서 raw와 smoothed RCI 및 원인 factor를 확인한다.
- 배속별 월간 그래프가 같은 월 snapshot을 표시하는지 smoke 검증한다.
- 대표 시나리오 command log를 실행해 Mac 결과와 비교한다.

## Contracts and constraints

- RCI는 presentation 색/그래프와 분리된 simulation state다.
- 계산 순서와 반올림 위치를 명시한다.
- 수요가 있다고 즉시 무제한 개발하지 않고 P3-05 growth cap을 거친다.

## Verification

- 빈 일자리 증가·실업 증가·빈집 누적 시나리오
- 시장 공급 부족/과잉
- smoothing 경계와 clamp
- 월별 snapshot checksum

## Done when

- [x] RCI 변화 원인을 factor별로 조회할 수 있다.
- [x] 요구된 수요 방향성과 누적 억제가 테스트로 보장된다.
- [ ] 양쪽 OS 결과가 허용 오차 또는 고정 정수 규칙 안에서 일치한다.
- [x] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Numeric rules (rounding and order)

- 월별 실행 순서는 application scheduler(`MonthlySimulationScheduler`)가 소유하고, 한 달 경계에서
  등록 순서대로 population/employment → market → RCI를 실행한다. Market과 RCI는 population이
  이번 달에 반영한 건물 점유/고용 수치를 읽는다.
- 시장 보존/손실 정책: 상품은 한 달 안에서 생산·소비되고 다음 달로 재고를 이월하지 않는다.
  매칭된 흐름(`GoodsSold`)만 보존되고, 산업 잉여(`GoodsSurplus`)는 월말에 소멸, 소비자 부족
  (`GoodsShortage`)은 이월하지 않는다. 단위는 추상 goods 단위 정수다.
- RCI 계산 순서(sector별): 부호 있는 factor 기여 합 → `RawPressure`, 대칭 clamp →
  `ClampedPressure`, persistent `SmoothedDemand`를 clamp 값으로 정수 한 스텝 이동.
- 반올림 위치는 세 곳뿐이며 모두 0 방향 절삭(정수 나눗셈): (1) 세금 penalty
  `permille * sensitivity / 1000`, (2) 사업체 가동률 `throughput * 1000 / capacity`,
  (3) smoothing step `|delta| * num / den`(delta≠0이면 최소 1). float 연산이 없으므로 같은
  입력은 모든 플랫폼에서 같은 결과를 낸다.
- RCI는 presentation 색/그래프와 분리된 순수 simulation state이며 직접 개발을 유발하지 않는다.
  수요는 P3-05 growth cap을 거친다.

## Mac implementation evidence

- Date: `2026-07-18`
- OS: `macOS 25.5.0 (Darwin 25.5.0), Apple Silicon arm64`
- Unity: `6000.5.3f1`
- Branch: `feature/p3-04-market-rci`
- Commit/PR: this implementation commit; PR pending Windows verification
- Narrow test: `UrbanEconomyMarketRciTests` EditMode filter → `10/10` passed
- Narrow result/log: `TestResults/p3-04-editmode.xml`, `TestResults/p3-04-editmode.log`
- Regression test: `./Tools/CI/run-unity-tests.sh edit` → EditMode `150/150` passed
- Regression result/log: `TestResults/editmode.xml`, `TestResults/editmode.log`
- PlayMode regression: `./Tools/CI/run-unity-tests.sh play` → PlayMode `23/23` passed
- PlayMode result/log: `TestResults/playmode.xml`, `TestResults/playmode.log`
- Covered contracts: single-commodity market conservation/loss, business utilisation permille,
  demand directions (vacant jobs +R, unemployment −R, vacant housing −R, vacant shops −C,
  goods shortage +I, goods surplus −I), symmetric clamp, deterministic integer smoothing
  (monotonic, no overshoot, cumulative suppression), decoupled tax-rate port with truncating
  rounding, scheduler run order (economy after population), once-per-month across speed/pause,
  insertion-order-independent monthly snapshot checksum, empty city, DTO serialization
- Remaining verify: Windows import/compile, debug read-model inspection of raw/smoothed RCI and
  factors, speed-per-month snapshot smoke, same command-log snapshot comparison vs Mac, commit/PR
  handoff evidence

## Out of scope

- 다품목 물류
- 상세 가격 모델
- 정책 UI
