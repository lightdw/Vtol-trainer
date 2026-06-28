# VTOL VR Trainer — one-shot installer.
# Drop this next to VTOLTrainer.dll + VTOLPayload.dll and double-click (or run from PowerShell).
# Auto-detects VTOL VR in common Steam library paths, validates BepInEx,
# and copies both DLLs into BepInEx\plugins\VTOLTrainer\.

param(
    [string]$GameDir = $null,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Say($msg, $color = "Gray") { Write-Host $msg -ForegroundColor $color }

Say "VTOL VR Trainer installer" "Cyan"
Say ""

# 1. Locate the game ---------------------------------------------------------
if (-not $GameDir) {
    $candidates = @()
    # Read Steam library list from registry + libraryfolders.vdf
    try {
        $steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
        if ($steamPath) {
            $candidates += (Join-Path $steamPath "steamapps\common\VTOL VR")
            $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
            if (Test-Path $vdf) {
                $lines = Get-Content $vdf
                foreach ($line in $lines) {
                    if ($line -match '"path"\s+"(.+?)"') {
                        $candidates += (Join-Path $Matches[1].Replace("\\", "\") "steamapps\common\VTOL VR")
                    }
                }
            }
        }
    } catch { }
    # Last-resort guesses
    $candidates += @(
        "C:\Program Files (x86)\Steam\steamapps\common\VTOL VR",
        "D:\SteamLibrary\steamapps\common\VTOL VR",
        "E:\SteamLibrary\steamapps\common\VTOL VR",
        "F:\SteamLibrary\steamapps\common\VTOL VR"
    )

    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c "VTOLVR.exe"))) { $GameDir = $c; break }
    }
}

if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "VTOLVR.exe"))) {
    Say "Couldn't find VTOL VR." "Red"
    Say "Re-run with the path:  .\Install.ps1 -GameDir 'D:\path\to\VTOL VR'" "Yellow"
    exit 1
}
Say "Found game     : $GameDir" "Green"

# 2. Verify BepInEx ----------------------------------------------------------
$bepDir = Join-Path $GameDir "BepInEx"
$doorstop = Join-Path $GameDir "winhttp.dll"
if (-not (Test-Path $bepDir) -or -not (Test-Path $doorstop)) {
    Say ""
    Say "BepInEx is not installed in this game directory." "Red"
    Say "Install BepInEx 5.4.x (x64) first:" "Yellow"
    Say "  https://github.com/BepInEx/BepInEx/releases" "Yellow"
    Say "Extract its contents into:  $GameDir" "Yellow"
    Say "Then run this installer again." "Yellow"
    exit 1
}
Say "Found BepInEx  : $bepDir" "Green"

# 3. Locate the DLLs to install ---------------------------------------------
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

function Find-Dll([string]$name) {
    $tries = @(
        (Join-Path $scriptDir $name),
        (Join-Path $scriptDir "$([System.IO.Path]::GetFileNameWithoutExtension($name))\bin\Release\$name"),
        (Join-Path $scriptDir "$([System.IO.Path]::GetFileNameWithoutExtension($name))\bin\Debug\$name")
    )
    foreach ($t in $tries) { if (Test-Path $t) { return $t } }
    return $null
}

$shimSrc    = Find-Dll "VTOLTrainer.dll"
$payloadSrc = Find-Dll "VTOLPayload.dll"

if (-not $shimSrc -or -not $payloadSrc) {
    Say ""
    Say "Couldn't find the trainer DLLs." "Red"
    Say "Place VTOLTrainer.dll and VTOLPayload.dll next to Install.ps1 and try again." "Yellow"
    exit 1
}
Say "Found shim     : $shimSrc" "Green"
Say "Found payload  : $payloadSrc" "Green"

# 4. Install -----------------------------------------------------------------
$dest = Join-Path $bepDir "plugins\VTOLTrainer"
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest -Force | Out-Null }

$destShim    = Join-Path $dest "VTOLTrainer.dll"
$destPayload = Join-Path $dest "VTOLPayload.dll"

if ((Test-Path $destShim) -and -not $Force) { Say "Overwriting existing shim..." "DarkGray" }
if ((Test-Path $destPayload) -and -not $Force) { Say "Overwriting existing payload..." "DarkGray" }

Copy-Item $shimSrc    $destShim    -Force
Copy-Item $payloadSrc $destPayload -Force

Say ""
Say "Installed:" "Cyan"
Say "  $destShim"
Say "  $destPayload"
Say ""
Say "Launch VTOL VR. Hotkeys: F1 menu, F2 fuel, F3 invuln, F4 weapons, F6 NoG," "Cyan"
Say "F7 countermeasures, F8 repair, F9 timescale, F10 TP, F11 kill, N/M/, thrust." "Cyan"
