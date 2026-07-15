param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$brokerPath = Join-Path $repositoryRoot "src\Cerebrum.Broker\bin\x64\$Configuration\net8.0-windows\win-x64\Cerebrum.Broker.exe"

if (-not (Test-Path -LiteralPath $brokerPath -PathType Leaf)) {
    throw "Build Cerebrum.Broker for $Configuration x64 before running this smoke test."
}

$pipeName = "Cerebrum.BrokerSmoke." + [Guid]::NewGuid().ToString("N")
$startArguments = @{
    FilePath = $brokerPath
    ArgumentList = @("--serve", "--pipe", $pipeName)
    PassThru = $true
    WindowStyle = "Hidden"
}
$broker = Start-Process @startArguments

function Send-BrokerRequest {
    param(
        [Parameter(Mandatory)]
        [string]$RequestId,

        [Parameter(Mandatory)]
        [string]$Command
    )

    $client = [IO.Pipes.NamedPipeClientStream]::new(
        ".",
        $pipeName,
        [IO.Pipes.PipeDirection]::InOut,
        [IO.Pipes.PipeOptions]::Asynchronous)

    try {
        $client.Connect(2000)
        $writer = [IO.StreamWriter]::new(
            $client,
            [Text.UTF8Encoding]::new($false),
            1024,
            $true)
        $reader = [IO.StreamReader]::new(
            $client,
            [Text.Encoding]::UTF8,
            $false,
            1024,
            $true)

        try {
            $writer.AutoFlush = $true
            $request = @{
                version = 1
                requestId = $RequestId
                command = $Command
            } | ConvertTo-Json -Compress
            $writer.WriteLine($request)
            $line = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -gt 4096) {
                throw "The broker returned an empty or oversized response."
            }

            return $line | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
            $writer.Dispose()
        }
    }
    finally {
        $client.Dispose()
    }
}

try {
    Start-Sleep -Milliseconds 150

    $health = Send-BrokerRequest -RequestId "smoke-health" -Command "health"
    if (-not $health.success -or $health.status -ne "healthy") {
        throw "The broker health response was not successful."
    }

    $shutdown = Send-BrokerRequest -RequestId "smoke-shutdown" -Command "shutdown"
    if (-not $shutdown.success -or $shutdown.status -ne "shutting-down") {
        throw "The broker shutdown response was not successful."
    }

    if (-not $broker.WaitForExit(3000)) {
        throw "The broker did not exit after its shutdown acknowledgement."
    }

    if ($broker.ExitCode -ne 0) {
        throw "The broker returned a nonzero exit code."
    }

    Write-Host "PASS broker pipe health and graceful shutdown ($Configuration)"
}
finally {
    if (-not $broker.HasExited) {
        $broker.Kill()
        $broker.WaitForExit(2000)
    }

    $broker.Dispose()
}
