param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$SkipNpmInstall,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$uiPath = Join-Path $root 'src\GameShell.Ui'
$desktopProject = Join-Path $root 'src\GameShell.Desktop\GameShell.Desktop.csproj'
$wwwrootPath = Join-Path $root 'src\GameShell.Desktop\wwwroot'
$artifactRoot = Join-Path $root 'artifacts'
$installerPath = Join-Path $artifactRoot 'installer'
$publishTempPath = Join-Path $artifactRoot 'package-temp'
$stagedUiPath = Join-Path $artifactRoot 'package-ui'
$previousUiPath = Join-Path $artifactRoot 'package-ui-previous'
$finalExePath = Join-Path $installerPath 'GameShell.exe'

foreach ($workspaceDirectory in @($wwwrootPath, $publishTempPath, $stagedUiPath, $previousUiPath, $installerPath)) {
    $checkedPath = [System.IO.Path]::GetFullPath($workspaceDirectory)
    $checkedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    if (-not $checkedPath.StartsWith($checkedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a directory outside the workspace: $checkedPath"
    }
}

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
        if (-not $resolvedPath.StartsWith($resolvedRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
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

Write-Host 'Stopping GameShell before building...'
Stop-RunningGameShell

Write-Host 'Building React UI...'
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

Write-Host 'Staging React build for publishing...'
Remove-WorkspaceDirectory $stagedUiPath
New-Item -ItemType Directory -Force -Path $stagedUiPath | Out-Null
Copy-Item -Path (Join-Path $uiPath 'dist\*') -Destination $stagedUiPath -Recurse -Force

Write-Host 'Publishing self-contained single-file GameShell.exe...'
Remove-WorkspaceDirectory $publishTempPath
New-Item -ItemType Directory -Force -Path $installerPath | Out-Null
New-Item -ItemType Directory -Force -Path $publishTempPath | Out-Null

Invoke-Native {
    dotnet publish $desktopProject `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -o $publishTempPath `
        "-p:GameShellWebRoot=$stagedUiPath" `
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

# Replace workspace assets only after publishing succeeded. Keep the previous
# directory until the staged build is installed so a failed move can roll back.
Stop-RunningGameShell
Remove-WorkspaceDirectory $previousUiPath
if (Test-Path -LiteralPath $wwwrootPath) {
    $checkedUiPath = (Resolve-Path -LiteralPath $wwwrootPath).Path
    if ($checkedUiPath -ne (Join-Path $root 'src\GameShell.Desktop\wwwroot')) {
        throw "Unexpected UI directory: $checkedUiPath"
    }
    Move-Item -LiteralPath $checkedUiPath -Destination $previousUiPath
}
try {
    Move-Item -LiteralPath $stagedUiPath -Destination $wwwrootPath
}
catch {
    if (Test-Path -LiteralPath $previousUiPath) {
        Move-Item -LiteralPath $previousUiPath -Destination $wwwrootPath
    }
    throw
}
Remove-WorkspaceDirectory $previousUiPath

try {
    $packagedExePath = $finalExePath
    $stagedExePath = Join-Path $installerPath 'GameShell.new.exe'
    Copy-Item -LiteralPath $publishedExe -Destination $stagedExePath -Force
    if (Test-Path -LiteralPath $finalExePath) {
        [System.IO.File]::Replace($stagedExePath, $finalExePath, [System.Management.Automation.Language.NullString]::Value)
    }
    else {
        [System.IO.File]::Move($stagedExePath, $finalExePath)
    }
    Write-Host "GameShell exe: $finalExePath"
}
catch {
    $fallbackExePath = Join-Path $installerPath ('GameShell-{0}.exe' -f (Get-Date).ToString('yyyyMMdd-HHmmssfff'))
    Copy-Item -LiteralPath $publishedExe -Destination $fallbackExePath -Force
    $packagedExePath = $fallbackExePath
    Write-Warning "Could not replace ${finalExePath}: $($_.Exception.Message). Fallback artifact: $fallbackExePath"
}

Remove-WorkspaceDirectory $publishTempPath
if (-not $NoLaunch) {
    Write-Host "Opening packaged GameShell: $packagedExePath"
    Start-Process -FilePath $packagedExePath
}
