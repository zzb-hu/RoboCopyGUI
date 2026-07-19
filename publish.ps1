# publish.ps1 — RoboCopyGUI 一键打包（单文件 exe 免安装，x64 自包含）
# 用法: powershell -File publish.ps1
# 注意：本文件必须带 UTF-8 BOM 保存，否则 PS5.1 按 GBK 误读中文
$ErrorActionPreference = 'Stop'

$proj = Join-Path $PSScriptRoot 'RoboCopyGUI.csproj'
$exe = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\win-x64\publish\RoboCopyGUI.exe'

Write-Host "== 1/2 发布单文件 exe（自包含 .NET 8 运行时） ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true --nologo `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "发布失败，退出码 $LASTEXITCODE" }

Write-Host ""
Write-Host "== 2/2 产物信息 ==" -ForegroundColor Cyan
if (-not (Test-Path $exe)) { throw "产物不存在: $exe" }
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
$vi = (Get-Item $exe).VersionInfo
Write-Host "路径:    $exe"
Write-Host "体积:    $mb MB"
Write-Host "产品名:  $($vi.ProductName)"
Write-Host "版本:    $($vi.FileVersion)"
Write-Host ""
Write-Host "打包完成！分发时只需发送这一个 exe 文件。" -ForegroundColor Green
