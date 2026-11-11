param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"

$resolvedExecutable = (Resolve-Path $ExecutablePath).Path
$markerPath = Join-Path $env:RUNNER_TEMP "dynamic-startup-smoke.marker"
$startupLog = Join-Path $env:LOCALAPPDATA "Dynamic\logs\startup.log"

Remove-Item $markerPath -Force -ErrorAction SilentlyContinue
$env:DYNAMIC_SMOKE_TEST = "1"
$env:DYNAMIC_SMOKE_TEST_MARKER = $markerPath
$process = $null

try {
    Write-Host "Launching published Windows app: $resolvedExecutable"
    $process = Start-Process -FilePath $resolvedExecutable -PassThru -WorkingDirectory (Split-Path $resolvedExecutable)

    if (-not $process.WaitForExit(45000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Dynamic did not complete startup within 45 seconds."
    }

    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        throw "Dynamic startup smoke test exited with code $($process.ExitCode)."
    }

    if (-not (Test-Path $markerPath)) {
        throw "Dynamic exited without creating the startup-success marker. XAML or HWND initialization did not complete."
    }

    $marker = Get-Content -Path $markerPath -Raw
    if ($marker -notmatch "pid=$($process.Id); initialized=true") {
        throw "Startup marker does not match the launched process: $marker"
    }

    Write-Host "PASS: XAML loaded, MainWindow was activated, and overlay HWND setup completed."
    Write-Host $marker
}
catch {
    Write-Host "::error::$($_.Exception.Message)"
    if (Test-Path $startupLog) {
        Write-Host "----- Dynamic startup diagnostics -----"
        Get-Content -Path $startupLog -Tail 100
        Write-Host "----- End diagnostics -----"
    }
    throw
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Remove-Item Env:\DYNAMIC_SMOKE_TEST -ErrorAction SilentlyContinue
    Remove-Item Env:\DYNAMIC_SMOKE_TEST_MARKER -ErrorAction SilentlyContinue
}
