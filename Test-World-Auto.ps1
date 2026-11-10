# Fully unattended acceptance test of a REAL built Unity player, not mocks.
# Windows PowerShell 5.1. Run after "Собрать игру.cmd" from the project root.
param(
    [ValidateRange(1024, 65535)] [int]$Port = 7786,
    [ValidateRange(30, 600)] [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = Join-Path $root 'Build\Sinbinder\Sinbinder.exe'
$runDir = Join-Path $root ('Logs\Multiplayer\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$hostProc = $null
$one = $null
$two = $null

function Check($condition, $message) {
    if (-not $condition) { throw $message }
}
function ReadLog([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    return [System.IO.File]::ReadAllText($path)
}
function PrintTail([string]$title, [string]$path) {
    Write-Host ("=== " + $title + " ===")
    if (Test-Path -LiteralPath $path) {
        Get-Content -LiteralPath $path -Tail 35 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "(no log file)"
    }
}
function Spawn([string]$title, [string]$arguments) {
    return Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe -Parent) -PassThru
}

try {
    Check (Test-Path -LiteralPath $exe) ('No built Unity player: ' + $exe)
    $latestScript = Get-ChildItem -Path (Join-Path $root 'Assets\Scripts') -Recurse -Filter '*.cs' -File |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($latestScript -and (Get-Item $exe).LastWriteTimeUtc -lt $latestScript.LastWriteTimeUtc) {
        throw ('The game executable is older than source file ' + $latestScript.FullName +
               '. Rebuild using "Собрать игру.cmd" first.')
    }

    New-Item -ItemType Directory -Force -Path $runDir | Out-Null
    $hostLog = Join-Path $runDir 'WorldHost.log'
    $oneLog  = Join-Path $runDir 'Client1.log'
    $twoLog  = Join-Path $runDir 'Client2.log'
    Write-Host ("Sinbinder REAL Unity 2-client acceptance test, port " + $Port)
    Write-Host ("Logs: " + $runDir)
    Write-Host "The world host and both players use the SAME local Unity build."

    $hostArgs = '-batchmode -nographics -mp-test-map -mp-world-host -port ' +
        $Port + ' -logFile "' + $hostLog + '"'
    $hostProc = Spawn 'World Host' $hostArgs
    $hostReady = $false
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 700
        if ($hostProc.HasExited) {
            throw ('Real Unity world host exited early, code ' + $hostProc.ExitCode)
        }
        $hostReady = (ReadLog $hostLog) -match
            '\[Sinbinder MP Probe\] REAL-world LAN order host on TCP'
    } until ($hostReady -or (Get-Date) -ge $deadline)
    Check $hostReady 'Real Unity world host never announced TCP readiness'

    $clientArgs = '-batchmode -nographics -lan-connect 127.0.0.1 -lan-auto-ready ' +
        '-mp-verify-client -port ' + $Port
    $one = Spawn 'Client 1' ($clientArgs + ' -logFile "' + $oneLog + '"')
    Start-Sleep -Milliseconds 450
    $two = Spawn 'Client 2' ($clientArgs + ' -logFile "' + $twoLog + '"')
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((-not $one.HasExited -or -not $two.HasExited) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 700
        if ($hostProc.HasExited) {
            throw ('World host exited before clients finished, code ' + $hostProc.ExitCode)
        }
        $l1 = ReadLog $oneLog
        $l2 = ReadLog $twoLog
        if ($l1 -match '\[Sinbinder REAL Unity Acceptance\] FAIL' -or
            $l2 -match '\[Sinbinder REAL Unity Acceptance\] FAIL') {
            throw 'At least one real Unity client reported a failed acceptance check'
        }
    }
    Check ($one.HasExited -and $two.HasExited) 'One or both real Unity clients timed out'
    Check ($one.ExitCode -eq 0 -and $two.ExitCode -eq 0) (
        'Non-zero Unity process exit code: client 1=' + $one.ExitCode + ', client 2=' + $two.ExitCode)

    $l1 = ReadLog $oneLog
    $l2 = ReadLog $twoLog
    $hostText = ReadLog $hostLog
    Check ($l1 -match '\[Sinbinder REAL Unity Acceptance\] PASS seat=1,') 'Client 1 did not prove seat 1 and real movement'
    Check ($l2 -match '\[Sinbinder REAL Unity Acceptance\] PASS seat=2,') 'Client 2 did not prove seat 2 and real movement'
    foreach ($entry in @(@('Host', $hostText), @('Client 1', $l1), @('Client 2', $l2))) {
        Check ($entry[1] -notmatch 'NullReferenceException|MissingReferenceException|error CS[0-9]+|\[Sinbinder REAL Unity Acceptance\] FAIL') (
            $entry[0] + ' log contains a critical Unity error')
    }

    Write-Host ''
    Write-Host '[Sinbinder REAL Unity Acceptance] PASS: BOTH real Unity clients connected,' -ForegroundColor Green
    Write-Host 'received private Warrior HP/positions, executed Lord orders and MOVED on NavMesh.' -ForegroundColor Green
    Write-Host ('Results: ' + $runDir)
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('[Sinbinder REAL Unity Acceptance] FAIL: ' + $_.Exception.Message) -ForegroundColor Red
    if ($runDir -and (Test-Path -LiteralPath $runDir)) {
        PrintTail 'World Host' (Join-Path $runDir 'WorldHost.log')
        PrintTail 'Client 1' (Join-Path $runDir 'Client1.log')
        PrintTail 'Client 2' (Join-Path $runDir 'Client2.log')
        Write-Host ('Save this folder for diagnosis: ' + $runDir)
    }
    exit 1
}
finally {
    foreach ($proc in @($one, $two, $hostProc)) {
        if ($null -ne $proc) {
            try {
                if (-not $proc.HasExited) {
                    $proc.Kill()
                    $proc.WaitForExit(3000) | Out-Null
                }
            } catch { }
        }
    }
}
