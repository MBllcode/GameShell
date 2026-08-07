param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$uiPath = Join-Path $root 'src\GameShell.Ui'
$desktopProject = Join-Path $root 'src\GameShell.Desktop\GameShell.Desktop.csproj'
$wwwrootPath = Join-Path $root 'src\GameShell.Desktop\wwwroot'
$artifactRoot = Join-Path $root 'artifacts'
$installerPath = Join-Path $artifactRoot 'installer'
$publishTempPath = Join-Path $artifactRoot 'package-temp'
$iconScriptPath = Join-Path $root 'scripts\GenerateGameShellIcon.ps1'
$finalExePath = Join-Path $installerPath 'GameShell.exe'

function Invoke-Native {
    param([Parameter(Mandatory = $true)][scriptblock]$Command)

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

function Remove-WorkspaceDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolvedRoot = (Resolve-Path $root).Path
    if (Test-Path $Path) {
        $resolvedPath = (Resolve-Path $Path).Path
        if (-not $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove path outside workspace: $resolvedPath"
        }

        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
}

function Stop-RunningGameShell {
    $processes = Get-Process -Name 'GameShell','GameShell.Desktop' -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        catch {
        }
    }
}

Write-Host 'Preparing GameShell icon from provided PNG asset...'
Invoke-Native { powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconScriptPath }

Write-Host 'Building React UI...'
Push-Location $uiPath
try {
    Invoke-Native { npm run build }
}
finally {
    Pop-Location
}

Write-Host 'Copying React build to WPF wwwroot...'
Remove-WorkspaceDirectory $wwwrootPath
New-Item -ItemType Directory -Force -Path $wwwrootPath | Out-Null
Copy-Item -Path (Join-Path $uiPath 'dist\*') -Destination $wwwrootPath -Recurse -Force

Write-Host 'Publishing self-contained single-file GameShell.exe...'
Stop-RunningGameShell
Remove-WorkspaceDirectory $publishTempPath
New-Item -ItemType Directory -Force -Path $installerPath | Out-Null
New-Item -ItemType Directory -Force -Path $publishTempPath | Out-Null

Invoke-Native {
    dotnet publish $desktopProject `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -o $publishTempPath `
        /p:PublishSingleFile=true `
        /p:IncludeAllContentForSelfExtract=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true `
        /p:DebugType=none `
        /p:DebugSymbols=false
}

$publishedExe = Join-Path $publishTempPath 'GameShell.exe'
if (-not (Test-Path $publishedExe)) {
    throw 'Publish did not produce GameShell.exe.'
}

try {
    $packagedExePath = $finalExePath
    Copy-Item -LiteralPath $publishedExe -Destination $finalExePath -Force
    Write-Host "GameShell exe: $finalExePath"
}
catch {
    $fallbackExePath = Join-Path $installerPath ('GameShell-{0}.exe' -f (Get-Date).ToString('yyyyMMdd-HHmmssfff'))
    Copy-Item -LiteralPath $publishedExe -Destination $fallbackExePath -Force
    $packagedExePath = $fallbackExePath
    Write-Warning "Could not replace $finalExePath because it is locked. Fallback artifact: $fallbackExePath"
}

Remove-WorkspaceDirectory $publishTempPath
Write-Host "Opening packaged GameShell: $packagedExePath"
Start-Process -FilePath $packagedExePath
