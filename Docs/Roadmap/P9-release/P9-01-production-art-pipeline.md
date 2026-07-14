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
- R/C/I·도로·환경 production set and variants
- scale/pivot/naming/material/LOD/collider validators
- Git LFS and import presets

## Mac steps

- 승인된 export를 받아 import 결과와 material/shader 호환을 확인한다.
- macOS build에서 누락 asset, case-sensitive path, LFS pointer 문제를 검증한다.
- production asset 사용 전후 simulation checksum이 같은지 확인한다.

## Windows steps

- Blender 기준 버전과 공용 export preset/script로 원본을 제작한다.
- 1 unit=1m, bottom-center pivot, applied scale, naming 규칙을 지킨다.
- 승인 export만 `Assets/OpenSimCity/Content/Art`로 이동하고 `.meta`를 함께 커밋한다.
- atlas, LOD, simple collider를 구성하고 Benchmark Scene에서 profiler/visual QA를 수행한다.

## Contracts and constraints

- `.blend`를 Unity가 직접 runtime source로 참조하지 않는다.
- first-party는 `Assets/OpenSimCity`, vendor는 `Assets/ThirdParty`에 둔다.
- binary source/export는 Git LFS와 필요시 lock을 사용한다.

## Verification

- asset validator 0 error
- LOD/collider/pivot visual QA
- LFS pointer and fresh clone import
- Windows/macOS build smoke

## Done when

- [ ] 일관된 production set이 gameplay 계약을 바꾸지 않고 교체된다.
- [ ] 모든 에셋이 naming/pivot/scale/LOD/collider 기준을 통과한다.
- [ ] 양쪽 플랫폼에서 누락 없이 렌더링된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 라이선스 불명 vendor asset
- gameplay 규칙 변경
- 런타임 procedural art
