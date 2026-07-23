# P3-08 production-like representative art baseline

## Fixture

- Source: Quaternius Downtown City MegaKit Standard, CC0-1.0
- Acquired archive SHA-256:
  `5b1a945576d54cdbb4ccc9c3d52711e6d530da74c74c586407ecc28b165335da`
- Selection: small/medium/large building, two-lane street, brick facade, AC prop
  각 1개
- Layout: representative binding 6개를 각 4회 반복한 24개 instance
- Boundary: `Benchmark` scene 전용. `CityPrototype`과 simulation/gameplay catalog는
  변경하지 않는다.

source triangle과 material slot은 budget이 아니다. 아래 값은 동일한 fixture와
카메라에서 후속 P8 측정이 비교할 관찰 기준선이다.

## Environment

- Date: 2026-07-23
- OS: Microsoft Windows 11 Enterprise 10.0.26200, 64-bit
- Unity: 6000.5.3f1 (`c2eb47b3a2a9`)
- Blender: 5.1.2
- CPU: Intel Core i5-10400F
- Memory: 31.9 GiB
- GPU: NVIDIA GeForce RTX 3060, 8036 MiB reported by Unity, driver
  32.0.16.1062
- Player graphics API: Direct3D 12

## Unity fixture baseline

| Mode | Renderers | Material slots | Unique materials | Triangles | Unique meshes | Textures | Texture runtime memory | Draw calls |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| production-like | 24 | 172 | 17 | 355,648 | 6 | 17 | 57,679,296 B | 175 |
| P3 placeholder | 56 | 56 | 7 | 672 | 1 | 0 | 0 B | 59 |

Draw call은 Windows standalone player의 같은 프레임에서 Unity 6.5가 제공하는
render-path counter를 합산했다. production-like breakdown은 Standard 2, SRP
Batcher 172, Null Geometry 1이고 placeholder는 Standard 2, SRP Batcher 56,
Null Geometry 1이다. 이 값은 fixture 외의 동일 player/test harness 비용도 포함하므로
절대 budget이 아니라 같은 harness의 비교 기준이다.

Unity import 과정에서 degenerate triangle이 제거되어 source 합계와 Unity fixture
triangle이 다르다. Blender validator는 source/normalized geometry를 검증하고,
Unity 테스트는 실제 import 결과를 별도로 고정한다.

## Import baseline

Unity `AssetDatabase.ImportAsset`를 `ForceSynchronousImport | ForceUpdate`로 실행한
warm-project 기준:

- normalized FBX 6개: 1,778 ms
- texture 17개: 3,983 ms
- total: 5,761 ms

이 수치는 `-batchmode -nographics`의 단일 Windows 측정값이며 성능 budget이나
개선 주장으로 사용하지 않는다.

## Commands and results

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' `
  --background --factory-startup `
  --python Blender/Scripts/Validators/validate_downtown_city_representatives.py -- `
  --manifest Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard/Provenance/acquisition-manifest.json `
  --output-root Assets/ThirdParty/Quaternius/DowntownCityMegaKit/Standard
```

Result: `P3_08_VALIDATION_PASSED assets=6 textures=17`.

```powershell
& $UnityEditor -batchmode -projectPath $RepoRoot -runTests `
  -testPlatform StandaloneWindows64 `
  -testFilter OpenSimCity.Tests.PlayMode.RepresentativeArtBenchmarkPlayModeTests.PlaceholderAndProductionLikeModesRecordSeparateBaselines `
  -buildPlayerPath Artifacts/Art/P3-08/PlayerTests/OpenSimCityP308Tests.exe `
  -testResults TestResults/p3-08-player-graphics.xml
```

Result: `1/1 passed`.

## Remaining cross-platform verification

- macOS fresh LFS checkout에서 manifest/entry hash와 대소문자 경로를 확인한다.
- macOS Unity 6000.5.3f1에서 texture/material import 및 Benchmark render smoke를
  실행한다.
- macOS에서 representative mode 전환 전후 simulation checksum 동일성을 확인한다.
