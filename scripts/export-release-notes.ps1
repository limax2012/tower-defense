param(
    [string]$OutputPath = "",
    [ValidateSet("Changelog", "Release")]
    [string]$Format = "Changelog"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "release-version.ps1")
$version = Get-MinimalBastionVersion -Repository $repository
$sourcePath = Join-Path $repository "src\MinimalBastion\ContentData\Releases.json"
$catalog = Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json
$release = @($catalog.releases) | Where-Object { $_.version -eq $version } | Select-Object -First 1
if ($null -eq $release) {
    throw "Release notes do not contain version $version."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repository "CHANGELOG.md"
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$heading = if ($Format -eq "Release") { "# Minimal Bastion $version" } else { "# Changelog`n`n## $version" }
$lines = @($heading, "")
$lines += @($release.changes | ForEach-Object { "- $_" })
$lines += ""
$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Host "Release notes: $OutputPath"
