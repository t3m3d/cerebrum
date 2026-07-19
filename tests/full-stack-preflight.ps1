[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runnerPath = Join-Path $repositoryRoot "tests\Cerebrum.Tests\bin\x64\$Configuration\net8.0\Cerebrum.Tests.dll"
if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
    throw "Build Cerebrum.Tests for $Configuration x64 before running the full-stack preflight."
}

$componentProcessNames = @(
    "Cerebrum.Host",
    "Cerebrum.Broker",
    "Medulla",
    "Thalamus",
    "Cortex"
)

function Get-ComponentProcessIds {
    $ids = @{}
    foreach ($name in $componentProcessNames) {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            try {
                $ids["$($process.ProcessName):$($process.Id)"] = $true
            }
            finally {
                $process.Dispose()
            }
        }
    }

    return $ids
}

$before = Get-ComponentProcessIds
& dotnet $runnerPath --full-stack-preflight $Configuration
$preflightExitCode = $LASTEXITCODE
$after = Get-ComponentProcessIds

$newProcesses = @($after.Keys | Where-Object { -not $before.ContainsKey($_) })
if ($newProcesses.Count -ne 0) {
    throw "The read-only preflight unexpectedly launched a component process: $($newProcesses -join ', ')"
}

if ($preflightExitCode -ne 0) {
    throw "The full-stack preflight failed with exit code $preflightExitCode."
}

Write-Host "PASS full-stack discovery and binary contracts; no component process was launched ($Configuration)"
