# P6-02 - Building decline, abandonment and redevelopment

Issue: #29

## Implemented capability

- stable-ID keyed `Healthy → Stressed → Declining → Abandoned → Redeveloping` state
- explicit adverse, recovery and redevelopment month counters
- land value, pollution, noise, crime, road access, power and occupancy factors
- deterministic factor ordering and top-reason query
- condition-aware 1000/800/500/0 permille effective building capacity
- pre-population monthly scheduler seam
- forced aggregate departures when effective housing capacity falls
- abandonment eligibility and explicit redevelopment block reasons
- industrial-use tolerance without weakening surrounding spatial fields
- atomic redevelopment command, municipal cost and rollback
- schema-1 condition snapshot with unknown-building validation
- selected-building Application state and inspector read model
- condition footprint datamap using the same period and reasons
- central pooled world markers and one dynamic datamap mesh
- CityPrototype scene bootstrap and unload cleanup

## Experience fixture

The committed CityPrototype fixture uses only public construction and spatial-source commands:

1. build road, R/C/I zones and power;
2. allow the first residential building to grow;
3. add a pollution source at that building;
4. preserve the same stable building ID while seven monthly periods progress to abandonment;
5. verify occupancy/effective capacity reach zero and inspector/datamap identify the same top reason;
6. remove the source and preserve recovered conditions for two months;
7. commit an explicit redevelopment command;
8. process three redevelopment months and return to Healthy with the same definition, parcel, anchor and footprint.

Recovery is not immediate and Presentation never owns a timer or replaces the building object as simulation state.

## Static architecture evidence

- condition state and timers are Unity-free Simulation data;
- Application resolves P6-01 fields, parcel accessibility and power queries;
- condition runs before Population in deterministic registration order;
- Population validates that condition capacity only reduces static definition capacity;
- command preview does not debit funds or begin redevelopment;
- failed commit refunds funds and keeps the previous stage;
- UI and Presentation consume projected Application enums/DTOs;
- marker objects contain no per-building `MonoBehaviour`;
- selected cell, panel state, datamap mesh and marker pool are omitted from snapshots.

## Persistence boundary

Saved:

- building stable ID and stage
- age/adverse/recovery/redevelopment counters
- processed month
- ordered factors and top reason
- eligibility and block reason

Rebuilt:

- P6-01 field arrays and derived sources
- parcel/power environment query
- effective capacity projection
- inspector/datamap rows
- pooled markers and dynamic mesh

## Committed fixtures

- `BuildingConditionSystemTests`
- `BuildingConditionCommandTests`
- `BuildingConditionPersistenceGuardTests`
- `BuildingConditionPlayModeTests`
- `Docs/Specs/BUILDING_CONDITION.md`

## Unverified hardware evidence

Local and self-hosted Unity execution is disabled while both machines are unavailable.

- `unverified`: Unity import and C# compilation
- `unverified`: EditMode and PlayMode execution
- `unverified`: selected-building click flow on actual mouse/trackpad
- `unverified`: condition marker and datamap appearance
- `unverified`: Korean font fallback and UI scaling
- `unverified`: pool, GC, CPU/GPU frame and profiler-marker measurements
- `unverified`: Windows/macOS timeline and checksum parity

No passing test count, frame budget or profiler result is claimed.
