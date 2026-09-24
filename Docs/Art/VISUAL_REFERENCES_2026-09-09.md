# UI·도시 그래픽 레퍼런스 및 취득 기록 — 2026-09-09

## 디자인 판단

사용자는 각진 UI, 알림에 한정한 측면 색 강조, 단색 남색 창을 벗어난 재질감, 깨끗한 Pretendard/Noto Sans 계열 글꼴을 요청했다. 이 요구가 일반적인 둥근 카드 스타일보다 우선한다.

## 외부 레퍼런스

- [Cities: Skylines II — Zones & Signature Buildings](https://www.paradoxinteractive.com/games/cities-skylines-ii/features/zones-signature-buildings): 도시 theme를 건물뿐 아니라 도로 마킹·가로 소품·차량까지 일관되게 적용한다는 참고. 게임 아트를 복사하거나 재배포하지 않는다.
- [Cities: Skylines UI manual](https://cdn.akamai.steamstatic.com/steam/apps/255710/manuals/CitiesSkylines-UserManual_EN.pdf): 월드 중심 시뮬레이션의 상태/건설/상세 정보 역할 분리 참고.
- [Kenney City Kit Roads](https://kenney.nl/assets/city-kit-roads): 모듈 도로 에셋 후보. 현재 사용자의 상세 Seoul 도로가 사실적 방향에 더 맞아 신규 import하지 않았다.
- [Poly Haven Concrete Pavement 02](https://polyhaven.com/a/concrete_pavement_02): 보도 입자 크기/줄눈 참고 후보. 기존 여의도 granite/concrete maps를 우선 연결했다.

## 실제 채택

- [Green Point Park HDRI](https://polyhaven.com/a/green_point_park), Greg Zaal, [CC0](https://polyhaven.com/license). 1K HDR source → 256px specular-convolved cubemap. `Assets/ThirdParty/PolyHaven/green_point_park/provenance.json`에 URL·날짜·SHA-256 기록. 용도는 유리/금속 reflection.
- [Pretendard v1.3.9](https://github.com/orioncactus/pretendard/releases/tag/v1.3.9), SIL OFL 1.1. 기존 취득 archive의 정적 Regular/SemiBold OTF를 사용. runtime은 64px TextCore SDF + 2048px dynamic atlas이며 실사용 문자 430개를 사전 생성했다. 원본·파생 atlas·license 모두 ThirdParty에 보존한다.
- 사용자 `Seoul_Yeouido_v08_Final/Seoul_Yeouido_v08_V07Structure_Photoreal_Integrated_v2.blend`: 상세 수목 2종·벤치·휴지통 채택. 수목 export 정점은 원본 evaluated 101,298 → 41,993이며 이 수치를 FPS 개선으로 해석하지 않는다. 건물 후보 2종은 단순 mass 수준이라 채택하지 않았다.
- 사용자 `Seoul_Yeouido_v09_GIS/README_KO.md`: 실제 도로·건물 평면 데이터 기반 작업임을 확인. 현재 게임 grid에 전체 GIS 장면을 무리하게 축소 통합하지 않았다. v08 미술 원형을 재사용하며, 현재 리뷰 fixture는 여의도 디지털 트윈이 아니다.
- `hud-graphite-v2.png`: built-in ImageGen으로 생성한 first-party UI 소재. 전체 프롬프트 요지·SHA-256은 `Assets/OpenSimCity/Content/UI/Textures/README.md` 참조. 제품 icon/text/geometry는 실제 UI Toolkit 요소다.
