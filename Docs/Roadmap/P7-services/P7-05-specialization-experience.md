# P7-05 Specialization UI, Balance and Experience

Issue: #54

Implemented:

- current-city environment binding for industrial jobs, powered jobs, external road market, goods matching, spatial pollution and public-service coverage
- Application read model projecting P7-04 state into stage, bottleneck and diagnostic view enums
- player panel showing same-period progress, inventories, private revenue/cost, stage utilization, bottleneck and six diagnostics
- bottleneck-specific recovery guidance without duplicating simulation rules in UI
- fixed general-city vs specialized-city balance fixture
- transport bottleneck -> recovery fixture
- 60-month private revenue bound fixture preventing revenue outside declared export flow
- CityPrototype UI observation/scene cleanup smoke source

The specialization remains derived from actual industrial capacity and production progress. No municipal cash bonus is injected from specialization output.

Verification status: Unity import/compile, EditMode/PlayMode execution, actual playthrough balance, font/input QA and cross-platform parity remain `unverified` because local and self-hosted Unity execution are disabled. No passing-test or measured-balance claim is made.
