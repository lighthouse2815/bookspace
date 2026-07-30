$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $repoRoot 'backend'
$frontendRoot = Join-Path $repoRoot 'frontend'

$backend = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', 'src/BookSpace.Api') `
    -WorkingDirectory $backendRoot `
    -WindowStyle Hidden `
    -PassThru

$frontend = Start-Process `
    -FilePath 'npm.cmd' `
    -ArgumentList @('run', 'dev', '--', '--host', '0.0.0.0') `
    -WorkingDirectory $frontendRoot `
    -WindowStyle Hidden `
    -PassThru

Write-Host "Backend PID: $($backend.Id) - http://localhost:5080"
Write-Host "Frontend PID: $($frontend.Id) - http://localhost:5173"
Write-Host 'Press Ctrl+C to stop both processes.'

try {
    while (-not $backend.HasExited -and -not $frontend.HasExited) {
        Start-Sleep -Seconds 1
        $backend.Refresh()
        $frontend.Refresh()
    }

    if ($backend.HasExited) {
        throw "Backend exited with code $($backend.ExitCode)."
    }

    throw "Frontend exited with code $($frontend.ExitCode)."
}
finally {
    foreach ($process in @($backend, $frontend)) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id
        }
    }
}
