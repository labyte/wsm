param(
    [string]$AppVersion = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Resolve repository root.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

# Version priority: arg > GitHub tag > timestamp.
if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        $AppVersion = $env:GITHUB_REF_NAME.TrimStart("v")
    } else {
        $AppVersion = Get-Date -Format "yyyy.MM.dd.HHmm"
    }
}

Write-Host "==> AppVersion: $AppVersion"

function Convert-ToFileVersion([string]$VersionText)
{
    $core = $VersionText.Split("-")[0].Split("+")[0]
    $parts = $core.Split(".")
    $safe = @()

    foreach ($part in $parts) {
        $num = 0
        [void][int]::TryParse($part, [ref]$num)
        $safe += $num
    }

    while ($safe.Count -lt 4) {
        $safe += 0
    }

    return ($safe[0..3] -join ".")
}

$fileVersion = Convert-ToFileVersion $AppVersion
Write-Host "==> FileVersion: $fileVersion"

$versionProps = @(
    "/p:Version=$AppVersion",
    "/p:InformationalVersion=$AppVersion",
    "/p:FileVersion=$fileVersion"
)

# 1) Restore and build.
dotnet restore "WSM.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build "WSM.sln" -c $Configuration @versionProps
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2) Publish Modern app.
dotnet publish "src/WSM.App.Modern/WSM.App.Modern.csproj" -c $Configuration /p:PublishProfile=FolderProfile @versionProps
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishDir = Join-Path $RepoRoot "src/WSM.App.Modern/bin/$Configuration/net8.0-windows/publish/win-x64"
if (-not (Test-Path $publishDir)) {
    throw "Publish output folder not found: $publishDir"
}

$requiredWinSwFiles = @(
    (Join-Path $publishDir "winsw/WinSW-x64.exe"),
    (Join-Path $publishDir "winsw/WinSW-x86.exe"),
    (Join-Path $publishDir "winsw/WinSW-net461.exe")
)

$missingWinSwFiles = $requiredWinSwFiles | Where-Object { -not (Test-Path $_) }
if ($missingWinSwFiles.Count -gt 0) {
    throw ("Missing bundled WinSW files in publish output:`n" + ($missingWinSwFiles -join "`n"))
}

Write-Host "==> WinSW files check passed."

# 3) Locate and run Inno Setup.
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 first."
}

& $iscc "installer/wsm.iss" "/DAppVersion=$AppVersion" "/DPublishDir=$publishDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Installer output: installer/Output"
