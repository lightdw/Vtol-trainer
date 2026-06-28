#!/usr/bin/env pwsh
# Hot-reload workflow:
#   1. Edit code under VTOLPayload\
#   2. Run this script (rebuilds + auto-deploys)
#   3. Press F5 in-game to reload — no restart needed
$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot\VTOLPayload\VTOLPayload.csproj" -c Release --nologo -v minimal
