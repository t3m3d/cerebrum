# Cerebrum architecture

## Purpose

Cerebrum is the session and desktop layer of a modular Windows desktop environment. It coordinates independently runnable applications instead of merging their code into one failure domain.

The architectural priority is recoverability: losing the taskbar, window overview, file manager, or a Windows-integration broker must not corrupt the desktop settings or require a reboot.

## Process ownership

| Process | Owner | Starts automatically | Restarted by host | Writable data owner |
| --- | --- | --- | --- | --- |
| Cerebrum.Host.exe | Cerebrum | User starts it in compatibility mode | Windows deployment layer later | Cerebrum Desktop |
| Cerebrum.Broker.exe | Cerebrum Host | No; launched before protected integration | Yes, within restart budget | None in protocol version 1 |
| Medulla.exe | Medulla | Configurable, default yes | Yes while host supervises | Medulla |
| Thalamus.exe | Thalamus | Configurable, default yes | Yes while host supervises | Thalamus |
| Cortex.exe | Cortex | No; launched on demand | No | Cortex |

Cerebrum does not forcibly terminate the three independent sibling applications during ordinary host shutdown. This keeps compatibility-mode development safe and allows each application to keep its own documented lifecycle. A future shell session contract may add explicit graceful shutdown acknowledgments.

## Project dependency direction

    Cerebrum.Host ───────► Cerebrum.Desktop ───────► Cerebrum.Core
          │                                              ▲
          └──────────────────────────────────────────────┘
    Cerebrum.Broker ───────────────────────────────► Cerebrum.Core
    Cerebrum.Tests ────────────────────────────────► Cerebrum.Core

No Cerebrum project references Medulla, Thalamus, or Cortex. Their boundaries are process launches, documented arguments, and future separately versioned IPC messages.

Cerebrum.Core deliberately targets plain .NET 8. It contains no WPF types and no native Windows calls, which keeps settings, discovery policy, contracts, and restart decisions testable without creating a desktop window.

## Startup sequence

1. Acquire a user-and-Windows-session-qualified mutex.
2. Reject a second host instance without modifying the existing session.
3. Resolve the data root; reject relative overrides.
4. Start the asynchronous privacy-preserving diagnostic writer.
5. Load settings from the current JSON document, then its backup, then safe defaults.
6. Locate the Cerebrum repository when running a development build.
7. Create the executable resolver and private broker pipe name.
8. Enumerate monitors and display a non-activating desktop surface on each.
9. Resolve the Broker but leave it stopped until a protected integration needs it.
10. Ensure Medulla and Thalamus are running when enabled.
11. Resolve Cortex but leave it stopped until the user requests file work.
12. Subscribe to display changes and component exits.

The desktop is shown before sibling executable discovery completes so repository and filesystem work does not delay first presentation.

## Desktop surfaces

Each monitor receives a separate borderless WPF window. Native pixel bounds are applied after HWND creation, allowing correct negative monitor coordinates and Per-Monitor V2 placement without treating physical pixels as WPF device-independent units.

The windows use the tool-window and no-activate extended styles:

- they do not create taskbar entries;
- clicking a surface does not steal foreground activation from a working application;
- buttons remain available as mouse-driven desktop actions;
- Windows continues to own focus, secure desktop transitions, and composition.

When display topology changes, the host closes the previous surfaces, re-enumerates monitors, and creates a new set. Per-monitor icon persistence is not implemented yet; it will use stable monitor identities rather than HWNDs.

Compatibility-mode z-order still needs hardware and Explorer-version testing. Cerebrum does not use undocumented WorkerW parenting in this foundation.

## Component discovery

Executable discovery is ordered and deterministic:

1. validated absolute settings path;
2. component-specific environment variable;
3. installed Local AppData location;
4. sibling repository build output;
5. PATH.

The resolver returns a source category, not a personal path, for status and diagnostics. Development discovery scans only each expected repository's source tree and accepts executable files below a build-output directory.

A missing application becomes a Missing component state. It does not fail host startup.

## Component state model

Each component is presented as one of:

- Unknown;
- Missing;
- Starting;
- Running;
- Stopped;
- Failed.

The supervisor serializes start attempts per component. It distinguishes an independently running sibling process from one launched by the current host. Only processes launched by the host receive exit monitoring and bounded restart attempts.

Restart history is limited to a one-minute window. The default budget is three attempts. Reaching the limit changes the state to Failed and requires a user-requested session repair or host restart.

## UI-thread rules

The WPF dispatcher may perform only short presentation and HWND tasks.

Work kept away from it includes:

- recursive executable discovery;
- settings file reads and writes;
- broker pipe I/O;
- diagnostic writes;
- component-process discovery;
- process launch monitoring.

Status events can arrive from worker threads. The application marshals only the resulting immutable status record back to the dispatcher.

Future shell, COM, WinRT, AppX, icon, and thumbnail providers must run inside the broker and have deadlines. They must not be moved into the WPF process for convenience.

## Settings durability

The settings store:

- caps accepted JSON at 1 MiB;
- performs semantic validation after deserialization;
- writes a unique pending file beside the destination;
- flushes through the operating system before commit;
- uses atomic replacement when a current file exists;
- maintains one backup generation;
- falls back to the backup or defaults when the current file is unreadable.

Settings versions are explicit. A new schema requires a deliberate migration; unknown versions are not silently interpreted as the current contract.

Each component owns a separate data directory. Cross-component theme synchronization must copy a versioned value contract, never share a writable settings file.

## Broker boundary

Protocol version 1 uses a current-user-only named pipe whose name is qualified by Windows session and user identity. Requests and responses are newline-delimited JSON and limited to 4,096 characters.

Every request contains:

- protocol version;
- random request ID;
- command.

Every response echoes the request ID and contains success plus a stable status. Unknown versions and commands fail closed.

Current commands are health, capabilities, and shutdown. This intentionally tiny protocol proves process isolation and health behavior before high-risk providers are added.

Because protocol version 1 exposes no active Windows provider, startup resolves
the Broker executable but does not launch it. A future provider must ensure and
health-check the Broker before sending its first bounded request.

## Diagnostics

Host diagnostics are written on a background channel. The active file rotates at approximately 1 MiB and retains one previous generation.

Allowed content:

- UTC timestamp;
- stable event code;
- component identifier;
- exception type name where necessary.

Disallowed content:

- window titles;
- paths and filenames;
- shortcut targets;
- process arguments;
- file contents;
- search queries;
- user-entered text.

Failure to write diagnostics is non-fatal.

## Shutdown

On host shutdown:

1. detach display-change events;
2. close every desktop surface;
3. cancel pending restart delays;
4. request graceful broker shutdown with a two-second deadline if it was started;
5. dispose process handles without terminating independent sibling applications;
6. flush the diagnostic channel;
7. release the single-instance mutex.

Explorer remains available throughout compatibility mode.

## Future boundaries

The next architectural increments are:

- desktop item model and per-monitor layout;
- versioned theme contract;
- broker provider interface with per-request cancellation;
- quick-settings and notification surfaces;
- signed component manifest and capability negotiation;
- deployment supervisor with crash-loop rollback;
- explicit shell-session shutdown and recovery contracts.
