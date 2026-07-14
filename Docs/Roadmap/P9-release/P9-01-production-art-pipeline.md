# [P9-01] production 아트 catalog 확장·한국 도시화·polish

- Phase: `P9`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `Epic; 실행 전 1–3 day 하위 이슈로 분할`
- Branch: `art/p9-01-production-set`
- Depends on: `P3-08, P8-03`

## Goal

P3-08에서 검증한 representative pipeline과 P8 성능 결정을 바탕으로 한국 도시 인상이 읽히는 production catalog를 필요한 gameplay definition 범위로 확장하고 출시 수준으로 다듬는다.

## Scope

- `Blender/Source/{External,OpenSimCity}`, `Blender/Scripts`, `Blender/Export`, first-party Art assets
- `Artifacts/Art/<batch>` 임시 작업·validator report와 승인 산출물 분리
- R/C/I·도로·환경 production set, 단계별 silhouette와 palette/모듈 variants
- P3-08 acquisition manifest·선별 import·wrapper pipeline의 확장과 감사
- texture relink, scale/axis/pivot/footprint, stable ID, material/renderer와 선택적 LOD/collider validators
- 한글 간판·옥상 설비·1층 입면·가로 소품을 포함한 한국 도시화 vertical slice
- Git LFS and import presets

## Required execution slices

P9-01 전체를 하나의 장기 브랜치에서 진행하지 않는다. `Ready`로 옮기기 전에 다음 vertical slice를 각각 1–3일 규모의 GitHub 하위 이슈와 짧은 art branch로 나눈다.

1. 저층 R/C/I·공공시설 한국 도시화 vertical slice와 디자인 승인
2. 필요한 gameplay definition 범위의 catalog 확장과 stable variant 연결
3. P8 기준 material/LOD 최적화
4. provenance·라이선스 감사와 Windows/macOS build QA

acquisition과 importer proof는 P3-08에서 먼저 완료한다. P9-01은 대표 fixture를 다시 만드는 이슈가 아니라 수량 확장·한국 도시화·측정 기반 polish 이슈다.

## Mac steps

- 승인된 export를 받아 import 결과와 material/shader 호환을 확인한다.
- macOS build에서 누락 asset, case-sensitive path, LFS pointer 문제를 검증한다.
- vendor source의 hash/license/provenance sidecar와 승인 export의 연결을 감사한다.
- production asset 사용 전후 simulation checksum이 같은지 확인한다.
- 같은 simulation state의 variant 선택이 플랫폼과 저장 왕복에서 안정적인지 확인한다.

## Windows steps

- Blender 기준 버전과 공용 import/export/validator script로 원본을 정규화·제작한다.
- 무료 Standard ZIP의 hash, CC0 license, 취득 URL·날짜와 153개 model inventory를 acquisition manifest에 기록하고 `__MACOSX`와 다른 engine용 중복 파일을 제외한다.
- source FBX의 절대 texture path를 pack texture basename으로 재연결하고 누락·중복을 report한다.
- 1 unit=1m, bottom-center pivot, applied scale, naming 규칙을 지킨다.
- 8m cell footprint와 도로 접면 anchor를 metadata로 검증하고 `asset_id`, `definition_id`, prefab key를 파일명과 독립적으로 유지한다.
- vendor 원본과 축·scale·pivot·texture path 호환성 정규화 mesh/texture는 `Assets/ThirdParty/Quaternius`에 둔다. OpenSimCity 전용 wrapper prefab/material/definition과 자체 제작 art는 provenance를 붙여 `Assets/OpenSimCity`로 이동하고 `.meta`를 함께 커밋한다.
- 저층 R/C/I와 공공시설의 작은 vertical slice로 한글 간판·옥상·입면 모듈을 먼저 검증한 뒤 필요한 gameplay definition 범위로 확장한다.
- P8에서 측정된 asset군별 기준에 따라 material 통합, LOD와 필요한 view collider를 구성하고 Benchmark Scene에서 profiler/visual QA를 수행한다.
- 건물 성장 단계·도로 계층·지구 경계가 전체 도시 화면에서도 구분되는지 비교 캡처를 남긴다.

## Contracts and constraints

- `.blend`를 Unity가 직접 runtime source로 참조하지 않는다.
- first-party는 `Assets/OpenSimCity`, vendor는 `Assets/ThirdParty`에 둔다.
- 원본 ZIP 전체를 Unity `Assets`에 넣지 않으며, 필요한 source와 runtime 파일만 선별한다.
- binary source/export는 Git LFS와 필요시 lock을 사용한다.
- `Assets/ThirdParty`, `Blender/Source`, `Blender/References`의 PNG/JPG/JPEG가 디렉터리별 LFS 규칙을 실제로 적용받는지 `git check-attr`로 확인한다.
- source 파일명은 runtime stable ID가 아니며 파일 이동·rename이 저장 데이터나 variant 선택을 바꾸지 않는다.
- grid query가 selection의 기준이다. 물리 상호작용이 필요한 view에만 단순 collider를 두고 모든 asset에 같은 LOD/collider를 강제하지 않는다.
- palette/모듈 variant는 시각적 다양성만 제공하며 수용량·세수·성장 자격을 다시 계산하지 않는다.
- 쇠퇴·불법 주차·서비스 부족처럼 simulation 의미를 가진 외형은 해당 state/query가 있을 때만 선택한다.
- 고유 에셋 수를 늘리기 전에 제한된 모듈과 palette로 서로 다른 두 도시의 정체성이 읽히는지 검증한다.
- 유료 Source tier는 Standard로 필요한 pipeline/variant를 만들 수 없다는 검증 결과가 있을 때만 검토하고, 포함 Unity project의 Unity 6/URP 호환성을 별도로 확인한다.

## Verification

- asset validator 0 error
- acquisition hash/license/provenance audit
- texture relink와 scale/axis/pivot/footprint visual QA
- P8 결정표에 따른 material/LOD/선택적 collider QA
- LFS pointer and fresh clone import
- Windows/macOS build smoke
- 같은 seed에서 서로 다른 운영 도시의 전체 화면·데이터맵·핵심 지표 비교

## Done when

- [ ] 일관된 production set이 gameplay 계약을 바꾸지 않고 교체된다.
- [ ] MegaKit Standard와 모든 외부 source의 hash, license, provenance와 선별 내역이 재현 가능하게 기록된다.
- [ ] 모든 에셋이 해당 asset군의 naming, stable ID, pivot, scale, footprint, material과 선택적 LOD/collider 기준을 통과한다.
- [ ] 작은 한국 도시화 vertical slice가 승인된 뒤 필요한 definition 범위로 확장된다.
- [ ] 양쪽 플랫폼에서 누락 없이 렌더링된다.
- [ ] 도로 계층·건물 단계·지구 차이가 production 화면에서 simulation query와 일치한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 라이선스 불명 vendor asset
- Standard ZIP 전체의 Unity Assets 복사와 미검증 Source tier 구매
- gameplay 규칙 변경
- 런타임 procedural art
- 여러 시그니처 건물과 테마 팩
