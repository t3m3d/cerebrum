# Cerebrum
The Cerebral Desktop Environment for Windows.

## Component map

Cerebrum is the complete desktop environment. Its major components remain independent applications so each one can be built, tested, updated, or recovered without taking down the others.

| Component | Role | Responsibility |
| --- | --- | --- |
| **Cerebrum** | Desktop environment | The overall product, shared visual language, integration contracts, and future optional shell experience. |
| **Medulla** | Taskbar and dock | Running applications, pinned apps, launcher search, clock, calendar, AppBar work-area reservation, and taskbar personalization. |
| **Cortex** | File manager | File browsing and file operations, exposed to the other components only through its command-line interface. |
| **Thalamus** | Window and workspace manager | Window overview, workspaces, tiling, snap layouts, placement, and Mission Control-style navigation. |

```text
Cerebrum desktop environment
├── Medulla   taskbar, dock, launcher, and status surfaces
├── Cortex    file manager
└── Thalamus  window management, workspaces, and overview
```

Medulla, Cortex, and Thalamus do not reference one another's assemblies. Cross-component actions use documented command-line and process boundaries, allowing every component to remain independently runnable.

## What exists now

This repository contains the first runnable Cerebrum desktop/session foundation. It is intentionally a compatibility-mode build: Explorer remains installed and running while Cerebrum displays its own desktop surfaces and coordinates the other applications.

The current host:

- creates one DPI-aware desktop surface per connected monitor;
- discovers development builds, installed builds, explicit component paths, or executables on PATH;
- starts and supervises Medulla and Thalamus;
- keeps the private Broker and Cortex cold until a capability or file window needs them;
- restarts session components after unexpected exits, with a bounded restart budget;
- exposes desktop actions for Cortex, Thalamus, Medulla, session repair, and host exit;
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

The desktop surface lives in this repository because it is part of the Cerebrum session. Medulla, Thalamus, and Cortex remain sibling repositories because they are useful and recoverable as independent applications.

## Runtime shape

    Windows logon session
    ├── Windows DWM, input, security, and core services
    ├── Explorer                         retained during compatibility mode
    └── Cerebrum.Host.exe               one instance per user/session
        ├── Cerebrum desktop window     one per monitor
        ├── Cerebrum.Broker.exe         launched only for protected integration
        ├── Medulla.exe                 independent taskbar/dock application
        ├── Thalamus.exe                independent window/workspace application
        └── Cortex.exe                  launched only when file work is requested

Cerebrum does not load assemblies from the three sibling applications. A component can be rebuilt, updated, exited, or recovered without loading its code into the host process.

## Responsibilities and boundaries

### Cerebrum Host

Cerebrum Host owns the desktop session policy. It decides which optional components should be available, reports their health, and performs bounded restart attempts. It does not own file operations, window tiling, taskbar rendering, package installation, authentication, or composition.

Closing Cerebrum stops its private broker if it was needed and ends supervision. Medulla, Thalamus, and Cortex remain independent processes; the host does not forcibly terminate them.

### Cerebrum Desktop

The desktop project owns wallpaper presentation, desktop actions, display-specific surfaces, and session-health presentation. Desktop icons, drag/drop, widgets, saved per-monitor layouts, and context menus are planned increments.

### Cerebrum Broker

The broker is deliberately a separate process but remains stopped while idle. Before a protected Windows-integration provider is used, the host starts and health-checks it through its version-one protocol:

- health;
- capabilities;
- graceful shutdown.

Future shell, AppX, COM, WinRT, icon, thumbnail, and notification integrations belong behind this deadline-controlled boundary. If a future integration hangs, Cerebrum must be able to abandon and replace the broker without closing the desktop.

### Medulla

Medulla owns the taskbar/dock, running-application buttons, pinned applications, launcher, AppBar work-area reservations, clock, calendar, and taskbar appearance. Cerebrum starts it as a process and does not read or write Medulla settings.

### Thalamus

Thalamus owns window overview, tiling, snap geometry, monitor moves, and named layouts. Cerebrum uses the documented Thalamus command line; it does not use Thalamus assemblies or persistence files.

### Cortex

Cortex owns file browsing, search, archives, previews, filesystem mutations, and undo. Cerebrum opens the user's home folder through the Cortex command-line boundary. Cortex is not started merely because a Cerebrum session begins.

## Requirements

- Windows 10 or Windows 11, x64.
- .NET 8 SDK for development.
- .NET 8 Windows Desktop Runtime for the framework-dependent build.
- Sibling Medulla, Thalamus, and Cortex builds are optional during early development; missing components are reported on the desktop instead of crashing the host.

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
- sibling-repository discovery, including the Windows `cortex-win` boundary;
- repository-root discovery;
- complete desktop performance-profile validation and resource-budget comparison;
- broker protocol serialization and its closed command list.

The read-only full-stack preflight can run while other desktop work is in progress:

    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\full-stack-preflight.ps1 -Configuration Release

It uses Cerebrum's production resolver to locate the broker, Medulla, Thalamus, and
Windows Cortex builds. It reads their PE and .NET manifests to require x64 .NET 8
artifacts, verifies sibling paths remain inside the expected repositories, and
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

The interactive smoke test briefly displays the desktop with Medulla and Thalamus disabled, invokes the accessible Exit action, verifies the on-demand broker remains cold, and removes its isolated temporary data:

    pwsh -NoProfile -File tests\desktop-smoke.ps1 -Configuration Debug
    pwsh -NoProfile -File tests\desktop-smoke.ps1 -Configuration Release

Run interactive integration only during a review window; see [Full-stack integration review](docs/integration-review.md).

## Safe first run

1. Keep Explorer and the normal Windows taskbar running.
2. Build Cerebrum, Medulla, Thalamus, and Cortex in Debug.
3. Start Cerebrum.Host.exe from the output path above.
4. Confirm one Cerebrum surface appears on every monitor.
5. Confirm the health panel reports the Broker and Cortex available on demand, with Medulla and Thalamus running.
6. Use Open Cortex, Show Thalamus Overview, and Ensure Medulla Is Running.
7. Attach or detach a display and confirm the desktop surfaces rebuild.
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
      "StartMedulla": true,
      "StartThalamus": true,
      "RestartSessionComponents": true,
      "RestartLimit": 3,
      "Components": {
        "Broker": null,
        "Medulla": null,
        "Thalamus": null,
        "Cortex": null
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
| CEREBRUM_BROKER_PATH | Cerebrum.Broker.exe |
| CEREBRUM_MEDULLA_PATH | Medulla.exe |
| CEREBRUM_THALAMUS_PATH | Thalamus.exe |
| CEREBRUM_CORTEX_PATH | Cortex.exe |

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
- Theme synchronization between repositories is not versioned yet.
- Medulla notification-area compatibility and Cerebrum quick settings are not implemented yet.
- The broker contains the health boundary but no risky Windows capability providers yet.
- Releases are not packaged or code-signed.
- The host does not yet forward a second launch to the existing desktop.
- Physical mixed-DPI monitor testing and accessibility automation remain required.

## Roadmap

### Phase 1 — Desktop foundation

- Desktop file and shortcut model.
- Per-monitor icon layout.
- Wallpaper picker and theme settings.
- Desktop selection, keyboard navigation, drag/drop, and context menus.
- Versioned theme exchange shared by value, not shared writable files.

### Phase 2 — System surfaces

- Quick settings for audio, network, Bluetooth, battery, displays, and power.
- Notification center and do-not-disturb state.
- Notification-area compatibility strategy for applications that expect the Windows shell.
- Broker providers for AppX, COM, WinRT, icons, thumbnails, and package metadata.

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
