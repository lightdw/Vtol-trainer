@echo off
REM Double-click launcher for vtol-log.ps1
start "VTOL VR Trainer Log" powershell.exe -NoExit -ExecutionPolicy Bypass -File "%~dp0vtol-log.ps1"
