# OpenSimCity repository instructions

## Architecture

- Keep simulation state independent from Unity presentation objects.
- `Core` and `Simulation` must not reference UI, presentation, gameplay, scenes, prefabs, or editor code.
- Player interaction changes city state through application commands; do not mutate simulation arrays directly from views.
- ScriptableObjects contain static definitions only. Runtime state uses stable IDs and serializable state/DTO types.
- Avoid per-building and per-vehicle `Update()` methods. Use central schedulers and pooled views.
- Preserve deterministic results for the same seed and ordered command stream.

## Unity assets

- Put first-party assets under `Assets/OpenSimCity`; put imported vendor assets under `Assets/ThirdParty`.
- Keep vendor-derived compatibility-normalized meshes and textures under `Assets/ThirdParty`; put project-authored wrapper prefabs, materials, definitions, and first-party art under `Assets/OpenSimCity`.
- Do not hand-edit large Scene or Prefab YAML graphs. Prefer reproducible Editor scripts.
- Preserve and commit `.meta` files with their assets.
- Do not add generated Unity directories such as `Library`, `Temp`, `Logs`, `Obj`, `Build`, or `UserSettings`.

## Verification

- Add EditMode tests for simulation rules and serialization.
- Add PlayMode or smoke tests for scene, input, view, and UI changes.
- Run the narrowest relevant tests first, then the broader available suite.
- Do not claim a performance improvement without benchmark or profiler evidence.
- Update `Docs/ARCHITECTURE.md`, `Docs/Roadmap/README.md` and the affected `Docs/Roadmap/P*/` issue, or `Docs/WORKFLOW.md` when changing their contracts.
- Update `Docs/DESIGN.md` when changing UI, visual, art-pipeline, or phase exposure contracts.
