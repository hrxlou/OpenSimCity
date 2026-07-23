# [P3-08] representative production-like 아트 성능 슬라이스

- Phase: `P3–P7 parallel preparation`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `2–3 days`
- Branch: `art/p3-08-representative-performance-slice`
- Depends on: `P3-07`

## Goal

placeholder와 구조가 다른 실제 후보 에셋 소수로 Benchmark 전용 fixture를 만들어 P8의 renderer, material, texture, memory와 LOD 측정이 production과 동떨어지지 않게 한다.

## Scope

- Downtown City MegaKit Standard acquisition manifest, hash/license와 선별 inventory
- small/medium/large 건물 각 1개, 도로 1개, facade/prop 각 1개의 대표 source
- texture relink와 scale/axis/pivot/8m footprint 정규화 proof
- vendor mesh/texture와 OpenSimCity wrapper prefab/material/definition 분리
- Benchmark 전용 representative catalog와 반복 배치 fixture
- triangle, renderer, material slot, texture memory, draw call과 import time baseline

## Mac steps

- acquisition manifest와 vendor/wrapper provenance 경계를 검토한다.
- fresh LFS checkout에서 texture/material 누락과 대소문자 경로를 확인한다.
- representative view on/off가 simulation checksum을 바꾸지 않는지 검증한다.

## Windows steps

- 공용 Blender importer/validator로 source FBX 절대 texture path와 transform을 정규화한다.
- vendor 원본·호환성 정규화 결과는 `Assets/ThirdParty/Quaternius`, 프로젝트 전용 prefab/material/definition은 `Assets/OpenSimCity`에 둔다.
- 대표 asset을 Benchmark fixture에 반복 배치해 placeholder fixture와 renderer/material/texture/draw call 차이를 기록한다.
- collider와 LOD는 일괄 생성하지 않고 P8에서 결정할 관찰값과 후보만 기록한다.

## Contracts and constraints

- CityPrototype의 P3 placeholder를 교체하지 않는다. 이 이슈의 runtime 통합은 Benchmark fixture에 한정한다.
- 대표 asset은 production 전체 세트나 한국 도시화 완료를 의미하지 않는다.
- source 모델 수치를 성능 budget으로 선언하지 않고 Unity fixture의 측정 입력으로만 사용한다.
- wrapper definition ID와 prefab key는 vendor 파일명과 독립적인 안정 ID다.
- 외부 license/hash/provenance와 LFS pointer가 없으면 fixture를 승인하지 않는다.

## Verification

- hash/license/provenance와 선별 목록
- texture relink, scale/axis/pivot/footprint validator
- placeholder 대비 renderer/material/texture/draw call baseline
- fresh checkout Windows/macOS import·render smoke
- view on/off simulation checksum

## Done when

- [x] 대표 건물·도로·모듈이 재현 가능한 importer/validator를 통과한다.
- [x] vendor asset과 OpenSimCity wrapper의 저장 경계가 실제 파일로 검증된다.
- [x] P8 fixture가 사용할 production-like renderer/material/texture baseline이 기록된다.
- [x] CityPrototype placeholder와 gameplay 계약을 바꾸지 않는다.
- [x] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Implementation evidence

- Windows 11 Enterprise 10.0.26200, Unity 6000.5.3f1, Blender 5.1.2
- acquisition archive SHA-256:
  `5b1a945576d54cdbb4ccc9c3d52711e6d530da74c74c586407ecc28b165335da`,
  CC0-1.0, selected assets 6/textures 17
- Blender validator: `P3_08_VALIDATION_PASSED assets=6 textures=17`
- Windows standalone renderer baseline test: `1/1 passed`, production-like
  `24 renderers / 172 material slots / 17 textures / 175 draw calls`,
  placeholder `56 / 56 / 0 / 59`
- Unity import baseline: models 6 `1,778 ms`, textures 17 `3,983 ms`,
  total `5,761 ms`
- Blender 5.1.2 clean export 2회에서 normalized FBX 6개의 SHA-256가
  각각 일치했다.
- Detailed evidence:
  [P3-08 production-like art baseline](../../Benchmarks/P3-08-production-like-art-baseline.md)
- Branch: `codex/p3-08-production-like-art-performance-slice`
- Implementation commit: `b52cb6feed85efd5cd66342dc5a73762a6353cc4`
- PR: [#91](https://github.com/hrxlou/OpenSimCity/pull/91)

## Out of scope

- production catalog 수량 확장
- 한국 도시화 facade·간판 세트
- 최종 material atlas·LOD·collider 기준
- CityPrototype 또는 저장 catalog의 production 교체
