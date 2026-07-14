# [P9-03] Windows/macOS 출시 후보·세이브 호환 검증

- Phase: `P9`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `3+ days`
- Branch: `release/p9-03-candidate`
- Depends on: `P9-02`

## Goal

Windows와 macOS 출시 후보를 고정 절차로 만들고 저장 호환, 라이선스, 성능, 회귀, 릴리스 체크리스트를 통과한다.

## Scope

- platform build scripts/profiles and versioning
- save compatibility policy and release fixtures
- license/third-party inventory, crash/log paths
- full regression, soak, packaging checklist

## Mac steps

- macOS release build를 clean checkout에서 생성한다.
- Windows release save와 이전 지원 버전 fixture를 load한다.
- Gatekeeper/signing/notarization 요구를 릴리스 범위에 맞게 점검한다.
- 전체 EditMode/PlayMode와 장시간 soak 결과를 기록한다.

## Windows steps

- Windows release build를 clean checkout에서 생성한다.
- macOS release save와 이전 지원 버전 fixture를 load한다.
- 해상도·GPU driver·사용자 데이터 경로·clean install smoke를 수행한다.
- 전체 test, benchmark budget, 장시간 soak 결과를 기록한다.

## Contracts and constraints

- 출시 candidate는 동일 commit과 content version에서 만든다.
- save breaking change는 migration 또는 명시적 정책 없이 허용하지 않는다.
- vendor asset 라이선스와 고지 파일을 확인한다.

## Verification

- clean clone build reproducibility
- Windows↔macOS save round-trip
- full tests and benchmark budgets
- 30+ minute release build soak
- license/checksum/version checklist

## Done when

- [ ] 두 플랫폼 출시 후보가 같은 저장을 연다.
- [ ] 회귀·성능·라이선스·패키징 체크리스트가 통과한다.
- [ ] 알려진 문제, save policy, build SHA가 release 기록에 남는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 스토어 제출 자체
- 마케팅·커뮤니티 운영
- 출시 후 콘텐츠
