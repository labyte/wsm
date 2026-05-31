param(
    [string]$AppVersion = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# 解析仓库根目录，保证在任意位置执行都可用
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

# 版本号优先级：参数 > GitHub Tag > 本地时间戳
if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        $AppVersion = $env:GITHUB_REF_NAME.TrimStart("v")
    } else {
        $AppVersion = Get-Date -Format "yyyy.MM.dd.HHmm"
    }
}

Write-Host "==> AppVersion: $AppVersion"

# 1) 还原与编译
dotnet restore "WSM.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build "WSM.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 2) 发布 Modern 版本（与现有 PublishProfile 对齐）
dotnet publish "src/WSM.App.Modern/WSM.App.Modern.csproj" -c $Configuration /p:PublishProfile=FolderProfile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishDir = Join-Path $RepoRoot "src/WSM.App.Modern/bin/$Configuration/net8.0-windows/publish/win-x64"
if (-not (Test-Path $publishDir)) {
    throw "Publish 输出目录不存在: $publishDir"
}

# 3) 查找并执行 Inno Setup
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "未找到 ISCC.exe，请先安装 Inno Setup 6。"
}

& $iscc "installer/wsm.iss" "/DAppVersion=$AppVersion" "/DPublishDir=$publishDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> 安装包输出目录: installer/Output"
