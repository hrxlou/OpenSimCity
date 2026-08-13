# P7-06B District Policy Evidence

Parent issue: #55. Depends on merged P7-06A (#115 / PR #119).

Implemented capability:

- stable per-district HeavyVehicleRestriction and TransitSubsidy state
- next-month schedule/disable commands
- all-or-none municipal monthly funding with explicit insufficient-funding reason
- goods-only route preference input and identity default
- bus generalized-cost input and identity default
- traffic/transit policy version invalidation without persistence of derived routes
- policy format-1 save/restore and district-delete cleanup
- district comparison read model for population, jobs, traffic/noise, transit split, service, pollution and policy cost
- public preview/last-period comparison panel
- EditMode/PlayMode fixture source

No direct RCI, land-value, noise, traffic-volume, or mode-share output bonus is owned by a policy. Those values are derived through existing traffic, transit, service and spatial state.

Verification is static in this branch. Unity import/compile, EditMode/PlayMode execution, visual/input QA, profiler evidence and Windows/macOS parity remain unverified; no pass count is claimed.
