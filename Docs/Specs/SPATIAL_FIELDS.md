# Spatial Fields Contract

## Fields and units

Every world cell stores five integer query channels in the inclusive range `0..1000`:

- land value
- pollution
- noise
- crime
- park amenity

Park amenity is a diagnostic channel and one named input to land value. Land value begins at a baseline of 500 and is derived after other channels are clamped:

```text
land value = clamp(
    500
    + direct land-value contribution
    + park amenity / 2
    - pollution * 2 / 5
    - noise / 4
    - crime / 3)
```

All division uses C# integer truncation toward zero at the displayed operation. Field calculations contain no floating-point input.

## Sources and falloff

A source has a stable source ID, stable definition ID, cell, enabled flag and integer contribution profile. The default representative definitions are:

- park
- industrial emitter
- traffic noise
- crime pressure
- civic improvement

For Manhattan distance `d` inside radius `r`:

```text
contribution = peak * (r + 1 - d) / (r + 1)
```

Outside the radius the contribution is zero. Source iteration is stable-ID ordered. Multiple sources add before each channel is clamped.

## Dirty propagation

Adding, moving, disabling or removing a source marks the bounded square covering its old and new radii. Dirty cells are stored as a sorted row-major index set. `RefreshDirty` evaluates only those indices, records the exact refreshed indices and touched chunks, then clears the set.

A full rebuild is available for restore and benchmark comparison. Performance evidence must compare the same field checksum and report evaluated cell counts; the code does not claim a CPU speedup without hardware measurement.

## City-derived sources

`SpatialFieldRuntimeModule` rebuilds presentation-independent sources from authoritative city state:

- each occupied road cell contributes the traffic-noise profile;
- each industrial building contributes the industrial-emitter profile.

Derived IDs use reserved namespaces and cannot be supplied by public source commands. They are rebuilt from roads/buildings and excluded from the capability snapshot.

Custom sources use application commands and are saved by stable ID. The capability snapshot stores source state only; cell arrays, dirty sets, chunks, overlay meshes and read models are rebuilt.

## Suitability

`SpatialSuitabilityQuery` publishes named, integer-only R/C/I preference scores. It does not create development eligibility or directly mutate growth. Residential, commercial and industrial uses weight the same public field channels differently. P6-02 may consume these factors for delayed condition changes.

## Read models and overlay

`SpatialFieldReadModel` projects row-major cell values, source descriptors, dirty evidence and checksum. `SpatialFieldOverlayReadModel` maps values to five fixed bands:

- 0–199: very low / dots
- 200–399: low / solid
- 400–599: medium / rings
- 600–799: high / hatch
- 800–1000: very high / cross-hatch

UI and Presentation consume Application enums and DTOs only. Pollution, noise and crime mark high values as adverse; land value and park amenity mark high values as beneficial/effective.

`SpatialFieldOverlayCoordinator` renders one dynamic mesh rather than one object per cell. Band-dependent inset and height provide a non-color intensity channel; the panel also provides text labels and exact numeric ranges. Overlay selection is transient and never enters simulation or persistence.

## Determinism and observation

- source order is stable-ID order;
- dirty evaluation order is row-major;
- chunk order is Y then X;
- calculations are integer-only;
- custom source command sequence is shared with the city runtime;
- derived sources depend only on road/building state;
- overlay visibility, active field, UI observation and mesh resolution cannot change field checksum.

## Verification status

Committed fixtures cover falloff, composition, clamp, dirty regions, chunk boundaries, insertion order, suitability, snapshot rebuild, derived road sources, overlay/query parity and dynamic-mesh lifecycle. Unity compilation, test execution, profiler timings and Windows/macOS parity remain `unverified` while local and self-hosted Unity hardware is unavailable.
