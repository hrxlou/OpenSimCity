# Persistence migration fixtures

- Files are UTF-8 without BOM and use LF line endings.
- A committed version fixture is immutable. Add a new source and golden target pair instead of editing historical bytes.
- Name pairs `p4-v{source}-minimal.json` and `p4-v{target}-minimal.json`; larger city fixtures add a stable scenario suffix.
- A migration test must clone the source bytes, assert that the source is unchanged, compare the migrated bytes with the golden target, and verify the target header version.
- JSON field order and numeric representation follow the snapshot contract in `Docs/ARCHITECTURE.md`.
- Cross-platform handoff records the fixture byte count and FNV-1a checksum before loading and after re-saving.
