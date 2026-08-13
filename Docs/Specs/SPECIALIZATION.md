# Urban Manufacturing Specialization Contract

P7-04 adds one derived city specialization: **urban light manufacturing**. It reuses the existing industrial building, single-goods market and aggregate goods-transport model rather than introducing individual factories, workers or trucks.

## Chain

```text
external raw-material import
  -> material preparation
  -> component inventory
  -> assembly
  -> finished inventory
  -> external export
```

All inventory, yield, loss, price, cost and progress arithmetic is integer-only. Stage unlocks persist once achieved. A stage can remain unlocked while its current monthly output is blocked by explicit labor, power, market, inventory or transport reasons.

## Identity and player causality

Specialization is not a checkbox. Material preparation unlocks from actual industrial jobs plus an external market connection. Assembly additionally requires accumulated material output. Export additionally requires accumulated finished output and a minimum aggregate goods-transport ratio. This keeps city identity derived from built capacity and network performance.

## State boundary

Persistent state includes inventories, cumulative imports/outputs/exports, private production revenue/cost, progress and three stage states. Presentation objects, paths, traffic caches and diagnostic display data are not authoritative. `SpecializationRuntimeSnapshotDto` schema 1 wraps the pure state DTO.

The production ledger is private-sector evidence and does not directly credit or debit the municipal ledger. Later balance/UI work may expose tax consequences only through existing city economic inputs, not by injecting a hidden output bonus.

## Diagnostics

The simulation input port carries industrial labor, powered labor, external-market availability, aggregate goods-transport health, pollution pressure and public-service coverage. The monthly result exposes labor, power, traffic, pollution, service and private-profit diagnostic factors. P7-05 owns player-facing presentation of those factors.

## Verification status

EditMode fixtures are committed for stage unlock, period inventory conservation, explicit bottleneck reasons and snapshot round-trip. Unity import/compile and test execution remain `unverified` under the current no-local/no-self-hosted-execution policy.
