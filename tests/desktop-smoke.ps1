param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$hostPath = Join-Path $repositoryRoot "src\Cerebrum.Host\bin\x64\$Configuration\net8.0-windows\win-x64\Cerebrum.Host.exe"

if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
    throw "Build Cerebrum.Host for $Configuration x64 before running this smoke test."
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("CerebrumDesktopSmoke-" + [Guid]::NewGuid().ToString("N"))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null

$settings = @{
    Version = 1
    ThemePreset = "Cerebrum"
    AccentColor = "#7C8CFF"
    WallpaperPath = $null
    StartMedulla = $false
    StartThalamus = $false
    RestartSessionComponents = $true
    RestartLimit = 3
    Components = @{
        Broker = $null
        Medulla = $null
        Thalamus = $null
        Cortex = $null
    }
} | ConvertTo-Json -Depth 4

[IO.File]::WriteAllText((Join-Path $testRoot "settings.json"), $settings)

$existingBrokerIds = @((Get-Process -Name "Cerebrum.Broker" -ErrorAction SilentlyContinue).Id)
$previousDataRoot = $env:CEREBRUM_DATA_ROOT
$env:CEREBRUM_DATA_ROOT = $testRoot
$hostProcess = Start-Process -FilePath $hostPath -PassThru

try {
    Start-Sleep -Seconds 2
    if ($hostProcess.HasExited) {
        throw "Cerebrum.Host exited during startup."
    }

    Add-Type -AssemblyName UIAutomationClient
    $unexpectedBrokers = @(
        Get-Process -Name "Cerebrum.Broker" -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -notin $existingBrokerIds }
    )
    if ($unexpectedBrokers.Count -ne 0) {
        throw "The on-demand Broker started during an idle desktop smoke test."
    }

    $windowName = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty,
        "Cerebrum Desktop")
    $desktop = [Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [Windows.Automation.TreeScope]::Descendants,
        $windowName)

    if ($null -eq $desktop) {
        throw "The Cerebrum desktop window was not exposed to UI Automation."
    }

    $buttonType = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ControlTypeProperty,
        [Windows.Automation.ControlType]::Button)
    $buttonName = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty,
        "Exit Cerebrum desktop")
    $buttonCondition = New-Object Windows.Automation.AndCondition($buttonType, $buttonName)
    $exitButton = $desktop.FindFirst(
        [Windows.Automation.TreeScope]::Descendants,
        $buttonCondition)

    if ($null -eq $exitButton) {
        throw "The desktop exit action was not exposed to UI Automation."
    }

    $invoke = $exitButton.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()

    if (-not $hostProcess.WaitForExit(5000)) {
        throw "Cerebrum.Host did not exit after the desktop exit action."
    }

    if ($hostProcess.ExitCode -ne 0) {
        throw "Cerebrum.Host returned a nonzero exit code."
    }

    $logPath = Join-Path $testRoot "logs\host.log"
    $log = if (Test-Path -LiteralPath $logPath) {
        [IO.File]::ReadAllText($logPath)
    }
    else {
        ""
    }

    if ($log -notmatch "CER-HOST-START" -or $log -notmatch "CER-HOST-STOP") {
        throw "The host lifecycle was not fully recorded."
    }

    Write-Host "PASS desktop presentation, UI Automation exit, cold Broker, and clean shutdown ($Configuration)"
}
finally {
    if (-not $hostProcess.HasExited) {
        $hostProcess.Kill()
        $hostProcess.WaitForExit(3000)
    }

    $hostProcess.Dispose()

    $newBrokers = @(
        Get-Process -Name "Cerebrum.Broker" -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -notin $existingBrokerIds }
    )
    foreach ($broker in $newBrokers) {
        $broker.Kill()
        $broker.WaitForExit(2000)
        $broker.Dispose()
    }

    $env:CEREBRUM_DATA_ROOT = $previousDataRoot

    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $expectedPrefix = Join-Path $temporaryRoot "CerebrumDesktopSmoke-"
    if ($resolvedRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Directory]::Exists($resolvedRoot)) {
        [IO.Directory]::Delete($resolvedRoot, $true)
    }
}
