[CmdletBinding()]
param(
    [ValidateSet('Import', 'ConfigureProject', 'Verify', 'BuildWindows', 'BuildWindowsRelease')]
    [string]$Task = 'BuildWindows',
    [string]$EditorPath,
    [string]$LogPath,
    [switch]$NoGraphics
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$editor = & (Join-Path $PSScriptRoot 'Resolve-Unity.ps1') -EditorPath $EditorPath
if (-not $LogPath) {
    $LogPath = Join-Path $repositoryRoot ".artifacts/3d-development/unity-$($Task.ToLowerInvariant()).log"
}
$LogPath = [IO.Path]::GetFullPath($LogPath)
New-Item -ItemType Directory -Force (Split-Path $LogPath -Parent) | Out-Null
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + $LogPath + '"'))
if ($NoGraphics) { $arguments += '-nographics' }
if ($Task -ne 'Import') { $arguments += @('-executeMethod', "Bastion3D.Editor.BuildAutomation.$Task") }
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
$exitCode = $process.ExitCode
if ($exitCode -ne 0) {
    $licenseError = $exitCode -eq 198
    if (-not $licenseError -and (Test-Path -LiteralPath $LogPath)) {
        $licenseError = [bool]((Get-Content -LiteralPath $LogPath -Tail 40) -match 'No valid Unity Editor license found|No valid license found')
    }
    if ($licenseError) {
        throw "Unity requires an activated license. Sign in to Unity Hub, then use Settings > Licenses if activation is required. Retry the same command. Log: $LogPath"
    }
    throw "Unity $Task failed with exit code $exitCode. Inspect $LogPath"
}
Write-Output "Unity $Task completed. Log: $LogPath"
