# P7-03 Service Response UI and Pooled Views

Issue: #33

Implemented scope:

- unmet service rows become deterministic response events
- assigned road-time service events reuse the existing deterministic route planner
- no assigned facility, school service or unavailable road route produces no display vehicle
- bounded 0..64 response-route sampling with stable IDs and phase offsets
- existing central `VehicleViewScheduler` reused for behaviour-free pooled views
- transient Application setting connects UI controls to Presentation without UI→Presentation references
- P7-01 budget/capacity/coverage panel extended with response status and display controls
- EditMode projection fixtures and CityPrototype PlayMode smoke source

Verification status: Unity import/compile, EditMode/PlayMode execution, actual pooled response rendering, input/font scaling, profiler measurements and Windows/macOS parity remain `unverified`. No passing-test count or measured performance claim is made.
