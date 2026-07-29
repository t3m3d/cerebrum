# Component contracts

## Rule of separation

Found, Cerebrum, Wallpaperbank, Parietal, Medulla, Thalamus, Cortex, and Snip are independently built applications. They do not reference each other's assemblies and do not edit each other's settings.

Cross-component behavior uses one of:

1. a documented executable command;
2. a separately versioned local IPC message;
3. a read-only, versioned data export.

A source-level interface in one repository is not a cross-component contract.
## Found to Cerebrum

Executable:

    found.exe --shell

Found is the outer session supervisor. The user selects a `Cerebrum.Host.exe` build in Found, then Found launches and watches that process. Found may restart a nonzero Cerebrum exit within its own bounded policy and always exposes an Explorer recovery action.

Cerebrum treats Found as `ExternalSupervisor`:

- observe whether a same-session Found process is running;
- report whether a Found build is available;
- never launch, restart, configure, or shut down Found;
- never register Found as the Windows shell.

This one-way ownership prevents Found and Cerebrum from restarting each other.


## Cerebrum to Wallpaperbank

Current executable:

    wallpaperbank\krypton-lang\dist\krypton-wallpaper.exe

Current use:

- start the executable with no arguments;
- rely on its single-instance replacement behavior;
- render it beneath Cerebrum's translucent desktop surfaces;
- monitor and restart only a copy launched by Cerebrum.

The current Krypton build targets the horizontal secondary display. Dynamic monitor identity, work-area changes, reduced motion, and a graceful shutdown command remain future contract work.

## Cerebrum to Parietal

Executable:

    Parietal.exe

Current use:

- start Parietal with no arguments;
- rely on its session single-instance boundary;
- observe only process availability and lifecycle.

Parietal owns the global menu, notification area, clock, status indicators, and top AppBar reservation. Medulla must not duplicate those surfaces.

## Cerebrum to Medulla

Executable:

    Medulla.exe

Current use:

- start Medulla with no arguments;
- rely on Medulla's user-and-session single-instance handling;
- observe only whether the process could be started or a Medulla process already exists.

Cerebrum does not currently request Medulla shutdown. Exit Medulla from its own user interface. While Cerebrum supervision is active, an unexpected Medulla exit can trigger a bounded restart.

Future Medulla integration should add a documented command/IPC surface for health, graceful shutdown, appearance refresh, and taskbar mode. It must not expose Medulla's internal window tracker or settings classes.

## Cerebrum to Thalamus

Executable:

    Thalamus.exe

Commands currently used by Cerebrum:

    thalamus.exe --overview

Other documented Thalamus commands available to future Cerebrum actions:

    thalamus.exe --tile-active left
    thalamus.exe --tile-active right
    thalamus.exe --tile-active top
    thalamus.exe --tile-active bottom
    thalamus.exe --tile-active maximize
    thalamus.exe --tile-active restore
    thalamus.exe --save-layout NAME
    thalamus.exe --restore-layout NAME
    thalamus.exe --exit

Thalamus owns validation, single-instance forwarding, window eligibility, placement, layouts, and error reporting. Cerebrum must not reproduce those policies.

Adjacent Windows workspace commands may report unsupported because Microsoft does not currently document stable adjacent virtual-desktop enumeration and switching. Cerebrum must preserve that capability fallback.

## Cerebrum to Cortex

Executable:

    Cortex.exe

Supported Cortex desktop-helper contract:

    cortex.exe
    cortex.exe --open PATH
    cortex.exe --reveal PATH
    cortex.exe --select PATH
    cortex.exe --pin PATH
    cortex.exe --pane left --open PATH
    cortex.exe --pane right --open PATH

A positional path remains compatible with Cortex, but Cerebrum should use the explicit action forms.

Cerebrum currently opens the user's home directory with:

    cortex.exe --open USER_HOME

Rules:

- pass every path as an individual ProcessStartInfo.ArgumentList element;
- do not interpolate a path into a shell command string;
- do not connect to Cortex's private activation pipe;
- do not load Cortex assemblies;
- do not edit Cortex settings;
- do not wait for the primary Cortex process to exit;
- keep all file mutations inside Cortex's file-operation service.

## Cerebrum to Snip

Snip is an on-demand screenshot and annotation application. Cerebrum resolves `Snip.exe` but does not keep it resident.

Documented commands:

    Snip.exe --capture=region
    Snip.exe --capture=window
    Snip.exe --capture=fullscreen

Cerebrum currently exposes region capture. Snip owns capture overlays, DPI conversion, annotation, clipboard handling, and export. Cerebrum passes arguments through `ProcessStartInfo.ArgumentList` and does not request elevation.

## Shared command construction

Cerebrum keeps its outbound Cortex, Thalamus, and Snip argument lists in
`Cerebrum.Core.Components.ComponentCommands`. The host passes every argument
through `ProcessStartInfo.ArgumentList`; it never constructs a shell command.

The Cortex open contract rejects relative paths before process creation. The
Thalamus overview contract contains only `--overview`. Deterministic tests pin
these exact argument lists so UI and supervision changes cannot silently drift
from the sibling applications' documented command surfaces.

## Host to broker

Protocol version 1 has no active Windows-integration provider, so the Broker is
resolved but not launched at ordinary session startup. A provider must start and
health-check it before sending the first request.

Transport:

- local named pipe;
- one private pipe per Windows user and Windows session;
- current-user-only pipe option;
- UTF-8 newline-delimited JSON;
- maximum 4,096 characters per message;
- three-second broker read deadline;
- bounded client connection/request deadline.

Request schema:

    {
      "version": 1,
      "requestId": "random-id",
      "command": "health"
    }

Response schema:

    {
      "version": 1,
      "requestId": "random-id",
      "success": true,
      "status": "healthy",
      "capabilities": null
    }

Protocol version 1 commands:

| Command | Successful status | Purpose |
| --- | --- | --- |
| health | healthy | Confirms the broker process and protocol are responsive |
| capabilities | capabilities | Returns the broker's current fixed capability list |
| shutdown | shutting-down | Acknowledges graceful host-requested shutdown |

Unknown versions, missing IDs, oversized messages, and unknown commands fail closed. The broker never executes an arbitrary process or accepts a path in protocol version 1.

## Component status contract inside Cerebrum

The host emits immutable status records to the desktop:

    ComponentStatus(
      Id,
      DisplayName,
      State,
      Detail,
      ChangedAt)

Allowed states are Unknown, Missing, Starting, Running, Stopped, and Failed.

Detail text is presentation-safe and does not contain executable paths or command arguments. Diagnostics use an even smaller stable event-code vocabulary.

## Executable path configuration

Settings properties:

    Components.Found
    Components.Broker
    Components.Wallpaper
    Components.Parietal
    Components.Medulla
    Components.Thalamus
    Components.Cortex
    Components.Snip

Environment variables:

    CEREBRUM_FOUND_PATH
    CEREBRUM_BROKER_PATH
    CEREBRUM_WALLPAPER_PATH
    CEREBRUM_PARIETAL_PATH
    CEREBRUM_MEDULLA_PATH
    CEREBRUM_THALAMUS_PATH
    CEREBRUM_CORTEX_PATH
    CEREBRUM_SNIP_PATH

All explicit paths must be fully qualified. Invalid paths do not fall back to treating the current directory as trusted input.

## Contract evolution

Every future IPC contract must specify:

- version and compatibility policy;
- maximum message and field sizes;
- authentication/identity boundary;
- connection, read, operation, and write deadlines;
- cancellation behavior;
- stable error/status values;
- whether a command is idempotent;
- privacy rules;
- behavior when the target component is missing or older;
- shutdown and resource-cleanup behavior.

A new capability must be optional until every supported component version can degrade safely without it.
