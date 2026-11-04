param(
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

Write-Host "Using dotnet: $dotnet" -ForegroundColor Cyan
& $dotnet --version

# 1. Generate the icon.
$iconPath = Join-Path $root "PowerXD\Assets\app.ico"
& $dotnet run --project (Join-Path $root "tools\IconGen") -c $Config -- $iconPath
if ($LASTEXITCODE -ne 0) { throw "Icon generation failed." }

# 2. Publish framework-dependent single file (small, needs .NET 8 Desktop Runtime).
& $dotnet publish (Join-Path $root "PowerXD\PowerXD.csproj") `
    -c $Config `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o (Join-Path $root "publish\portable")
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed." }

# 3. Publish self-contained single file (fully portable, larger).
& $dotnet publish (Join-Path $root "PowerXD\PowerXD.csproj") `
    -c $Config `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o (Join-Path $root "publish\self-contained")
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed." }

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host "  Framework-dependent : $root\publish\portable\PowerXD.exe" -ForegroundColor Green
Write-Host "  Self-contained      : $root\publish\self-contained\PowerXD.exe" -ForegroundColor Green
