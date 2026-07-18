# Poly Haven 선택 기록

검색 시점: Phase D 건축 형태 완성 및 `03_architecture_complete.blend` 저장 이후.

## 선택 후보

| 에셋 이름 | ID | 유형 | 적용 예정 위치 | 해상도 | 선택 이유 | 기존 대체재 |
|---|---|---|---|---|---|---|
| Cambridge | `cambridge` | HDRI | 중립 낮 월드 조명 | 2K HDR | outdoor/urban/midday/overcast/low contrast 조합으로 밝은 외벽과 입면 깊이 판단에 적합 | 절차적 중성 월드 + Sun/Area Light가 있으며 다운로드 실패 시 유지 |
| Suburban Parking Area | `suburban_parking_area` | HDRI | 절제된 해질녘 월드 조명 | 2K HDR | outdoor/urban/sunrise-sunset/clear/low contrast이며 과도하게 극적이지 않은 도시 주변광 후보 | 절차적 청회색 dusk 월드 + 저강도 Sun/Area Light가 있으며 다운로드 실패 시 유지 |

## 제외 결정

- 깨끗한 외벽용 Texture는 `clean, concrete, wall` 교집합 검색 결과가 0건이었다.
- 핵심 외벽, 유리, 차콜 패널, 브론즈 금속은 색·러프니스·미세 범프를 한곳에서 제어하는 Blender 절차적 셰이더가 이미 있으므로 외부 PBR로 교체하지 않는다.
- 모델/조경 소품은 다운로드하지 않는다. 현재 절차적 지면·수목·사람 실루엣으로 스케일 검토가 가능하며 입면을 가리는 소품 추가가 필요하지 않다.
- 4K/8K는 이번 선택에서 제외한다. 2K HDRI로 조명 개선 여부를 먼저 검증한다.
