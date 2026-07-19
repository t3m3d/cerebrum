[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runnerPath = Join-Path $repositoryRoot "tests\Cerebrum.Tests\bin\x64\$Configuration\net8.0\Cerebrum.Tests.dll"
if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
    throw "Build Cerebrum.Tests for $Configuration x64 before comparing performance."
}

$resolvedBaseline = (Resolve-Path -LiteralPath $BaselinePath).Path
$resolvedCandidate = (Resolve-Path -LiteralPath $CandidatePath).Path

& dotnet $runnerPath --performance-compare $resolvedBaseline $resolvedCandidate
if ($LASTEXITCODE -ne 0) {
    throw "The candidate did not satisfy the lighter-desktop performance policy."
}

Write-Host "PASS candidate satisfies the lighter-desktop performance policy"
