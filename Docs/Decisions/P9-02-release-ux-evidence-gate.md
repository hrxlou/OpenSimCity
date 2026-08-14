# P9-02 release UX evidence gate

P9-02A/B/C implementation being merged is not itself a release UX pass. `P9ReleaseUxGate` requires evidence pinned to one commit for the first-15-minute flow, representative problem diagnosis/recovery, settings round-trip, non-color problem communication, tutorial/presentation checksum invariance, and both Windows and macOS smoke runs.

The gate records implementation readiness separately from physical/runtime evidence. Missing Unity, hardware, audio-device, render, input, or cross-platform evidence remains `unverified` and causes the gate to remain open rather than being inferred from source or fixture presence.

No local or self-hosted Unity verification was performed while implementing this gate.

## 2026-08-14 system-panel docking regression fix

Source implementation is prepared on `fix/ui-panel-docking-menu` from `main` `a5a4eb474fd060cc23bd2018f4218ce1be17d840`. The runtime candidate through `6b5906f9bff889f02ed9293a4bf91227012aae21` introduces the shared system-panel dock, removes independent absolute screen coordinates from the eight system UI binders, makes the release menu a full-screen modal backdrop, and suppresses the dock while that menu is open. `SystemPanelDockPlayModeTests` was added to cover initial menu suppression, input blocking, dock adoption, normalization and deterministic ordering.

Runtime evidence remains `unverified`: the new PlayMode tests were not executed; `CityPrototype` was not visually smoked at the target window/aspect-ratio set; scrollbar/pointer interaction was not exercised in a running Player or Editor; and the initial full-screen menu plus post-close panel stack was not captured on Windows or macOS. No local or self-hosted Unity verification was run for this regression fix.
