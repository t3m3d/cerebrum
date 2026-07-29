# Full-stack integration review

This procedure joins Found, Cerebrum, Wallpaperbank, Parietal, Medulla, Thalamus,
Windows Cortex, and Snip without changing the registered Windows shell. Use the read-only
preflight at any time. Reserve the interactive phase for a review window because
Parietal and Medulla register AppBars and Windows may resize maximized application
windows when the work area changes.

## Phase A: read-only preflight

Build Cerebrum, then run:

    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Release

The preflight:

- uses Cerebrum's production component resolver and treats an unfinished Found build as optional;
- requires the broker, native Krypton wallpaper, Parietal, Medulla, Thalamus, Windows Cortex, and Snip artifacts;
- verifies x64 PE headers for every artifact plus .NET 8 runtime and dependency manifests for managed components;
- verifies sibling builds remain inside their declared repositories;
- does not start any component process or create any window;
- does not register an AppBar, hotkey, WinEvent hook, or named-pipe server.

A successful result ends with
`FULL_STACK_PREFLIGHT_OK components=8 launched=0`.

## Phase B: interactive compatibility review

Before starting:

1. Save active work and close disposable test documents.
2. Keep Explorer and the Windows taskbar running.
3. Confirm no prior Found, Cerebrum, Krypton wallpaper, Parietal, Medulla, Thalamus, Cortex, or Snip test process remains.
4. Use ordinary user integrity; do not run any component as administrator.
5. Prefer one monitor for the first pass. Add mixed-DPI monitors only after the
   basic lifecycle is clean.

Review in this order:

1. Start `Cerebrum.Host.exe`.
2. Confirm Cerebrum Host remains headless while Wallpaperbank is enabled; no invisible dashboard should cover desktop icons.
3. Confirm the Krypton wallpaper is visible and animated on its current secondary-display target.
4. Confirm Found is observed separately, Broker/Cortex/Snip are available on demand,
   and Wallpaperbank, Parietal, Medulla, and Thalamus are running.
5. Confirm Parietal owns the top global-menu/status strip and Medulla owns only the bottom dock.
6. Confirm the Medulla dock appears and its bottom AppBar reservation does not
   cover maximized application content. The Explorer taskbar intentionally
   remains visible in compatibility mode.
7. Invoke **Show Thalamus Overview**, dismiss it with Escape, and confirm no
   second Thalamus process remains.
8. Invoke **Open Cortex** and confirm Windows Cortex opens the user profile.
9. Invoke **Capture with Snip**, select a disposable region, and cancel or discard the result.
9. Exit and restart one disposable sibling component at a time to inspect the
   bounded Cerebrum restart policy.
10. Attach or detach a display only after saving work, then verify the desktop
   surfaces and Medulla reservations are rebuilt correctly.

## Shutdown and recovery

Exit Cerebrum from its desktop action. Compatibility-mode host shutdown leaves
the independent sibling applications running by design.

Then:

- stop the Krypton wallpaper with `wallpaperbank\krypton-lang\dist\krypton-wallpaper-stop.exe`;
- exit Parietal from its menu or process-specific recovery action;
- stop Thalamus with `Thalamus.exe --exit`;
- quit Medulla from its own menu so it removes its AppBar reservation;
- exit Cortex from its tray command if closing its window leaves it resident.

If a component becomes unusable, end only that named component in Task Manager.
Never end Explorer as part of this review. Cerebrum makes no Winlogon or shell
registry change, so Explorer remains the immediate recovery environment.
