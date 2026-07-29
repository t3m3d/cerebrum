# Cerebrum
The Cerebral Desktop Environment for Windows.

## Component map

Cerebrum is the complete desktop environment. Its major components remain independent applications so each one can be built, tested, updated, or recovered without taking down the others.

| Component | Role | Responsibility |
| --- | --- | --- |
| **Found** | Session supervisor and recovery shell | Starts Cerebrum, watches its lifetime, and provides the explicit route back to Windows Explorer during shell-mode testing. |
| **Cerebrum** | Desktop/session host | Owns the environment, shared visual language, integration contracts, health, recovery, and future optional shell experience. |
| **Wallpaperbank** | Wallpaper layer | Supplies independently runnable visual themes. The current `krypton-lang` theme is a native Krypton-authored animated desktop. |
| **Parietal** | Top status and global-menu bar | Owns the global application menu, notification area, clock, status indicators, and top work-area reservation. |
| **Medulla** | Dock and launcher | Owns running applications, pinned apps, launcher search, and the bottom work-area reservation. |
| **Thalamus** | Window and workspace manager | Owns window overview, workspaces, tiling, snap layouts, placement, and Mission Control-style navigation. |
| **Cortex** | File manager | Owns file browsing and operations through its command-line/process boundary. |
| **Snip** | Screenshot utility | Provides region, window, and full-desktop capture, annotation, clipboard copy, and export through an on-demand executable. |

```text
Cerebrum desktop environment
├── Wallpaperbank/krypton-lang  native animated background
├── Parietal                    global menu, tray, clock, and top status bar
├── Medulla                     dock, running apps, pins, and launcher
├── Thalamus                    window management, workspaces, and overview
├── Cortex                      file manager
└── Snip                        planned screenshot and annotation utility
```

The component repositories do not reference one another's assemblies. Cross-component actions use documented command-line or IPC boundaries, allowing each layer to remain independently runnable and recoverable.

## What exists now

This repository contains the first runnable Cerebrum desktop/session foundation. It is intentionally a compatibility-mode build: Explorer remains installed and running while Cerebrum displays its own desktop surfaces and coordinates the other applications.

The current host:

- runs without a competing desktop window while Wallpaperbank is enabled; when disabled, creates one DPI-aware fallback surface per monitor;
- discovers development builds, installed builds, explicit component paths, or executables on PATH;
- starts and supervises the Krypton wallpaper, Parietal, Medulla, and Thalamus;
- keeps the private Broker, Cortex, and Snip cold until their capabilities are requested;
- observes Found as an external supervisor and never launches it back from Cerebrum;
- restarts session components after unexpected exits, with a bounded restart budget;
- exposes desktop actions for Cortex, Thalamus, Snip, Medulla, session repair, and host exit;
- persists validated settings with atomic replacement and backup recovery;
- records privacy-preserving diagnostic event codes;
- reacts to display-topology changes by rebuilding the desktop surfaces;
- leaves Explorer, DWM, Winlogon, UAC, and the secure desktop untouched.

This is a foundation, not shell-replacement mode. It does not hide the Windows taskbar, modify Winlogon, replace Explorer, host third-party notification icons, or take ownership of Windows authentication.

## Repository projects

| Project | Output | Purpose |
| --- | --- | --- |
| **Cerebrum.Core** | Library | Non-UI component catalog, settings, discovery, broker protocol, and desktop performance policy |
| **Cerebrum.Desktop** | WPF library | Desktop window, monitor surface, component-health presentation, clock, and user actions |
| **Cerebrum.Host** | Windows executable | Single session owner, desktop creation, process supervision, restart policy, diagnostics, and cross-component launch behavior |
| **Cerebrum.Broker** | Background executable | Replaceable out-of-process boundary for Windows APIs that must never be allowed to hang the desktop |
| **Cerebrum.Tests** | Console test runner | Dependency-free deterministic tests for the Core policies and contracts |

The desktop surface lives in this repository because it is part of the Cerebrum session. Wallpaperbank, Parietal, Medulla, Thalamus, Cortex, and the planned Snip utility remain sibling repositories because they are independently useful and recoverable.

## Runtime shape

    Windows logon session
    ├── Windows DWM, input, security, and core services
    ├── Explorer                         retained during compatibility mode
    └── Cerebrum.Host.exe               one instance per user/session
        ├── Cerebrum desktop window     fallback only when Wallpaperbank is disabled
        ├── krypton-wallpaper.exe       native animated background
        ├── Parietal.exe                top status/global-menu bar
        ├── Medulla.exe                 bottom dock and launcher
        ├── Thalamus.exe                window/workspace manager
        ├── Cerebrum.Broker.exe         launched only for protected integration
        └── Cortex.exe                  launched only when file work is requested

`Snip.exe` will join the on-demand branch after the `snip` repository defines and builds it. Cerebrum never loads sibling assemblies into the Host; every component can be rebuilt, restarted, or recovered across a process boundary.

## Responsibilities and boundaries

### Found

Found is the outer session supervisor. In compatibility testing it runs as `found.exe --shell`, launches `Cerebrum.Host.exe`, watches the host exit code, and keeps an Explorer recovery action reachable with `Ctrl+Shift+F12`. Cerebrum reports Found's presence but never starts or restarts it, preventing a circular supervision loop.

Found is under construction and is not yet registered as the Windows shell. Explorer remains the supported shell and recovery environment.

### Cerebrum Host

Cerebrum Host owns the desktop session policy. It decides which optional components should be available, reports their health, and performs bounded restart attempts. It does not own file operations, window tiling, taskbar rendering, package installation, authentication, or composition.

Closing Cerebrum stops its private broker if it was needed and ends supervision. Wallpaperbank, Parietal, Medulla, Thalamus, and Cortex remain independent processes; the host does not forcibly terminate them.

### Cerebrum Desktop

The desktop project owns fallback display-specific host surfaces, desktop actions, and session-health presentation. Those surfaces are not created while Wallpaperbank is enabled, preventing an invisible input layer from covering Explorer icons. Desktop icons, drag/drop, widgets, saved per-monitor layouts, and context menus are planned increments.

### Cerebrum Broker

The broker is deliberately a separate process but remains stopped while idle. Before a protected Windows-integration provider is used, the host starts and health-checks it through its version-one protocol:

- health;
- capabilities;
- graceful shutdown.

Future shell, AppX, COM, WinRT, icon, thumbnail, and notification integrations belong behind this deadline-controlled boundary. If a future integration hangs, Cerebrum must be able to abandon and replace the broker without closing the desktop.

### Wallpaperbank

Wallpaperbank owns desktop artwork and ambient interaction only. The current `krypton-lang/dist/krypton-wallpaper.exe` is a native, click-through Krypton process targeting the secondary display. Cerebrum discovers it from Wallpaperbank's `dist` directory, starts it with the session, and suppresses its own fallback desktop surface so the wallpaper and Explorer icons remain visible and clickable.

### Parietal

Parietal owns the global application menu, system tray, clock, status indicators, and top AppBar reservation. Cerebrum starts and supervises it as a separate process; Parietal failure must not remove the wallpaper or dock.

### Medulla

Medulla owns the bottom dock, running-application buttons, pinned applications, launcher, and bottom AppBar reservation. Clock, tray, and global-menu ownership belongs to Parietal. Cerebrum starts Medulla as a process and does not read or write Medulla settings.

### Thalamus

Thalamus owns window overview, tiling, snap geometry, monitor moves, and named layouts. Cerebrum uses the documented Thalamus command line; it does not use Thalamus assemblies or persistence files.

### Cortex

Cortex owns file browsing, search, archives, previews, filesystem mutations, and undo. Cerebrum opens the user's home folder through the Cortex command-line boundary. Cortex is not started merely because a Cerebrum session begins.

### Snip

Snip owns screenshot capture, annotation, clipboard copy, and export as an on-demand process. Cerebrum resolves `Snip.exe` from `../snip` and launches region capture with `--capture=region`; window and full-desktop modes remain available through the same documented command boundary.

## Requirements

- Windows 10 or Windows 11, x64.
- .NET 8 SDK for development.
- .NET 8 Windows Desktop Runtime for the framework-dependent build.
- Sibling Wallpaperbank, Parietal, Medulla, Thalamus, and Cortex builds are optional during development; missing components become status/log events instead of crashing the host.

No third-party NuGet packages are used. The repository-local NuGet configuration keeps restore deterministic and offline-friendly.

## Build

From the repository root:

    dotnet restore Cerebrum.sln --configfile NuGet.Config -p:Platform=x64
    dotnet build Cerebrum.sln -c Debug -p:Platform=x64 --no-restore
    dotnet build Cerebrum.sln -c Release -p:Platform=x64 --no-restore

Debug host output:

    src\Cerebrum.Host\bin\x64\Debug\net8.0-windows\win-x64\Cerebrum.Host.exe

Release host output:

    src\Cerebrum.Host\bin\x64\Release\net8.0-windows\win-x64\Cerebrum.Host.exe

## Automated verification

After a Debug build:

    dotnet tests\Cerebrum.Tests\bin\x64\Debug\net8.0\Cerebrum.Tests.dll

The test runner covers:

- component ownership and startup policy;
- settings semantic validation;
- atomic persistence and backup recovery;
- absolute data-root enforcement;
- explicit executable discovery priority;
- exact Cortex and Thalamus command construction;
- sibling-repository discovery, including Wallpaperbank `dist`, Parietal, and the Windows `cortex-win` boundary;
- repository-root discovery;
- complete desktop performance-profile validation and resource-budget comparison;
- broker protocol serialization and its closed command list.

The read-only full-stack preflight can run while other desktop work is in progress:

    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Release

It uses Cerebrum's production resolver to locate the broker, native Krypton
wallpaper, Parietal, Medulla, Thalamus, and Windows Cortex builds. It validates
x64 PE architecture for every component and .NET 8 manifests for managed
components, verifies sibling paths remain inside the expected repositories, and
proves that no component process was launched. It does not create a desktop
surface, register an AppBar, show an overview, install a hotkey, or open a file window.

The read-only performance harness records a declared stock, compatibility, or
future Lite process profile without launching or stopping any component:

    pwsh -NoProfile -File tests\performance-capture.ps1 -Configuration Release -Profile Stock -DurationSeconds 30

After equivalent stock and candidate snapshots exist, the policy comparator
requires a complete candidate session, at least 10% lower private memory, bounded
idle CPU, and bounded handles. See [Desktop performance policy](docs/performance.md).

    pwsh -NoProfile -File tests\performance-compare.ps1 -Configuration Release -BaselinePath artifacts\performance\stock.json -CandidatePath artifacts\performance\lite.json

A standalone broker health check is also available:

    src\Cerebrum.Broker\bin\x64\Debug\net8.0-windows\win-x64\Cerebrum.Broker.exe --health

The live named-pipe health and graceful-shutdown flow has a reusable smoke test:

    pwsh -NoProfile -File tests\broker-smoke.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\broker-smoke.ps1 -Configuration Release

The interactive smoke test briefly displays the desktop with Wallpaperbank, Parietal, Medulla, and Thalamus disabled, invokes the accessible Exit action, verifies the on-demand broker remains cold, and removes its isolated temporary data:

    pwsh -NoProfile -File tests\desktop-smoke.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\desktop-smoke.ps1 -Configuration Release

Run interactive integration only during a review window; see [Full-stack integration review](docs/integration-review.md).

## Safe first run

1. Keep Explorer and the normal Windows taskbar running.
2. Build Cerebrum, Parietal, Medulla, Thalamus, and Cortex; build Wallpaperbank's `krypton-lang` executable.
3. Start Cerebrum.Host.exe from the output path above.
4. Confirm the Krypton animation is visible on its current secondary-display target and no Cerebrum dashboard covers the wallpaper or Explorer icons.
5. Confirm Wallpaperbank, Parietal, Medulla, and Thalamus are running in the current session; Broker and Cortex remain available on demand. The visual health panel is a wallpaper-disabled fallback.
6. Use Open Cortex, Show Thalamus Overview, and Ensure Medulla Is Running.
7. Attach or detach a display and inspect Parietal, Medulla, and Wallpaperbank placement. Wallpaperbank still requires its own dynamic-monitor update.
8. Choose Exit Cerebrum desktop. Explorer remains the recovery environment.

If the host becomes unusable, end Cerebrum.Host.exe only in Task Manager. Do not end Explorer. The current build makes no persistent shell change, so recovery requires no registry repair.

## Settings and local data

Cerebrum stores its own data beneath:

    %LOCALAPPDATA%\Cerebrum\Desktop\
      settings.json
      settings.json.bak
      logs\host.log

A controlled test can use the CEREBRUM_DATA_ROOT environment variable, but the value must be an absolute path. Invalid relative overrides are rejected.

Default settings:

    {
      "Version": 1,
      "ThemePreset": "Cerebrum",
      "AccentColor": "#7C8CFF",
      "WallpaperPath": null,
      "StartWallpaper": true,
      "StartParietal": true,
      "StartMedulla": true,
      "StartThalamus": true,
      "RestartSessionComponents": true,
      "RestartLimit": 3,
      "Components": {
        "Found": null,
        "Broker": null,
        "Wallpaper": null,
        "Parietal": null,
        "Medulla": null,
        "Thalamus": null,
        "Cortex": null,
        "Snip": null
      }
    }

Component paths and wallpaper paths must be absolute. Unknown versions, invalid colors, unsupported themes, relative paths, and unsafe restart limits fail semantic validation. Each application owns its own settings; never point multiple components at the same writable JSON file.

## Component discovery

The host resolves each executable in this order:

1. Absolute path in Cerebrum settings.
2. Component-specific environment variable.
3. Installed location beneath Local AppData.
4. A recent Debug or Release build in the expected sibling repository.
5. The current PATH.

Environment overrides:

| Variable | Component |
| --- | --- |
| CEREBRUM_FOUND_PATH | found.exe |
| CEREBRUM_BROKER_PATH | Cerebrum.Broker.exe |
| CEREBRUM_WALLPAPER_PATH | krypton-wallpaper.exe |
| CEREBRUM_PARIETAL_PATH | Parietal.exe |
| CEREBRUM_MEDULLA_PATH | Medulla.exe |
| CEREBRUM_THALAMUS_PATH | Thalamus.exe |
| CEREBRUM_CORTEX_PATH | Cortex.exe |
| CEREBRUM_SNIP_PATH | Snip.exe |

Discovery reports only its source category to the desktop and diagnostics. Personal paths and document names are not written to diagnostic logs.

## Safety contract

- Never rename or impersonate explorer.exe.
- Never modify or delete WindowsApps directly.
- Never kill, restart, or hide Explorer automatically.
- Never replace DWM, Winlogon, Credential UI, UAC secure desktop, or Windows input.
- Never perform shell, package, icon, thumbnail, or filesystem discovery synchronously on the WPF dispatcher.
- Treat sibling applications as separate processes with versioned CLI or IPC boundaries.
- Give private broker requests a deadline and a fixed maximum message size.
- Keep settings writes same-volume, flushed, replaceable, and recoverable.
- Limit automatic restart attempts so a broken component cannot create an infinite crash loop.
- Keep diagnostics local and exclude titles, paths, filenames, command arguments, and content.

## Current limitations

- Explorer remains visible and is still the registered Windows shell.
- The Cerebrum desktop is a compatibility surface rather than an Explorer desktop-host replacement.
- Desktop files, icon placement, drag/drop, widgets, and desktop context menus are not implemented yet.
- Wallpaperbank's current Krypton scene targets the horizontal secondary display and does not yet react dynamically to all monitor/work-area changes.
- Found remains an observe-only compatibility component until its shell registration, health handshake, signing, and rollback path are complete.
- Theme synchronization between repositories is not versioned yet.
- Parietal notification-area compatibility and Cerebrum quick settings remain incomplete.
- The broker contains the health boundary but no risky Windows capability providers yet.
- Releases are not packaged or code-signed.
- The host does not yet forward a second launch to the existing desktop.
- Physical mixed-DPI monitor testing and accessibility automation remain required.

## Roadmap

### Phase 1 — Desktop foundation

- Desktop file and shortcut model.
- Per-monitor icon layout.
- Wallpaper picker, Wallpaperbank theme selection, and reduced-motion settings.
- Desktop selection, keyboard navigation, drag/drop, and context menus.
- Versioned theme exchange shared by value, not shared writable files.

### Phase 2 — System surfaces

- Quick settings for audio, network, Bluetooth, battery, displays, and power.
- Notification center and do-not-disturb state.
- Notification-area compatibility strategy for applications that expect the Windows shell.
- Broker providers for AppX, COM, WinRT, icons, thumbnails, and package metadata.
- Snip region/window/display capture, annotation, clipboard copy, and export contract.

### Phase 3 — Session and deployment

- Signed component manifests and capability negotiation.
- Installer, updater, rollback, and crash-loop recovery.
- Logon startup in compatibility mode.
- Explicit opt-in shell mode with a timed Return to Explorer path.
- Suspend/resume, lock/unlock, RDP, display attach/detach, and upgrade-cycle testing.

### Phase 4 — Cerebrum test image

A later private test image can combine an official Windows Evaluation image or a user-supplied licensed Windows ISO with an unattended provisioning layer. Windows ADK, DISM, and ISO tooling can install the signed Cerebrum components, configure compatibility mode, and optionally enable shell mode after preflight checks.

The image pipeline must:

- preserve a known-good Explorer recovery path;
- offer a documented keyboard and Safe Mode rollback;
- detect crash loops before relaunching Cerebrum;
- avoid redistributing Microsoft Windows outside its license;
- use signed, versioned release artifacts rather than development builds;
- test in a virtual machine before any spare physical machine.

Cerebrum is therefore deployable as a future Windows desktop image without becoming a fork of the Windows kernel or an executable named explorer.exe.

## More documentation

- [Architecture and lifecycle](docs/architecture.md)
- [Component and broker contracts](docs/component-contracts.md)
- [Full-stack integration review](docs/integration-review.md)
- [Shell-mode and test-image safety plan](docs/deployment-and-recovery.md)
- [Desktop performance policy](docs/performance.md)
