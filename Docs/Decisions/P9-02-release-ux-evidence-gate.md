# P9-02 release UX evidence gate

P9-02A/B/C implementation being merged is not itself a release UX pass. `P9ReleaseUxGate` requires evidence pinned to one commit for the first-15-minute flow, representative problem diagnosis/recovery, settings round-trip, non-color problem communication, tutorial/presentation checksum invariance, and both Windows and macOS smoke runs.

The gate records implementation readiness separately from physical/runtime evidence. Missing Unity, hardware, audio-device, render, input, or cross-platform evidence remains `unverified` and causes the gate to remain open rather than being inferred from source or fixture presence.

No local or self-hosted Unity verification was performed while implementing this gate.
