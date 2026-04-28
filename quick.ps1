<#
.SYNOPSIS
Fast dev iteration loop: kill running app, incremental Debug build, restage, launch.

.DESCRIPTION
For releases, use build.ps1 (Release / single-file / self-contained).
This script targets the inner dev loop: kills the running app and module hosts,
runs an incremental dotnet build (Debug), copies ModuleHost outputs next to
VRCFaceTracking.App.exe so FindHostExe() picks them up, and launches.

.PARAMETER NoFrontend
Skip "npm run build:dev". Useful when only C# changed.

.PARAMETER NoLaunch
Build only; do not start the app.

.PARAMETER ShowLogs
After launch, open a second PowerShell window that tails the active log file.
#>
param(
    [switch]$NoFrontend,
    [switch]$NoLaunch,
    [switch]$ShowLogs
)

$ErrorActionPreference = "Stop"

$Root      = $PSScriptRoot
$Slnx      = Join-Path $Root "VRCFaceTracking.slnx"
$AppBinDir = Join-Path $Root "src\VRCFaceTracking.App\bin\Debug\net10.0-windows"
$AppExe    = Join-Path $AppBinDir "VRCFaceTracking.App.exe"
$LogDir    = Join-Path $env:LOCALAPPDATA "VRCFaceTracking\logs"

$swTotal = [System.Diagnostics.Stopwatch]::StartNew()

# 1. Kill running instances ----------------------------------------------------
Write-Host "[quick] Killing running instances..." -ForegroundColor DarkCyan
$procNames = @("VRCFaceTracking","VRCFaceTracking.App",
               "VRCFaceTracking.ModuleHost","VRCFaceTracking.ModuleHostV2")
Get-Process -Name $procNames -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 200  # let file handles release

# 2. Frontend ------------------------------------------------------------------
if (-not $NoFrontend) {
    Write-Host "[quick] Building frontend (dev mode)..." -ForegroundColor DarkCyan
    Push-Location (Join-Path $Root "ui")
    try {
        if (-not (Test-Path "node_modules")) {
            Write-Host "[quick] Installing npm dependencies..." -ForegroundColor DarkGray
            npm install --silent
        }
        npm run build:dev
        if ($LASTEXITCODE -ne 0) { throw "npm run build:dev failed" }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "[quick] Skipping frontend (-NoFrontend)" -ForegroundColor DarkGray
}

# 3. .NET incremental Debug build ---------------------------------------------
Write-Host "[quick] Building .NET (Debug, incremental)..." -ForegroundColor DarkCyan
dotnet build $Slnx -c Debug --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "[quick] Build failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}

# 4. Stage ModuleHost outputs next to App.exe ---------------------------------
# FindHostExe() in UnifiedLibManager.cs searches beside the app exe first,
# so dropping them there is the simplest way to keep dev runs working.
function StageDir($src, $dst, [string[]]$extraArgs = @()) {
    if (-not (Test-Path $src)) {
        Write-Host "[quick]   (skipped, missing) $src" -ForegroundColor DarkGray
        return
    }
    # robocopy: 0=no files, 1-7=success with notes, 8+=error.
    # Out-Null swallows the report; we normalize the exit code so it doesn't
    # bleed into the script's final $LASTEXITCODE.
    $args = @($src, $dst) + $extraArgs + @('/NFL','/NDL','/NJH','/NJS','/NC','/NS','/NP')
    & robocopy @args | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed staging '$src' -> '$dst' (exit $LASTEXITCODE)"
    }
    $global:LASTEXITCODE = 0
}

Write-Host "[quick] Staging ModuleHost binaries..." -ForegroundColor DarkCyan
StageDir (Join-Path $Root "src\VRCFaceTracking.ModuleHost\bin\Debug\net10.0")   $AppBinDir @('/XO')
StageDir (Join-Path $Root "src\VRCFaceTracking.ModuleHostV2\bin\Debug\net10.0") $AppBinDir @('/XO')

# 5. Built-in modules: none. They live in RealWhyKnot/WKVRCFT-Emulation now.
#    For dev work that needs a module loaded, build it from that repo and let the
#    app's normal module loader pick it up from %LOCALAPPDATA%\VRCFaceTracking\modules\

# 6. fti_osc.dll if present and not already next to exe -----------------------
$FtiSrc = Join-Path $Root "fti_osc.dll"
$FtiDst = Join-Path $AppBinDir "fti_osc.dll"
if ((Test-Path $FtiSrc) -and -not (Test-Path $FtiDst)) {
    Copy-Item $FtiSrc $FtiDst
}

$swTotal.Stop()
Write-Host ""
Write-Host "[quick] Built in $([Math]::Round($swTotal.Elapsed.TotalSeconds, 1))s" -ForegroundColor Green
Write-Host "[quick] Log dir: $LogDir" -ForegroundColor Yellow
Write-Host "[quick] Exe:     $AppExe" -ForegroundColor Yellow

# 7. Launch -------------------------------------------------------------------
if ($NoLaunch) {
    Write-Host "[quick] (Skipping launch -- -NoLaunch)" -ForegroundColor DarkGray
    return
}

if (-not (Test-Path $AppExe)) {
    Write-Host "[quick] App exe not found at $AppExe" -ForegroundColor Red
    exit 1
}

Write-Host "[quick] Launching..." -ForegroundColor Green
Start-Process -FilePath $AppExe | Out-Null

if ($ShowLogs) {
    # Spawn a second window that waits for the active log to appear, then tails it.
    # Double-quoted here-string: $LogDir is interpolated now; backticks defer the rest.
    $tailScript = @"
`$dir = '$LogDir'
Write-Host 'Waiting for log file...' -ForegroundColor DarkGray
while (`$true) {
    `$f = Get-ChildItem -Path `$dir -Filter 'vrcft_*.log' -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (`$f) { break }
    Start-Sleep -Milliseconds 200
}
Write-Host ('Tailing ' + `$f.FullName) -ForegroundColor Green
Get-Content -Path `$f.FullName -Wait -Tail 50
"@
    Start-Process powershell -ArgumentList "-NoExit","-NoProfile","-Command",$tailScript | Out-Null
}
