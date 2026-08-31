param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$releasesRoot = Join-Path $repository ".build\releases"
. (Join-Path $PSScriptRoot "release-version.ps1")
$version = Get-MinimalBastionVersion -Repository $repository
$releaseNotesPath = Join-Path $releasesRoot "MinimalBastion-$version-ReleaseNotes.md"

& (Join-Path $PSScriptRoot "export-release-notes.ps1")
& (Join-Path $PSScriptRoot "export-release-notes.ps1") -OutputPath $releaseNotesPath -Format Release
& (Join-Path $PSScriptRoot "publish-windows.ps1") -Configuration $Configuration
& (Join-Path $PSScriptRoot "publish-browser.ps1") -Configuration $Configuration

$artifacts = @(
    (Join-Path $releasesRoot "MinimalBastion-$version-Windows.zip"),
    (Join-Path $releasesRoot "MinimalBastion-$version-Browser.zip"),
    $releaseNotesPath
)
$checksumPath = Join-Path $releasesRoot "SHA256SUMS.txt"
$artifacts | ForEach-Object {
    if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) {
        throw "Missing release artifact: $_"
    }
    $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($_))"
} | Set-Content -LiteralPath $checksumPath -Encoding ASCII

Write-Host "Checksums: $checksumPath"
