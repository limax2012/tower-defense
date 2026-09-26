param(
    [string]$OutputPath = "",
    [ValidateSet("Changelog", "Release")]
    [string]$Format = "Changelog"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "release-version.ps1")
$version = Get-MaximalBastionVersion -Repository $repository
$sourcePath = Join-Path $repository "src\MaximalBastion\ContentData\Releases.json"
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

$lines = if ($Format -eq "Release") {
    @("# Maximal Bastion $version", "") + @($release.changes | ForEach-Object { "- $_" }) + ""
} else {
    $changelog = @("# Changelog", "")
    foreach ($entry in @($catalog.releases)) {
        $changelog += @("## $($entry.version)", "")
        $changelog += @($entry.changes | ForEach-Object { "- $_" })
        $changelog += ""
    }
    $changelog
}
$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Host "Release notes: $OutputPath"
