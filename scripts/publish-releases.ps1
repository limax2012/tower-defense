param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "publish-windows.ps1") -Configuration $Configuration
& (Join-Path $PSScriptRoot "publish-browser.ps1") -Configuration $Configuration
