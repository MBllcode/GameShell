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
$license = Join-Path $releaseDirectory 'LICENSE.txt'
$notices = Join-Path $releaseDirectory 'THIRD_PARTY_NOTICES.txt'
$generatedNotices = Join-Path $root 'artifacts\licenses\THIRD_PARTY_NOTICES.txt'
if (-not (Test-Path -LiteralPath $generatedNotices)) {
    throw 'Build the package first to generate third-party notices.'
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $license -Force
Copy-Item -LiteralPath $generatedNotices -Destination $notices -Force
Compress-Archive -LiteralPath @($releaseExe, $license, $notices) -DestinationPath $archive -CompressionLevel Optimal -Force
Write-Host "Release assets: $releaseDirectory"
