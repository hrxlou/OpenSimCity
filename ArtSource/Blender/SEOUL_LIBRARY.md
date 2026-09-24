# User-authored Seoul asset library

Source library: a separate local Blender workspace (set its path when running the exporter).

The source `.blend` files are user-authored project assets. `export_seoul_library.py`
opens them read-only in Blender background mode, duplicates selected asset collections,
joins each export to one renderer, normalizes its pivot to ground centre, and writes FBX
artifacts under `Assets/OpenSimCity/Content/Art/Seoul/Models`. Source files are never saved
or modified.

Primary sources:

- `Seoul_Assets.blend`: mixed-use buildings, cars, bus, anchored street trees, lighting,
  shelters, bollards and modular roads.
- `Seoul_Detail_Asset_Library.blend`: crosswalk, detailed street light and supported tree.
- `Seoul_Base.blend`: grounded long-span Seoul bridge source; export it with the
  optional `--objects-only` switch so the city-library FBX files are not rewritten.
- `Seoul_Yeouido_v05/Korea_City_Assets_v05.blend` in that workspace:
  user-authored v05 office, mixed-use, villa, apartment and underpass prototypes.
  The selected `V05_` collections are geometry/material prototypes, not surveyed
  parcel data; their source package remains read-only.

The selected road set includes authored 4/6/8/10-lane straight modules. OpenSimCity
currently wraps the 4-lane module as an avenue and the 6-lane module as a boulevard;
the larger modules stay available for a later multi-cell arterial/intersection pass.

## 2026-09-09 v08 integration

`export_yeouido_game_details.py` extracts StreetTreeA/B, Bench and WasteBin from the v08 Photoreal Integrated v2 file under the Blender workspace. It appends only the four prototypes into an empty scene before evaluation; the 25,573-object city is never evaluated/exported as one game asset. `v08-export-report.json` records source dimensions and vertex counts. Current office/apartment blocks from v08 were inspected but rejected as too simple; existing v05 detailed facades remain active.

`SeoulSurfaceUvPostprocessor` supplies box UVs/tangents only when the FBX has no UV channel, retaining authored UVs elsewhere. These imported meshes keep Read/Write enabled for Unity 6000.5 postprocessing compatibility; this has a CPU memory cost and is not a performance optimization claim. Empty geometry now aborts wrapper creation.

`UrbanRoughnessSetup` packs the source roughness into linear URP smoothness alpha. Source .blend files are never saved by the exporter.
