# P9-02 release UX evidence gate

P9-02A/B/C implementation being merged is not itself a release UX pass. `P9ReleaseUxGate` requires evidence from the same commit for the first-15-minute flow, representative problem diagnosis/recovery, settings round-trip, non-color problem communication, tutorial/presentation checksum invariance, and both Windows and macOS smoke runs.

Local or hardware-dependent Unity verification was not performed while implementing the gate. Missing platform evidence remains explicitly unverified rather than being represented as a pass.
