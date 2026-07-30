$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $repoRoot 'backend'
$frontendRoot = Join-Path $repoRoot 'frontend'

Write-Host 'Verifying BookSpace backend...'
Push-Location $backendRoot
try {
    dotnet restore BookSpace.slnx
    dotnet build BookSpace.slnx --no-restore
    dotnet test BookSpace.slnx --no-build
}
finally {
    Pop-Location
}

Write-Host 'Verifying BookSpace frontend...'
Push-Location $frontendRoot
try {
    npm install
    npm run lint
    npm run build
}
finally {
    Pop-Location
}

Write-Host 'BookSpace verification completed.'
