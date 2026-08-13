# Water and Density Contract

P7-02 adds deterministic road-component water allocation and Low/Medium/High density progression. Static footprint remains definition-owned.

A density stage completed in month N changes effective population/employment capacity beginning in month N+1. Ordinary R/C/I tax therefore follows actual next-period occupancy/employment rather than a same-period bonus.

Density eligibility requires population, water, land value, road tier and public-service coverage. Upgrade starts are capped per month in stable building-ID order and pause without resetting when a requirement is lost. Capacity multipliers are 1x/2x/4x and compose with building-condition capacity.

The schema-1 capability snapshot stores water producers and density stage/progress. Network allocation, road components, eligibility inputs and overlay meshes are rebuilt.

Unity compile/test execution, visual QA, profiling and cross-platform parity remain unverified under the current no-local/no-self-hosted-execution policy.
