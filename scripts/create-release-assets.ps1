param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $root 'artifacts\installer\GameShell.exe'
$releaseDirectory = Join-Path $root "artifacts\releases\$Version"
if (-not (Test-Path -LiteralPath $executable)) {
    throw 'Build the portable EXE with scripts/package.ps1 before creating release assets.'
}
$actualVersion = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion.Split('+')[0]
if ($actualVersion -ne $Version) {
    throw "Package version $actualVersion does not match release version $Version."
}
New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$releaseExe = Join-Path $releaseDirectory 'GameShell.exe'
$archive = Join-Path $releaseDirectory 'GameShell-win-x64.zip'
Copy-Item -LiteralPath $executable -Destination $releaseExe -Force
Compress-Archive -LiteralPath $releaseExe -DestinationPath $archive -CompressionLevel Optimal -Force
$checksums = foreach ($file in @($releaseExe, $archive)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($file))"
}
$checksums | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release assets: $releaseDirectory"
