# P7-02 Water Utility and Density

Issue: #32
Branch: `feature/p7-02-water-density`

Implemented scope:

- deterministic road-component water production/allocation and explicit failure reasons
- stable water producer commands, construction cost and monthly maintenance
- Low/Medium/High density state with stable-ID monthly start cap
- population, water, land value, road tier and public-service eligibility factors
- pause/resume upgrade progress and schema-1 stage/progress snapshot
- density capacity composed with building decline capacity
- next-period density tax timing through ordinary R/C/I occupancy/employment settlement
- read model, UI, pooled markers and water/density overlay
- EditMode and PlayMode fixture sources

Static integration corrections included while closing the slice:

- definition-owned building footprint is resolved from stable definition ID without adding footprint to the building snapshot DTO
- parcel external-road and power building queries have compatibility seams required by P6 condition code
- water/density helper queries use deterministic stable-ID and row-major inputs

Verification status: Unity import/compile, EditMode/PlayMode execution, visual QA, profiler measurements and Windows/macOS parity remain `unverified` because local and self-hosted Unity execution is intentionally disabled. No passing-test count or runtime performance number is claimed.
