# Desktop performance policy

## Goal

Cerebrum is called lighter only when a complete idle desktop session beats a
stock Explorer session under the same machine, display, power, and workload
conditions. Process count alone is not the result. The primary measurements are
private memory, normalized idle CPU, handles, and working set.

The harness is observation-only. It enumerates a fixed list of desktop process
names in the current Windows session, waits for the requested sample interval,
and writes one JSON snapshot. It never starts or stops Cerebrum, Explorer,
Medulla, Thalamus, Cortex, or the broker.

## Profiles

A snapshot must match one of these process shapes:

| Profile | Explorer | Cerebrum Host | Medulla | Thalamus | Cortex |
| --- | --- | --- | --- | --- | --- |
| Stock | Required | Absent | Absent | Absent | Absent |
| Compatibility | Required | Required | Required | Required | Absent |
| Lite | Absent | Required | Required | Required | Absent |

The version-one Broker is allowed but not required in Cerebrum profiles. It now
remains cold until a protected Windows-integration provider needs it. Cortex is
always invalid in an idle snapshot because file browsing is on demand.

Profile validation prevents an empty, crashed, or partially started session from
appearing artificially light. A capture is also rejected if a tracked desktop
process starts or exits during the CPU interval.

## Counted processes

The desktop total includes:

- Explorer and the known Windows shell surfaces, including Start, Search, shell
  infrastructure, text input, and Widgets;
- Cerebrum Host, its Broker when active, Medulla, and Thalamus;
- Cortex if it is unexpectedly resident.

DWM is captured separately as compositor context when accessible. It is not added to the primary
desktop budget because Windows owns it in every profile. Unrelated applications,
window titles, command lines, filenames, and user content are never collected.

The explicit name list is in
`Cerebrum.Core.Performance.DesktopProcessCatalog`. Update that catalog when a
Windows release or Cerebrum component introduces a new owned desktop process.

## Capture procedure

Build first:

    dotnet build Cerebrum.sln -c Release -p:Platform=x64 --no-restore

For a useful measurement, use the same monitor topology and power mode, close
file-manager windows, wait for background startup work to settle, and avoid
interacting with the machine during each interval. Capture at least three runs
per profile; keep the median rather than choosing the best run.

Capture stock Explorer:

    pwsh -NoProfile -File tests\performance-capture.ps1 -Configuration Release -Profile Stock -DurationSeconds 30

During an approved interactive review window, start the complete compatibility
session manually and capture it:

    pwsh -NoProfile -File tests\performance-capture.ps1 -Configuration Release -Profile Compatibility -DurationSeconds 30

The Lite profile remains reserved for the future reversible Explorer-off session.
The capture tool does not enter that mode:

    pwsh -NoProfile -File tests\performance-capture.ps1 -Configuration Release -Profile Lite -DurationSeconds 30

Snapshots default to the ignored `artifacts\performance` directory. An explicit
`-OutputPath` can be supplied for repeatable naming.

## Comparison gate

Compare a stock snapshot with a compatibility or Lite candidate:

    pwsh -NoProfile -File tests\performance-compare.ps1 -Configuration Release -BaselinePath artifacts\performance\stock.json -CandidatePath artifacts\performance\lite.json

A candidate passes only when:

- its private-byte total is at least 10% below stock;
- its normalized idle CPU is no more than 0.25 percentage points above stock;
- its handle total is no more than 10% above stock;
- both snapshots match their declared complete process profiles;
- Cortex is not resident and every desktop process survived the sample interval.

Working set is reported but is not a hard gate because Windows can trim it
aggressively between runs. Private bytes are the primary per-process allocation
measure. DWM metrics are reported separately and should be reviewed for large
composition regressions.

Compatibility mode is expected to fail the memory target while Explorer and
Cerebrum are both present. That result is useful: it quantifies the cost of safe
development mode. No claim that Lite is lighter should be made until a reversible
Lite session exists and passes repeated Release measurements.

## Safety

Performance work must not improve a score by disabling security, accessibility,
input, update, or recovery services. Do not terminate Explorer for a capture
outside an approved review window. Do not make persistent Winlogon or shell
registry changes from the benchmark scripts.
