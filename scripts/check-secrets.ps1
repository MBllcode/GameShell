$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scannerRoot = Join-Path $root 'artifacts\security'
$archive = Join-Path $scannerRoot 'gitleaks.zip'
$scanner = Join-Path $scannerRoot 'gitleaks.exe'
$expectedHash = 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'
New-Item -ItemType Directory -Force -Path $scannerRoot | Out-Null
if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    Invoke-WebRequest -Uri 'https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_windows_x64.zip' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Gitleaks archive checksum mismatch.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $scannerRoot -Force
& $scanner git $root --log-opts='--all --full-history' --redact --ignore-gitleaks-allow --no-banner --no-color
if ($LASTEXITCODE -ne 0) { throw 'Secret scanning failed. Review the redacted findings before publishing.' }
