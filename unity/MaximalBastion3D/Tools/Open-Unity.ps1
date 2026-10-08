[CmdletBinding()]
param([string]$EditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$editor = & (Join-Path $PSScriptRoot 'Resolve-Unity.ps1') -EditorPath $EditorPath
# This is the interactive project launcher, so its editor window is visible.
Start-Process -FilePath $editor -ArgumentList @('-projectPath', ('"' + $projectRoot + '"')) | Out-Null
