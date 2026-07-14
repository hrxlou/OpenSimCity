# [P3-06] 시 재정 원장·MVP HUD 구현

- Phase: `P3`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p3-06-finance-hud`
- Depends on: `P3-03, P3-04, P3-05`

## Goal

민간 시장과 분리된 시 재정 원장, 세율, 건설비, 유지비를 계산하고 날짜·재정·인구·RCI를 HUD에 표시한다.

## Scope

- MunicipalFinanceState와 월간 ledger entries
- 세율 command, 다음 월 적용, 비용/유지비 집계
- Application query/ViewModel과 HUD
- 재정 EditMode·UI PlayMode 테스트

## Mac steps

- 원장 합계와 balance 불변식, 세율 effective month 정책을 구현·테스트한다.
- 민간 UrbanEconomyState와 시 예산을 별도 타입/시스템으로 유지한다.
- query DTO가 UI에 필요한 값과 진단 factor만 노출하는지 검토한다.
- 세율 변경의 다음 월 수요·재정 영향을 시나리오 테스트한다.

## Windows steps

- UI Toolkit 또는 프로젝트 기준 UI로 날짜, 잔액, 월 수입/지출, 인구, RCI를 배치한다.
- UI action이 application command를 생성하고 simulation을 직접 수정하지 않게 연결한다.
- 화면 비율, scale, pause/배속에서 값 갱신과 입력을 PlayMode 검증한다.
- building/road placement 비용과 실패 메시지를 HUD/툴 feedback에 연결한다.

## Contracts and constraints

- UI는 simulation 배열과 구체 renderer를 참조하지 않는다.
- 원장 entry 순서와 반올림 규칙은 결정적이다.
- HUD의 animation/refresh 빈도는 monthly simulation 결과를 바꾸지 않는다.

## Verification

- 원장 sum·세율 적용 월·유지비 테스트
- UI command wiring PlayMode test
- 다른 aspect ratio와 scale smoke
- Mac에서 HUD와 query 값 비교

## Done when

- [ ] 세율 변화가 다음 월부터 수요와 재정에 관찰 가능하게 반영된다.
- [ ] HUD가 날짜·재정·인구·RCI를 정확히 표시한다.
- [ ] 양쪽에서 재정 scenario 결과가 같다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 상세 예산 패널
- 그래프 애니메이션
- 서비스 예산
