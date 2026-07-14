# [P9-03] Windows/macOS 출시 후보·세이브 호환 검증

- Phase: `P9`
- Primary: `Cross-platform`
- Verify: `Both`
- Estimate: `Epic; Ready 전 3개 1–3 day 이슈로 분할`
- Branch: `release/p9-03-candidate`
- Depends on: `P9-02`

## Goal

Windows와 macOS 출시 후보를 고정 절차로 만들고 저장 호환, 라이선스, 성능, 회귀, 릴리스 체크리스트를 통과한다.

## Scope

- platform build scripts/profiles and versioning
- save compatibility policy and release fixtures
- versioned built-in content catalog, definition validator, missing-content policy
- license/third-party inventory, crash/log paths
- full regression, soak, packaging checklist

## Required split before Ready

1. Windows/macOS clean build profile·versioning·제품 Scene 목록과 packaging
2. cross-platform·이전 지원 save/content catalog compatibility fixture
3. full regression·benchmark·soak·license audit와 release candidate gate

## Mac steps

- macOS release build를 clean checkout에서 생성한다.
- Windows release save와 이전 지원 버전 fixture를 load한다.
- Gatekeeper/signing/notarization 요구를 릴리스 범위에 맞게 점검한다.
- 전체 EditMode/PlayMode와 장시간 soak 결과를 기록한다.
- save fixture가 요구하는 content catalog version과 definition ID를 검증한다.

## Windows steps

- Windows release build를 clean checkout에서 생성한다.
- macOS release save와 이전 지원 버전 fixture를 load한다.
- 해상도·GPU driver·사용자 데이터 경로·clean install smoke를 수행한다.
- 전체 test, benchmark budget, 장시간 soak 결과를 기록한다.
- 누락·중복·비호환 definition fixture가 명시적 오류 또는 계약된 안전 fallback을 보이는지 확인한다.

## Contracts and constraints

- 출시 candidate는 동일 commit과 content version에서 만든다.
- save breaking change는 migration 또는 명시적 정책 없이 허용하지 않는다.
- 저장 파일은 필요한 catalog version과 definition ID를 기록하고 누락 콘텐츠를 임의의 다른 definition으로 조용히 치환하지 않는다.
- built-in catalog 경계는 향후 data-only pack을 막지 않되 P9에서 공개 mod API나 외부 pack loader 호환성을 약속하지 않는다.
- vendor asset 라이선스와 고지 파일을 확인한다.

## Verification

- clean clone build reproducibility
- Windows↔macOS save round-trip
- full tests and benchmark budgets
- 30+ minute release build soak
- license/checksum/version checklist
- content catalog validation and missing-definition fixture

## Done when

- [ ] 두 플랫폼 출시 후보가 같은 저장을 연다.
- [ ] 회귀·성능·라이선스·패키징 체크리스트가 통과한다.
- [ ] 알려진 문제, save policy, build SHA가 release 기록에 남는다.
- [ ] content catalog version과 missing-definition 정책이 release 기록과 save fixture에 남는다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 스토어 제출 자체
- 마케팅·커뮤니티 운영
- 출시 후 콘텐츠
- 공개 코드 모드 API·Workshop 통합·외부 data-only pack 로더
