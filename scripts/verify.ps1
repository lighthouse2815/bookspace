$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $repoRoot 'backend'
$frontendRoot = Join-Path $repoRoot 'frontend'

function Invoke-CheckedNativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string[]]$ArgumentList,
        [Parameter(Mandatory)]
        [string]$Description
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$Description thất bại với exit code $LASTEXITCODE."
    }
}

Write-Host 'Verifying BookSpace backend...'
Push-Location $backendRoot
try {
    Invoke-CheckedNativeCommand dotnet @('restore', 'BookSpace.slnx') 'Backend restore'
    Invoke-CheckedNativeCommand dotnet @(
        'format',
        'BookSpace.slnx',
        '--verify-no-changes',
        '--no-restore'
    ) 'Backend format'
    Invoke-CheckedNativeCommand dotnet @(
        'build',
        'BookSpace.slnx',
        '--no-restore'
    ) 'Backend build'
    Invoke-CheckedNativeCommand dotnet @(
        'test',
        'BookSpace.slnx',
        '--no-build'
    ) 'Backend tests'
}
finally {
    Pop-Location
}

Write-Host 'Verifying BookSpace frontend...'
Push-Location $frontendRoot
try {
    Invoke-CheckedNativeCommand npm @('ci') 'Frontend install'
    Invoke-CheckedNativeCommand npm @('run', 'typecheck') 'Frontend typecheck'
    Invoke-CheckedNativeCommand npm @('run', 'lint') 'Frontend lint'
    Invoke-CheckedNativeCommand npm @('test') 'Frontend tests'
    Invoke-CheckedNativeCommand npm @('run', 'build') 'Frontend build'
}
finally {
    Pop-Location
}

Write-Host 'BookSpace verification completed.'
