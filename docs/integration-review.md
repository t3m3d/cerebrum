# Full-stack integration review

This procedure joins Cerebrum, Medulla, Thalamus, and Windows Cortex without
changing the registered Windows shell. Use the read-only preflight at any time.
Reserve the interactive phase for a review window because Medulla registers
AppBars and Windows may resize maximized application windows when the work area
changes.

## Phase A: read-only preflight

Build Cerebrum, then run:

    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Release

The preflight:

- uses Cerebrum's production component resolver;
- requires the broker, Medulla, Thalamus, and Windows Cortex artifacts;
- verifies x64 PE headers plus .NET 8 runtime and dependency manifests;
- verifies sibling builds remain inside their declared repositories;
- does not start any component process or create any window;
- does not register an AppBar, hotkey, WinEvent hook, or named-pipe server.

A successful result ends with
`FULL_STACK_PREFLIGHT_OK components=4 launched=0`.

## Phase B: interactive compatibility review

Before starting:

1. Save active work and close disposable test documents.
2. Keep Explorer and the Windows taskbar running.
3. Confirm no prior Cerebrum, Medulla, Thalamus, or Cortex test process remains.
4. Use ordinary user integrity; do not run any component as administrator.
5. Prefer one monitor for the first pass. Add mixed-DPI monitors only after the
   basic lifecycle is clean.

Review in this order:

1. Start `Cerebrum.Host.exe`.
2. Confirm one non-activating Cerebrum desktop surface per monitor.
3. Confirm the health surface reports the Broker and Cortex available on demand,
   with Medulla and Thalamus running.
4. Confirm the Medulla dock appears and its bottom AppBar reservation does not
   cover maximized application content. The Explorer taskbar intentionally
   remains visible in compatibility mode.
5. Invoke **Show Thalamus Overview**, dismiss it with Escape, and confirm no
   second Thalamus process remains.
6. Invoke **Open Cortex** and confirm Windows Cortex opens the user profile.
7. Exit and restart one disposable sibling component at a time to inspect the
   bounded Cerebrum restart policy.
8. Attach or detach a display only after saving work, then verify the desktop
   surfaces and Medulla reservations are rebuilt correctly.

## Shutdown and recovery

Exit Cerebrum from its desktop action. Compatibility-mode host shutdown leaves
the independent sibling applications running by design.

Then:

- stop Thalamus with `Thalamus.exe --exit`;
- quit Medulla from its own menu so it removes its AppBar reservation;
- exit Cortex from its tray command if closing its window leaves it resident.

If a component becomes unusable, end only that named component in Task Manager.
Never end Explorer as part of this review. Cerebrum makes no Winlogon or shell
registry change, so Explorer remains the immediate recovery environment.
