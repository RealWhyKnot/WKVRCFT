param(
    [switch]$Dev  # Use -Dev for a development build: debug logs enabled by default, no PDB strip
)

$ErrorActionPreference = "Stop"

$BuildDir = Join-Path $PSScriptRoot "dist"
$VendorDir = Join-Path $PSScriptRoot "vendor"
$LocalVersionState = Join-Path $VendorDir "local_build_state.json"

# --- Setup git hooks ---
$HooksPath = git -C $PSScriptRoot config core.hooksPath 2>$null
if ($HooksPath -ne ".githooks") {
    Write-Host "Setting up git hooks..." -ForegroundColor Cyan
    git -C $PSScriptRoot config core.hooksPath .githooks
}

# --- Clean dist ---
if (Test-Path $BuildDir) {
    Write-Host "Cleaning dist folder..." -ForegroundColor Cyan

    Get-Process "VRCFaceTracking" -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process "VRCFaceTracking.ModuleHost" -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process "VRCFaceTracking.ModuleHostV2" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    try {
        Remove-Item -Path $BuildDir -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Host "Warning: Failed to fully clean dist folder. Some files may be in use." -ForegroundColor Yellow
    }
}
New-Item -ItemType Directory $BuildDir -Force | Out-Null
if (!(Test-Path $VendorDir)) { New-Item -ItemType Directory $VendorDir | Out-Null }

# --- Daily Versioning Logic ---
$Today = Get-Date -Format "yyyy.M.d"
$BuildCount = 0

if (Test-Path $LocalVersionState) {
    $State = Get-Content $LocalVersionState | ConvertFrom-Json
    if ($State.Date -eq $Today) {
        $BuildCount = $State.Count + 1
    }
}

$FullVersion = "$Today.$BuildCount"
@{ "Date" = $Today; "Count" = $BuildCount } | ConvertTo-Json | Out-File $LocalVersionState

Write-Host "Building Version: $FullVersion" -ForegroundColor Magenta

# --- Inject Version into Vue Store ---
$AppStorePath = Join-Path $PSScriptRoot "ui/src/stores/appStore.ts"
$StoreContent = Get-Content $AppStorePath -Raw
$RegexPattern = "version = ref\('(.+?)'\)"
$RegexReplace = "version = ref('$FullVersion')"
$NewStoreContent = $StoreContent -replace $RegexPattern, $RegexReplace
Set-Content $AppStorePath $NewStoreContent

# --- Inject Version into C# ---
$VersionInfoPath = Join-Path $PSScriptRoot "src/VRCFaceTracking.Core/VersionInfo.cs"
@"
namespace VRCFaceTracking.Core;
public static class VersionInfo
{
    public const string Version = "$FullVersion";
}
"@ | Set-Content $VersionInfoPath

# --- Build Frontend ---
Write-Host "`n--- Building Frontend ($( if ($Dev) { 'development' } else { 'production' }) mode) ---" -ForegroundColor Cyan
Push-Location (Join-Path $PSScriptRoot "ui")
npm install --silent
if ($Dev) {
    # Development: import.meta.env.DEV=true → debug logs on by default, source maps included
    npm run build:dev
} else {
    # Production: import.meta.env.DEV=false → debug logs hidden by default
    npm run build:prod
}
Pop-Location

# --- Build .NET Projects ---
Write-Host "`n--- Building .NET Projects ---" -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot "src/VRCFaceTracking.App/VRCFaceTracking.App.csproj") -c Release -o $BuildDir --self-contained true -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -warnaserror
dotnet publish (Join-Path $PSScriptRoot "src/VRCFaceTracking.ModuleHost/VRCFaceTracking.ModuleHost.csproj") -c Release -o $BuildDir --self-contained true -r win-x64 -p:PublishSingleFile=true -warnaserror
dotnet publish (Join-Path $PSScriptRoot "src/VRCFaceTracking.ModuleHostV2/VRCFaceTracking.ModuleHostV2.csproj") -c Release -o $BuildDir --self-contained true -r win-x64 -p:PublishSingleFile=true -warnaserror

# --- Build Built-in Modules ---
Write-Host "`n--- Building Built-in Modules ---" -ForegroundColor Cyan
$BuiltInDir = Join-Path $BuildDir "builtin-modules"
New-Item -ItemType Directory -Path $BuiltInDir -Force | Out-Null

$EmulatedTrackingProject = Join-Path $PSScriptRoot "modules/VRCFaceTracking.EmulatedTracking/VRCFaceTracking.EmulatedTracking.csproj"
$EmulatedTrackingOut = Join-Path $BuiltInDir "VRCFaceTracking.EmulatedTracking"
dotnet publish $EmulatedTrackingProject -c Release -o $EmulatedTrackingOut -warnaserror
Write-Host "  EmulatedTracking built → $EmulatedTrackingOut" -ForegroundColor Gray

# --- Final Packaging ---
Write-Host "`n--- Packaging ---" -ForegroundColor Cyan

# Rename main executable
$AppExeName = "VRCFaceTracking.App.exe"
$AppBuildPath = Join-Path $BuildDir $AppExeName
if (Test-Path $AppBuildPath) {
    Move-Item $AppBuildPath (Join-Path $BuildDir "VRCFaceTracking.exe") -Force
}

# Copy wwwroot to dist
Copy-Item -Path (Join-Path $PSScriptRoot "src/VRCFaceTracking.App/wwwroot") -Destination $BuildDir -Recurse -Force

# Copy fti_osc native lib if present
$FtiOscPath = Join-Path $PSScriptRoot "fti_osc.dll"
if (Test-Path $FtiOscPath) {
    Copy-Item $FtiOscPath (Join-Path $BuildDir "fti_osc.dll")
}

# Cleanup
if (-not $Dev) {
    # Strip PDBs from production builds to keep the folder lean
    Get-ChildItem -Path $BuildDir -Filter "*.pdb" -Recurse | Remove-Item -Force
}
Get-ChildItem -Path $BuildDir -Filter "*.log" | Remove-Item -Force

# Write version files
$FullVersion | Set-Content -Path (Join-Path $BuildDir "version.txt") -Encoding UTF8
$FullVersion | Set-Content -Path (Join-Path $PSScriptRoot "version.txt") -Encoding UTF8

# --- Restore Version in Store ---
$RestoreContent = $NewStoreContent -replace "version = ref\('(.+?)'\)", "version = ref('dev')"
Set-Content $AppStorePath $RestoreContent

Write-Host "`nBuild $FullVersion Complete! Output in: $BuildDir" -ForegroundColor Green
