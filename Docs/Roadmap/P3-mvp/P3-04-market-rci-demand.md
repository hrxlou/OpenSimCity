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

- [ ] RCI 변화 원인을 factor별로 조회할 수 있다.
- [ ] 요구된 수요 방향성과 누적 억제가 테스트로 보장된다.
- [ ] 양쪽 OS 결과가 허용 오차 또는 고정 정수 규칙 안에서 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 다품목 물류
- 상세 가격 모델
- 정책 UI
