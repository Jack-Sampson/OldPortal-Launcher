# Build-Release.ps1
# Builds the OldPortal Launcher installer using InnoSetup
# Version is automatically read from OPLauncher.csproj (single source of truth)

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "OldPortal Launcher - Release Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the version from .csproj (single source of truth)
Write-Host "[1/4] Reading version from project file..." -ForegroundColor Yellow
$ProjectFile = Join-Path $PSScriptRoot "..\OPLauncher.csproj"
[xml]$ProjectXml = Get-Content $ProjectFile
$Version = $ProjectXml.Project.PropertyGroup.Version

if (-not $Version) {
    Write-Host "  [ERROR] Could not read version from OPLauncher.csproj" -ForegroundColor Red
    exit 1
}

Write-Host "  Version: $Version" -ForegroundColor Green
Write-Host ""

# Clean previous builds
Write-Host "[2/4] Cleaning previous builds..." -ForegroundColor Yellow
$PublishDir = Join-Path $PSScriptRoot "..\publish\win-x86"
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force
}
Write-Host "  [OK] Clean complete" -ForegroundColor Green
Write-Host ""

# Build and publish
Write-Host "[3/4] Building and publishing..." -ForegroundColor Yellow
dotnet publish -c $Configuration -r win-x86 --self-contained -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] Build complete" -ForegroundColor Green
Write-Host ""

# Create installer with InnoSetup
Write-Host "[4/4] Creating installer package with InnoSetup..." -ForegroundColor Yellow

# Find InnoSetup compiler
$InnoSetupPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$ISCC = $null
foreach ($path in $InnoSetupPaths) {
    if (Test-Path $path) {
        $ISCC = $path
        break
    }
}

if (-not $ISCC) {
    Write-Host "  [ERROR] InnoSetup not found!" -ForegroundColor Red
    Write-Host "  Please install InnoSetup 6.6.1 or later from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}

Write-Host "  Using InnoSetup: $ISCC" -ForegroundColor Gray

# Run InnoSetup compiler (from parent directory where installer.iss is located)
$InstallerScript = Join-Path $PSScriptRoot "..\installer.iss"
& $ISCC $InstallerScript

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Installer creation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  [OK] Installer created" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "BUILD COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Installer Location: .\Releases\OPLauncher-Setup.exe" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files created:" -ForegroundColor Yellow
Get-ChildItem ".\Releases\OPLauncher-Setup.exe" | ForEach-Object {
    $sizeInMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  - $($_.Name) ($sizeInMB MB)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the installer: .\Releases\OPLauncher-Setup.exe" -ForegroundColor White
Write-Host "  2. Verify installation directory picker works" -ForegroundColor White
Write-Host "  3. Check desktop shortcut and start menu entries" -ForegroundColor White
Write-Host ""
