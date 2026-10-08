[CmdletBinding()]
param([string]$EditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
$version = ($versionLine -split ': ', 2)[1].Trim()
$candidates = @(
    $EditorPath,
    $env:UNITY_EDITOR,
    (Join-Path $env:SystemDrive "UnityEditors/$version/Editor/Unity.exe"),
    (Join-Path $repositoryRoot ".artifacts/3d-development/Unity/$version/Editor/Unity.exe"),
    (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"),
    (Join-Path $env:ProgramFiles "Unity $version/Editor/Unity.exe")
)
foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
}
throw "Unity $version was not found. Run Tools/Install-Unity.ps1, set UNITY_EDITOR, or pass -EditorPath."
