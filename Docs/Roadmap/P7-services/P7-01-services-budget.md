# P7-01 - Public services coverage, capacity and budget

Issue: #30

## Implemented

- stable service definitions and facility runtime state for police, fire, school, healthcare and waste
- spatial-distance and road-travel-time coverage strategies
- stable building/facility allocation and conserved aggregate demand
- range, capacity, usage and explicit uncovered reasons
- next-month `0..150%` facility budget command
- deterministic municipal funding scale and operating cost
- public placement/remove/budget commands with preview and rollback
- schema-1 facility/budget capability snapshot
- versioned Application facility/coverage read model
- selected-cell placement and selected-facility budget UI
- central pooled facility placeholders and one coverage mesh

## Committed fixtures

- `PublicServiceSystemTests`
- `PublicServicePlayModeTests`
- `Docs/Specs/PUBLIC_SERVICES.md`

## Experience path

```text
build connected R/C/I city
  -> select empty road-adjacent cell
  -> place police facility through public command
  -> process service month
  -> inspect usage/capacity/coverage reason
  -> enable police coverage map
  -> schedule 50% budget
  -> next month observes reduced capacity/range/cost
```

## Unverified

- Unity import and compile
- EditMode and PlayMode execution
- road-time coverage against actual directed route fixtures
- service placeholder and coverage visual QA
- UI pointer/keyboard and OS scaling
- municipal balance scenario in a running Player
- pool/GC/CPU/GPU profiler evidence
- Windows/macOS checksum parity

No test pass count or measured runtime budget is claimed while local and self-hosted Unity machines are unavailable.
