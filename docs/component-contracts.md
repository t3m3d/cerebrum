# Component contracts

## Rule of separation

Cerebrum, Medulla, Thalamus, and Cortex are independently built applications. They do not reference each other's assemblies and do not edit each other's settings.

Cross-component behavior uses one of:

1. a documented executable command;
2. a separately versioned local IPC message;
3. a read-only, versioned data export.

A source-level interface in one repository is not a cross-component contract.

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

## Shared command construction

Cerebrum keeps its outbound Cortex and Thalamus argument lists in
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

    Components.Broker
    Components.Medulla
    Components.Thalamus
    Components.Cortex

Environment variables:

    CEREBRUM_BROKER_PATH
    CEREBRUM_MEDULLA_PATH
    CEREBRUM_THALAMUS_PATH
    CEREBRUM_CORTEX_PATH

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
