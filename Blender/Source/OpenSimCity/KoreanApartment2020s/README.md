# KoreanApartment2020s

가상의 2020년대 서울 신축 아파트 주동 테스트 에셋이다. 실존 단지의 로고·텍스처·고유 facade를 복제하지 않고, 강남권 신축 아파트에서 공통적으로 읽히는 밝은 판상형 매스, 반복 창호, 차콜/브론즈 수직 프레임, 어두운 저층 포디움과 옥탑 프레임만 절차적으로 조합한다.

## 재생성

저장소 루트에서 설치된 Blender 5.1 계열을 사용한다.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --python Blender/Scripts/Generators/generate_korean_apartment.py
```

생성 결과:

- source: `Blender/Source/OpenSimCity/KoreanApartment2020s/korean_apartment_2020s.blend`
- explicit exports: `Blender/Export/OpenSimCity/KoreanApartment2020s/`
- Unity runtime copies: `Assets/OpenSimCity/Content/Art/Buildings/KoreanApartment2020s/`
- previews/report: `Artifacts/Art/korean-apartment-test/` (gitignore 대상)

`bpy` 스크립트는 매스→입면→저층부/옥탑→LOD→preview/export/validation 순서로 실행하며, 중간 checkpoint `.blend`를 artifact 폴더에 저장한다.

## Unity 연결

FBX는 Unity runtime용으로 사용하고, GLB는 외부 도구/검증용으로 함께 보존한다. `.blend`는 Unity runtime source로 참조하지 않는다. Unity에서 import 후 `KoreanApartmentAssetSetup` 메뉴를 실행하면 embedded model material을 URP material로 정규화하고, LOD0/1/2 FBX를 묶은 prefab과 `LODGroup`을 재생성할 수 있다.
