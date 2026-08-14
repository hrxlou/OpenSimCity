# [P9-03B] Save/content catalog compatibility

Issue: #132  
Parent: #44  
Depends on: #136

## Release save contract

The historical inner `CitySnapshot` schema and its v1→v2 migration remain unchanged. New release saves are written through `ReleaseCatalogSnapshotPayloadCodec`, which wraps the inner payload in the deterministic `OSCRSAVE` envelope.

Envelope v1 records:

- envelope version
- built-in content catalog version (`ReleaseBuildProfile.ContentCatalogVersion`, currently `1`)
- the exact stable definition IDs referenced anywhere in the snapshot DTO graph
- the original snapshot payload bytes

Definition references are discovered only from serialized `ulong` fields whose name ends with `DefinitionId`. The stored list is non-zero, unique, and strictly ascending. On load, annotated saves are decoded and the reference list is recalculated; any mismatch fails as corrupt data. An unsupported envelope/catalog version also fails closed. Existing `CitySnapshotSerializer` definition validation still rejects referenced definitions that do not exist in the runtime catalogs, so the envelope never substitutes another definition.

## Compatibility policy

- new saves: annotated envelope required through the runtime `CityPersistenceHost`
- previous unwrapped v1/v2 saves: accepted and forwarded to the existing snapshot codec/migration dispatcher
- unknown future inner snapshot versions: rejected by the existing migration dispatcher
- unknown future release envelope/catalog versions: rejected by the release codec
- missing/duplicate/zero/out-of-order envelope definition IDs: rejected
- annotated metadata that does not exactly match decoded snapshot references: rejected

The envelope is explicit little-endian binary with fixed magic and sorted numeric IDs, so its bytes do not depend on platform endianness or dictionary iteration order.

## Deferred runtime verification

No Unity save/load, Windows↔macOS file exchange, or previous-version physical fixture execution was run during implementation. Those checks remain `unverified` for the user-run release-candidate pass. The committed fixture source covers deterministic bytes, legacy unwrapped acceptance, unknown catalog rejection, duplicate/order rejection, and metadata/reference mismatch behavior but has not been executed here.
