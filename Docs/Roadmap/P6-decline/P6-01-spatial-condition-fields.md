# [P6-01] 토지가치·오염·소음·범죄 공간 필드 구현

- Phase: `P6`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `3 days`
- Branch: `feature/p6-01-spatial-fields`
- Depends on: `P5-03`

## Goal

청크/셀 기반 토지가치, 오염, 소음, 범죄 필드와 공원 효과를 dirty 영역만 갱신하는 순수 simulation 시스템으로 구현한다.

## Scope

- spatial field storage와 source contributions
- distance/falloff rules, clamp, dirty propagation
- 용도별 선호도 factor와 field fixtures

## Mac steps

- 각 field의 단위·범위·합성 순서를 정의한다.
- source 추가/삭제와 dirty rectangle/chunk propagation을 구현한다.
- dirty 밖 값 불변, 경계 falloff, 여러 source 합성 테스트를 추가한다.
- 부동소수점 차이를 줄이는 반올림/고정 단위 정책을 정한다.

## Windows steps

- 각 field overlay gradient와 legend를 query에 연결한다.
- source 설치/철거 뒤 dirty 영역만 갱신되는지 profiler marker와 화면으로 확인한다.
- 대표 fixture의 셀 값을 Mac 결과와 비교한다.

## Contracts and constraints

- field 계산은 Material/texture resolution과 독립적이다.
- overlay는 simulation field를 수정하지 않는다.
- 성능 개선 주장은 dirty/full 비교 benchmark로만 한다.

## Verification

- source falloff·합성·clamp
- dirty 밖 불변
- 경계와 chunk crossing
- overlay 셀 값 일치

## Done when

- [ ] 네 공간 필드와 공원 효과를 조회할 수 있다.
- [ ] Dirty 영역 밖을 불필요하게 다시 계산하지 않는다.
- [ ] 양쪽 대표 fixture 값이 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 쇠퇴 상태 전이
- 최종 데이터맵 셰이더
- 서비스 거리장
