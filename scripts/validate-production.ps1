[CmdletBinding()]
param(
    [string]$EnvironmentFile = '.env.production'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$environmentPath = if ([System.IO.Path]::IsPathRooted($EnvironmentFile)) {
    $EnvironmentFile
}
else {
    Join-Path $repoRoot $EnvironmentFile
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw "Khong tim thay file cau hinh production: $environmentPath"
}

$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    $trimmed = $line.Trim()
    if (-not $trimmed -or $trimmed.StartsWith('#')) {
        continue
    }

    $separator = $trimmed.IndexOf('=')
    if ($separator -le 0) {
        throw "Dong cau hinh production khong hop le: $trimmed"
    }

    $key = $trimmed.Substring(0, $separator).Trim()
    $value = $trimmed.Substring($separator + 1).Trim()
    $settings[$key] = $value
}

$required = @(
    'BOOKSPACE_PUBLIC_ORIGIN',
    'BOOKSPACE_JWT_SECRET',
    'BOOKSPACE_OPERATOR_NAME',
    'BOOKSPACE_SUPPORT_EMAIL',
    'BOOKSPACE_DATA_VOLUME',
    'BOOKSPACE_DOCKER_SUBNET',
    'BOOKSPACE_SMTP_HOST',
    'BOOKSPACE_SMTP_FROM_ADDRESS'
)
foreach ($key in $required) {
    if (-not $settings.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($settings[$key])) {
        throw "Thieu cau hinh bat buoc: $key"
    }
}

$unsafeValue = $settings.Values | Where-Object {
    $_ -match 'REPLACE_|bookspace\.example\.com|smtp\.example\.com'
} | Select-Object -First 1
if ($unsafeValue) {
    throw 'File production van chua gia tri mau. Hay thay toan bo REPLACE_* va domain example.'
}

$publicOrigin = $null
if (-not [Uri]::TryCreate($settings['BOOKSPACE_PUBLIC_ORIGIN'], [UriKind]::Absolute, [ref]$publicOrigin) -or
    $publicOrigin.Scheme -ne 'https' -or
    $publicOrigin.AbsolutePath -ne '/' -or
    -not [string]::IsNullOrEmpty($publicOrigin.Query) -or
    -not [string]::IsNullOrEmpty($publicOrigin.Fragment) -or
    $settings['BOOKSPACE_PUBLIC_ORIGIN'].EndsWith('/')) {
    throw 'BOOKSPACE_PUBLIC_ORIGIN phai la origin HTTPS tuyet doi, khong co path, query, fragment hoac dau slash cuoi.'
}

if ([Text.Encoding]::UTF8.GetByteCount($settings['BOOKSPACE_JWT_SECRET']) -lt 32) {
    throw 'BOOKSPACE_JWT_SECRET phai co it nhat 32 byte UTF-8.'
}

$supportEmail = $null
try {
    $supportEmail = [System.Net.Mail.MailAddress]::new($settings['BOOKSPACE_SUPPORT_EMAIL'])
}
catch {
    throw 'BOOKSPACE_SUPPORT_EMAIL phai la dia chi email hop le.'
}
if ($supportEmail.Address -ne $settings['BOOKSPACE_SUPPORT_EMAIL']) {
    throw 'BOOKSPACE_SUPPORT_EMAIL phai la dia chi email hop le.'
}

$ipv4Octet = '(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])'
if ($settings['BOOKSPACE_DOCKER_SUBNET'] -notmatch "^($ipv4Octet\.){3}$ipv4Octet\/(1[6-9]|2[0-9])$") {
    throw 'BOOKSPACE_DOCKER_SUBNET phai la IPv4 CIDR tu /16 den /29.'
}
$subnetParts = $settings['BOOKSPACE_DOCKER_SUBNET'].Split('/')
$subnetBytes = [System.Net.IPAddress]::Parse($subnetParts[0]).GetAddressBytes()
$prefixLength = [int]$subnetParts[1]
for ($index = 0; $index -lt $subnetBytes.Length; $index++) {
    $remainingBits = $prefixLength - ($index * 8)
    $networkMask = if ($remainingBits -ge 8) {
        255
    }
    elseif ($remainingBits -gt 0) {
        256 - [Math]::Pow(2, 8 - $remainingBits)
    }
    else {
        0
    }
    $hostMask = 255 - [int]$networkMask
    if (($subnetBytes[$index] -band $hostMask) -ne 0) {
        throw 'BOOKSPACE_DOCKER_SUBNET phai dung dia chi network base cua CIDR.'
    }
}

if ($settings['BOOKSPACE_DATA_VOLUME'] -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{1,127}$') {
    throw 'BOOKSPACE_DATA_VOLUME phai la ten Docker volume hop le, dai 2-128 ky tu.'
}

$smtpUsername = if ($settings.ContainsKey('BOOKSPACE_SMTP_USERNAME')) {
    $settings['BOOKSPACE_SMTP_USERNAME']
}
else {
    ''
}
$smtpPassword = if ($settings.ContainsKey('BOOKSPACE_SMTP_PASSWORD')) {
    $settings['BOOKSPACE_SMTP_PASSWORD']
}
else {
    ''
}
if ([string]::IsNullOrWhiteSpace($smtpUsername) -xor
    [string]::IsNullOrWhiteSpace($smtpPassword)) {
    throw 'SMTP username va password phai duoc cau hinh cung nhau hoac cung bo trong.'
}

$composeFile = Join-Path $repoRoot 'docker-compose.production.yml'
& docker compose --env-file $environmentPath --file $composeFile config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Cau hinh Docker Compose production khong hop le (exit code $LASTEXITCODE)."
}

Write-Host 'Production configuration: PASS'
