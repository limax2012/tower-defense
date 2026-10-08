[CmdletBinding()]
param([string]$EditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
$version = ($versionLine -split ': ', 2)[1].Trim()
if ($EditorPath) {
    if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw "The explicit Unity editor path does not exist: $EditorPath" }
    $explicitEditor = (Get-Item -LiteralPath $EditorPath)
    $actualVersion = ($explicitEditor.VersionInfo.ProductVersion -split '_', 2)[0]
    if ($actualVersion -ne $version) { Write-Warning "Explicit editor override $actualVersion differs from the pinned project version $version." }
    return $explicitEditor.FullName
}
$candidates = @(
    $env:UNITY_EDITOR,
    (Join-Path $env:SystemDrive "UnityEditors/$version/Editor/Unity.exe"),
    (Join-Path $repositoryRoot ".artifacts/3d-development/Unity/$version/Editor/Unity.exe"),
    (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"),
    (Join-Path $env:ProgramFiles "Unity $version/Editor/Unity.exe")
)
foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        $file = Get-Item -LiteralPath $candidate
        $actualVersion = ($file.VersionInfo.ProductVersion -split '_', 2)[0]
        if ($actualVersion -eq $version) { return $file.FullName }
        Write-Warning "Ignoring Unity $actualVersion at $candidate; this project requires $version."
    }
}
throw "Unity $version was not found. Run Tools/Install-Unity.ps1, set UNITY_EDITOR, or pass -EditorPath."
