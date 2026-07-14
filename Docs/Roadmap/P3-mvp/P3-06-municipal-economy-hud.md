# [P3-06] 시 재정 원장·MVP HUD 구현

- Phase: `P3`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p3-06-finance-hud`
- Depends on: `P3-03, P3-04, P3-05`

## Goal

민간 시장과 분리된 시 재정 원장, 세율, 건설비, 유지비를 계산하고 날짜·재정·인구·RCI와 성장 가능 여부·주요 차단 원인을 플레이어용 HUD에 표시한다.

## Scope

- MunicipalFinanceState와 월간 ledger entries
- 세율 command, 다음 월 적용, 비용/유지비 집계
- Application query/ViewModel과 HUD
- `DevelopmentStatusViewModel`: 시장 압력, 개발 가능 필지 수, 상위 차단 reason, 접근성 factor, 지난 기간 주요 변화
- 재정 EditMode·UI PlayMode 테스트

## Mac steps

- 원장 합계와 balance 불변식, 세율 effective month 정책을 구현·테스트한다.
- 민간 UrbanEconomyState와 시 예산을 별도 타입/시스템으로 유지한다.
- query DTO가 UI에 필요한 값, 기간, 진단 factor만 노출하는지 검토한다.
- 시장 수요와 필지별 개발 실행 가능성을 분리하고 차단 reason을 결정적 우선순위로 집계한다.
- 지난 기간 대비 RCI·개발 가능 필지·전력/외부 연결 상태 변화의 원인을 ViewModel에 제공한다.
- 세율 변경의 다음 월 수요·재정 영향을 시나리오 테스트한다.

## Windows steps

- P2-00에서 확정한 Unity UI Toolkit 기반으로 날짜, 잔액, 월 수입/지출, 인구, RCI, 개발 상태를 배치한다.
- [디자인 기준](../../DESIGN.md)의 `GlobalHud`, `SimulationControls`, `ProblemIndicator`, `Toast/CommandFeedback`와 기본 색 토큰을 사용한다.
- 실제 font asset import 전에 한글 glyph/fallback, 숫자 정렬, 최소 크기와 font/icon 라이선스를 기록한다.
- `수요 있음/개발 가능`, `수요 있음/개발 가능 필지 없음`, `수요 없음`을 서로 다른 문구와 상태로 표시한다.
- 성장 차단 상위 3개 reason과 필지/overlay 상세 보기로 가는 진입점을 제공한다.
- UI action이 application command를 생성하고 simulation을 직접 수정하지 않게 연결한다.
- 화면 비율, scale, pause/배속에서 값 갱신과 입력을 PlayMode 검증한다.
- building/road placement 비용과 실패 메시지를 HUD/툴 feedback에 연결한다.

## Contracts and constraints

- UI는 simulation 배열과 구체 renderer를 참조하지 않는다.
- 화면별 임의 USS 값 대신 P2-00의 공통 token, PanelSettings와 focus/pointer boundary를 재사용한다.
- UI는 reason/factor를 재계산하지 않고 ViewModel의 코드·순서·기간을 표시한다.
- debug inspector만으로 보이는 factor는 experience acceptance를 충족하지 않는다.
- 원장 entry 순서와 반올림 규칙은 결정적이다.
- HUD의 animation/refresh 빈도는 monthly simulation 결과를 바꾸지 않는다.
- 날씨·행복도·교통·서비스·정책 등 P3에 없는 지표는 HUD에 placeholder나 비활성 메뉴로 노출하지 않는다.
- 반투명 UI의 대비는 실제 CityPrototype 월드 위에서 확인하며 상태를 색만으로 구분하지 않는다.

## Verification

- 원장 sum·세율 적용 월·유지비 테스트
- DevelopmentStatusViewModel reason ordering·기간·집계 테스트
- UI command wiring PlayMode test
- 수요/실행 가능성 상태와 상위 차단 reason PlayMode test
- 다른 aspect ratio와 scale smoke
- keyboard focus와 실제 game render 위 text/status contrast smoke
- Mac에서 HUD와 query 값 비교

## Experience acceptance

- RCI 수요가 높지만 전력이 없어 성장하지 않는 fixture에서 HUD가 `수요 있음/개발 가능 필지 없음`과 전력 차단 reason을 표시한다.
- 발전소 또는 연결을 복구하는 정상 command를 실행하면 다음 적용 기간에 개발 가능 필지 수와 표시 reason이 갱신된다.
- 외부 도로를 끊고 다시 연결하는 fixture에서도 이주/성장 상태와 표시 원인이 같은 기간 snapshot을 가리킨다.

## Done when

- [ ] 세율 변화가 다음 월부터 수요와 재정에 관찰 가능하게 반영된다.
- [ ] HUD가 날짜·재정·인구·RCI·개발 실행 가능성을 정확히 표시한다.
- [ ] 플레이어가 성장 차단 상위 원인과 값의 적용 기간을 debug 도구 없이 확인한다.
- [ ] 양쪽에서 재정 scenario 결과가 같다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 상세 예산 패널
- 그래프 애니메이션
- 서비스 예산
- 날씨·행복도·교통·서비스·정책 dashboard
- P9 출시 수준 onboarding과 contextual tutorial
