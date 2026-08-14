# P9-02 release UX evidence gate

P9-02A/B/C implementation being merged is not itself a release UX pass. `P9ReleaseUxGate` requires evidence pinned to one commit for the first-15-minute flow, representative problem diagnosis/recovery, settings round-trip, non-color problem communication, tutorial/presentation checksum invariance, and both Windows and macOS smoke runs.

The gate records implementation readiness separately from physical/runtime evidence. Missing Unity, hardware, audio-device, render, input, or cross-platform evidence remains `unverified` and causes the gate to remain open rather than being inferred from source or fixture presence.

No local or self-hosted Unity verification was performed while initially implementing this gate. Later regression verification is recorded below.

## 2026-08-14 system-panel docking regression fix

Source implementation is prepared on `fix/ui-panel-docking-menu` from `main` `a5a4eb474fd060cc23bd2018f4218ce1be17d840`. The verified runtime candidate through `d3943f091b1c669da0954bea416da5c06975f846` introduces the shared system-panel dock, removes independent absolute screen coordinates from the eight system UI binders, makes the release menu a full-screen modal backdrop, suppresses the dock while that menu is open, installs the release menu across scene loads, and updates existing world-input PlayMode fixtures to explicitly close the default-open menu before exercising world interactions. `SystemPanelDockPlayModeTests` covers initial menu suppression, input blocking, dock adoption, normalization and deterministic ordering.

PR #144 CI run #223 (`31780479618`) completed successfully on the self-hosted Windows Unity runner. EditMode passed 371/371, PlayMode passed 52/52, and the benchmark-evidence step passed. This verifies the automated Unity regression coverage for the candidate, including the new system-panel dock/menu tests and the existing input/placement suite.

Physical/visual release evidence remains `unverified`: `CityPrototype` has not been visually smoked at the full target window/aspect-ratio set; scrollbar and real pointer interaction have not been exercised manually in a running Player or Editor; the initial full-screen menu plus post-close panel stack has not been captured on Windows or macOS; and no macOS smoke run was performed for this regression fix.
