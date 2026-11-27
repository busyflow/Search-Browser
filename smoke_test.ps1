param(
    [string]$ExpectedVersion = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

Write-Host "=== [1/4] Checking Version Consistency ===" -ForegroundColor Cyan
$mw = Get-Content (Join-Path $root "src\MainWindow.cs") -Raw
$ip = Get-Content (Join-Path $root "src\InternalPagesHtml.cs") -Raw
$is = Get-Content (Join-Path $root "InstallerSetup.cs") -Raw

$v1 = if ($mw -match 'currentVersion\s*=\s*"([^"]+)"') { $Matches[1] } else { "MISSING" }
$v2 = if ($ip -match 'Search for Windows.{1,8}v([0-9\.]+)') { $Matches[1] } else { "MISSING" }
$v3 = if ($is -match '\(v([0-9\.]+)\)') { $Matches[1] } else { "MISSING" }

Write-Host "  MainWindow.cs:        v$v1"
Write-Host "  InternalPagesHtml.cs: v$v2"
Write-Host "  InstallerSetup.cs:    v$v3"

if ($v1 -ne $v2 -or $v1 -ne $v3) {
    Write-Error "Version mismatch detected! All 3 files must match."
    exit 1
}
if ($ExpectedVersion -and $v1 -ne $ExpectedVersion) {
    Write-Error "Expected version $ExpectedVersion, but found $v1."
    exit 1
}

Write-Host "=== [2/4] Building Search.exe & Search-Setup.exe ===" -ForegroundColor Cyan
powershell -ExecutionPolicy Bypass -File (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit 1
}

$exe = Join-Path $root "Search.exe"
$setup = Join-Path $root "Search-Setup.exe"
if (!(Test-Path $exe) -or !(Test-Path $setup)) {
    Write-Error "Build artifacts missing!"
    exit 1
}

Write-Host "=== [3/4] Running Startup & Crash Smoke Test ===" -ForegroundColor Cyan
$existing = Get-Process -Name "Search" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "  Stopping running Search.exe instances for clean test..."
    $existing | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4

if ($proc.HasExited) {
    Write-Error "SMOKE TEST FAILED: Search.exe exited unexpectedly with code $($proc.ExitCode)!"
    exit 1
}

$proc.Refresh()
$memMB = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
$wsMB = [math]::Round($proc.WorkingSet64 / 1MB, 1)
Write-Host "  Search.exe alive! PID=$($proc.Id) | Private=${memMB}MB | WorkingSet=${wsMB}MB" -ForegroundColor Green

if ($memMB -gt 120) {
    Write-Warning "Search.exe private memory (${memMB}MB) is higher than expected baseline!"
}

Write-Host "=== [4/4] Cleaning Up Test Instance ===" -ForegroundColor Cyan
$proc | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Write-Host "ALL SMOKE TESTS PASSED for v$v1!" -ForegroundColor Green
exit 0
