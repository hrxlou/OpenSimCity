# [P9-01] production 로우폴리 아트 파이프라인·콘텐츠 세트

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3+ days`
- Branch: `art/p9-01-production-set`
- Depends on: `P8-02`

## Goal

검증된 gameplay 계약에 맞춰 일관된 건물·도로·환경 세트, atlas, LOD, collider를 만들고 자동 validator와 Benchmark Scene을 통과한다.

## Scope

- `Blender/Source`, `Blender/Export`, first-party Art assets
- R/C/I·도로·환경 production set, 단계별 silhouette와 palette/모듈 variants
- scale/pivot/naming/material/LOD/collider validators
- Git LFS and import presets

## Mac steps

- 승인된 export를 받아 import 결과와 material/shader 호환을 확인한다.
- macOS build에서 누락 asset, case-sensitive path, LFS pointer 문제를 검증한다.
- production asset 사용 전후 simulation checksum이 같은지 확인한다.
- 같은 simulation state의 variant 선택이 플랫폼과 저장 왕복에서 안정적인지 확인한다.

## Windows steps

- Blender 기준 버전과 공용 export preset/script로 원본을 제작한다.
- 1 unit=1m, bottom-center pivot, applied scale, naming 규칙을 지킨다.
- 승인 export만 `Assets/OpenSimCity/Content/Art`로 이동하고 `.meta`를 함께 커밋한다.
- atlas, LOD, simple collider를 구성하고 Benchmark Scene에서 profiler/visual QA를 수행한다.
- 건물 성장 단계·도로 계층·지구 경계가 전체 도시 화면에서도 구분되는지 비교 캡처를 남긴다.

## Contracts and constraints

- `.blend`를 Unity가 직접 runtime source로 참조하지 않는다.
- first-party는 `Assets/OpenSimCity`, vendor는 `Assets/ThirdParty`에 둔다.
- binary source/export는 Git LFS와 필요시 lock을 사용한다.
- palette/모듈 variant는 시각적 다양성만 제공하며 수용량·세수·성장 자격을 다시 계산하지 않는다.
- 고유 에셋 수를 늘리기 전에 제한된 모듈과 palette로 서로 다른 두 도시의 정체성이 읽히는지 검증한다.

## Verification

- asset validator 0 error
- LOD/collider/pivot visual QA
- LFS pointer and fresh clone import
- Windows/macOS build smoke
- 같은 seed에서 서로 다른 운영 도시의 전체 화면·데이터맵·핵심 지표 비교

## Done when

- [ ] 일관된 production set이 gameplay 계약을 바꾸지 않고 교체된다.
- [ ] 모든 에셋이 naming/pivot/scale/LOD/collider 기준을 통과한다.
- [ ] 양쪽 플랫폼에서 누락 없이 렌더링된다.
- [ ] 도로 계층·건물 단계·지구 차이가 production 화면에서 simulation query와 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 라이선스 불명 vendor asset
- gameplay 규칙 변경
- 런타임 procedural art
- 여러 시그니처 건물과 테마 팩
