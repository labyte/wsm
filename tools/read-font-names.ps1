# 开发辅助：读取 TTF 的 Win32 族名，便于配置 pack URI。#FamilyName
# 不参与构建；CI 不会执行此脚本。
Add-Type -AssemblyName PresentationCore
$repoRoot = Split-Path $PSScriptRoot -Parent
$dir = Join-Path $repoRoot 'assets\fonts\AlibabaPuHuiTi-3'
if (-not (Test-Path $dir)) {
    Write-Error "字体目录不存在: $dir"
    exit 1
}
Get-ChildItem $dir -Filter *.ttf | ForEach-Object {
    $g = New-Object System.Windows.Media.GlyphTypeface($_.FullName)
    $name = $g.Win32FamilyNames[1033]
    $face = $g.Win32FaceNames[1033]
    Write-Output "$($_.Name) => Family: $name | Face: $face"
}
