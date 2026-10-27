param(
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$uiPath = Join-Path $root 'src\GameShell.Ui'
$desktopPath = Join-Path $root 'src\GameShell.Desktop'
$wwwrootPath = Join-Path $desktopPath 'wwwroot'

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'Restoring .NET solution...'
Invoke-Native { dotnet restore (Join-Path $root 'GameShell.sln') }

Write-Host 'Preparing React UI...'
Push-Location $uiPath
try {
    if (-not $SkipNpmInstall) {
        if (Test-Path 'package-lock.json') {
            Invoke-Native { npm ci }
        }
        else {
            Invoke-Native { npm install }
        }
    }

    Invoke-Native { npm run build }
}
finally {
    Pop-Location
}

Write-Host 'Copying React build to WPF wwwroot...'
if (Test-Path $wwwrootPath) {
    $resolvedWebRoot = (Resolve-Path -LiteralPath $wwwrootPath).Path
    $expectedWebRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'src\GameShell.Desktop\wwwroot'))
    if (-not [string]::Equals($resolvedWebRoot, $expectedWebRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected web root: $resolvedWebRoot"
    }
    Remove-Item -LiteralPath $resolvedWebRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $wwwrootPath | Out-Null
Copy-Item -Path (Join-Path $uiPath 'dist\*') -Destination $wwwrootPath -Recurse -Force

Write-Host 'Building .NET solution...'
Invoke-Native { dotnet build (Join-Path $root 'GameShell.sln') --no-restore }

Write-Host 'Build completed.'
