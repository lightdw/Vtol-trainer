#Requires -Version 5.1
# Live tail of the VTOL VR BepInEx log with color-coding and restart-detection.
# Double-click vtol-log.cmd (or run this directly) to open in its own window.

$LogPath = 'D:\SteamLibrary\steamapps\common\VTOL VR\BepInEx\LogOutput.log'
$Host.UI.RawUI.WindowTitle = "VTOL VR Trainer — Live Log"

function Write-LogLine($line) {
    if ($line -match '^\[Error') {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match '^\[Warning') {
        Write-Host $line -ForegroundColor Yellow
    } elseif ($line -match '^\[Message') {
        Write-Host $line -ForegroundColor Cyan
    } elseif ($line -match '^\[Info\s*:VTOL VR Trainer') {
        Write-Host $line -ForegroundColor Green
    } elseif ($line -match 'VTOLPayload|VTOLTrainer|Harmony patches|FuelTank|Blackout|VrCanvas|payload') {
        Write-Host $line -ForegroundColor Green
    } else {
        Write-Host $line -ForegroundColor DarkGray
    }
}

Write-Host "Watching: $LogPath" -ForegroundColor Magenta
Write-Host "Ctrl+C to quit. Auto-reopens when the game restarts." -ForegroundColor DarkMagenta
Write-Host ('-' * 80) -ForegroundColor DarkMagenta

while ($true) {
    # Wait for the file to exist (game hasn't been launched yet).
    while (-not (Test-Path $LogPath)) {
        Write-Host "Waiting for log file..." -ForegroundColor DarkMagenta
        Start-Sleep -Seconds 2
    }

    # Track size so we can detect a truncation/recreation on game restart.
    $lastSize = (Get-Item $LogPath).Length
    $reader = $null
    try {
        $stream = [System.IO.File]::Open(
            $LogPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
        $reader = New-Object System.IO.StreamReader($stream)
        $reader.BaseStream.Seek(0, [System.IO.SeekOrigin]::End) | Out-Null

        while ($true) {
            $line = $reader.ReadLine()
            if ($null -ne $line) {
                Write-LogLine $line
                continue
            }
            Start-Sleep -Milliseconds 200

            # Restart detection: file shrank (BepInEx rewrites from 0 on game start).
            if (Test-Path $LogPath) {
                $size = (Get-Item $LogPath).Length
                if ($size -lt $lastSize) {
                    Write-Host ('=' * 80) -ForegroundColor Magenta
                    Write-Host "Game restart detected — reopening log..." -ForegroundColor Magenta
                    Write-Host ('=' * 80) -ForegroundColor Magenta
                    break
                }
                $lastSize = $size
            } else {
                break
            }
        }
    } finally {
        if ($reader) { $reader.Dispose() }
    }
}
