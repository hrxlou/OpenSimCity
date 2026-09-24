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

## 2026-09-05 visual-foundation pass

- gameplay definition과 stable presentation key를 바꾸지 않는 범위에서 기존 primitive prefab의 실루엣과
  재질군을 보강했다. 주거·상업·공업·발전소·도로에 창호, 1층 입면, 옥상 설비, curb와 차선 layer를 추가했다.
- CityPrototype Editor setup에 matte ground, camera background, warm sun, soft shadow와 fog baseline을 추가했다.
- 이번 pass는 first-party production catalog 확장 전의 화면 품질 기준선이며, P9-01의 provenance·LOD·catalog
  완료 조건을 닫지 않는다.

## 2026-09-05 hybrid product-art promotion

- 검증된 Quaternius full-building 3종을 주거 저밀도 A/B/C presentation key에 승격한다.
- 상업 A/B/C는 first-party 구조에 검증된 brick facade detail을, 공업 A/B/C와 발전소는 검증된 mechanical prop detail을 결합한다.
- 총 10개 gameplay presentation key가 근거리 vendor-derived detail과 원거리 first-party lightweight geometry를 `LODGroup`으로 전환한다. 건설 중 상태와 cell 단위 도로는 기존 경량 prefab을 유지한다.
- 제품 프리팹 root는 footprint 단위로 정규화하고 `Detail`/`Distance` 계층 및 두 단계 LOD를 validator가 강제한다.
- `ProductArtPreviewCapture`는 제품 wrapper 10종의 LOD0을 1440×810 off-screen contact sheet로 렌더해
  데스크톱 캡처가 불가능한 batch/원격 환경에서도 material·silhouette·detail 결합을 검수한다.

## 2026-09-06 user Seoul-library integration

- 별도 Blender 작업 공간의 사용자 제작 `Seoul_Assets.blend`와
  `Seoul_Detail_Asset_Library.blend`를 원본 저장 없이 headless Blender로 선별 export한다.
- 저층 혼합상가 6종을 주거/상업 A–C key에 연결하고, 한국형 taxi·bus, 가로수·가로등과 교차로 횡단보도를
  behaviour/collider 없는 단일-renderer prefab으로 정규화한다.
- `ArtSource/Blender/export_seoul_library.py`와 `SEOUL_LIBRARY.md`가 선택 목록과 재현 절차를 고정한다.
- `road.boulevard`의 실제 runtime wrapper는 사용자 제작
  `KR_RD_6Lane_Straight40m`을 사용한다. 기존 P9 정적 geometry benchmark manifest는
  gameplay proxy인 `OSC_REP_ROAD_2LANE` 예산을 공유하며, 사용자 원본을 CC0 vendor
  provenance로 잘못 분류하지 않는다.
- 차량 pool은 최대 48개와 결정적 2차선 오프셋을 사용하고, 배경 수목·가로등은 중앙 coordinator 하나가 생성한다.
  이는 simulation state나 command stream을 변경하지 않는 presentation 작업이다.

## Done when

현재 진행: Blender MCP 제작 건물군, Poly Haven PBR 표면, 사용자 제작 서울 에셋 선별 세트를 Unity catalog에
연결했다. 전역 렌더 설정의 HDR·4x MSAA·soft shadow·cascade를 구성했고 macOS player에서 축척·재질·그림자를
검수한다. 전체 R/C/I catalog 확장과 Windows 검수, 고품질 설정의 GPU 비용 측정은 남아 있다.

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

## 2026-09-09 UI·광원·표면 수정

shadow cutoff, 활성 scene RenderSettings 저장, scene transition 낮밤 설치, Trilight·reflection 변화, 미연결 roughness 및 FBX 누락 UV를 수정했다. v08 수목 2종·벤치·휴지통과 Poly Haven HDR reflection을 연결했다.

세부 재현 계약은 `Docs/DESIGN.md`의 2026-09-09 절, 출처는 `Docs/Art/VISUAL_REFERENCES_2026-09-09.md`를 따른다. 최종 실행 검증 기록은 후속 검증 절에 추가한다.

검증 중 roughness 연결에 따라 fallback benchmark의 고유 texture가 3→6으로 변경됨을 확인했다. renderer 188/material slot 204/triangle 14,912/unique mesh 3은 유지됐으며, texture memory 50,337,936 bytes를 기록했다. 이는 geometry나 FPS 개선 주장이 아니다.

### macOS 검증 결과

Unity 6000.5.3f1: EditMode 380 통과/실패 0/Windows 전용 1 제외, PlayMode 56 통과/실패 0. macOS Standalone build 및 native 1920×1080 실제 리뷰 렌더 확인. v08 export는 Blender Cycles에서도 검수했다. 상세 파일은 ignored `Artifacts/VisualPolish/2026-09-09/VERIFICATION.md` 및 XML·PNG·app. Windows 실행 증거는 이번 Mac 작업에 포함되지 않으며, P9 전체의 양 플랫폼 출시 승인을 의미하지 않는다.
