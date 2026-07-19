[CmdletBinding()]
param(
    [ValidateSet("Stock", "Compatibility", "Lite")]
    [string]$Profile = "Stock",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateRange(5, 300)]
    [int]$DurationSeconds = 15,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runnerPath = Join-Path $repositoryRoot "tests\Cerebrum.Tests\bin\x64\$Configuration\net8.0\Cerebrum.Tests.dll"
if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
    throw "Build Cerebrum.Tests for $Configuration x64 before capturing performance."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $OutputPath = Join-Path $repositoryRoot "artifacts\performance\$($Profile.ToLowerInvariant())-$timestamp.json"
}
else {
    $OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
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
& dotnet $runnerPath --performance-capture $Profile.ToLowerInvariant() $DurationSeconds $OutputPath
$captureExitCode = $LASTEXITCODE
$after = Get-ComponentProcessIds

$newProcesses = @($after.Keys | Where-Object { -not $before.ContainsKey($_) })
if ($newProcesses.Count -ne 0) {
    throw "Performance capture unexpectedly launched a component process: $($newProcesses -join ', ')"
}

if ($captureExitCode -ne 0) {
    throw "Performance capture failed profile validation with exit code $captureExitCode. The JSON was retained for diagnosis."
}

Write-Host "PASS read-only $Profile performance capture; no component process was launched"
Write-Host "Snapshot: $OutputPath"
