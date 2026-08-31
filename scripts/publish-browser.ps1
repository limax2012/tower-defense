param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$buildRoot = Join-Path $repository ".build"
$releasesRoot = Join-Path $buildRoot "releases"
$publishRoot = Join-Path $releasesRoot ".browser-publish"
$siteRoot = Join-Path $releasesRoot "browser"
$statePath = Join-Path $releasesRoot ".browser-build-state.json"
$projectPath = Join-Path $repository "src\MinimalBastion.Web\MinimalBastion.Web.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }
. (Join-Path $PSScriptRoot "browser-build-state.ps1")
. (Join-Path $PSScriptRoot "release-version.ps1")
$version = Get-MinimalBastionVersion -Repository $repository
$archivePath = Join-Path $releasesRoot "MinimalBastion-$version-Browser.zip"
$requiredContent = @(
    "Content\Fonts\Interface.xnb",
    "Content\Audio\MainMenuLoop.xnb",
    "Content\Audio\Music\BassyRollIn.xnb",
    "Content\Audio\Music\ChillStroll.xnb",
    "Content\Audio\Music\FocusedDanceParty.xnb",
    "Content\Audio\Music\IcyInvestigation.xnb",
    "Content\Audio\Music\KickbackHall.xnb",
    "Content\Audio\Music\MildlyUpbeatArpeggios.xnb",
    "Content\Audio\Music\NightclubHalo.xnb",
    "Content\Audio\Music\QuietInvestigation.xnb",
    "Content\Audio\Music\SneakFocusMission.xnb",
    "Content\Audio\Music\XylophoneBallad.xnb"
)

New-Item -ItemType Directory -Path $releasesRoot -Force | Out-Null
foreach ($target in @($publishRoot, $siteRoot)) {
    $resolvedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $target))
    if ($resolvedParent -ne [System.IO.Path]::GetFullPath($releasesRoot)) {
        throw "Browser package target must remain inside .build\releases."
    }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }

$dotnetDirectory = Split-Path -Parent $dotnet
$env:Path = "$dotnetDirectory;$env:Path"
& $dotnet publish $projectPath -c $Configuration -o $publishRoot --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "Browser publish failed with exit code $LASTEXITCODE." }

$publishedSite = Join-Path $publishRoot "wwwroot"
if (-not (Test-Path -LiteralPath (Join-Path $publishedSite "index.html"))) {
    throw "Browser publish did not produce wwwroot\index.html."
}
foreach ($contentPath in $requiredContent) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishedSite $contentPath) -PathType Leaf)) {
        throw "Browser publish is missing required content: $contentPath"
    }
}

New-Item -ItemType Directory -Path $siteRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishedSite "*") -Destination $siteRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repository "CHANGELOG.md") -Destination (Join-Path $siteRoot "CHANGELOG.md") -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open(
    $archivePath,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $siteRootPath = [System.IO.Path]::GetFullPath($siteRoot).TrimEnd('\', '/')
    foreach ($file in Get-ChildItem -LiteralPath $siteRoot -File -Recurse) {
        $relativePath = $file.FullName.Substring($siteRootPath.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $relativePath,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$archiveCheck = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($null -eq $archiveCheck.GetEntry("index.html")) {
        throw "Browser archive does not contain index.html at its root."
    }
    foreach ($contentPath in $requiredContent) {
        $archiveContentPath = $contentPath.Replace('\', '/')
        if ($null -eq $archiveCheck.GetEntry($archiveContentPath)) {
            throw "Browser archive is missing required content: $archiveContentPath"
        }
    }
    if ($archiveCheck.Entries.FullName.Where({ $_.Contains('\') }, 'First').Count -ne 0) {
        throw "Browser archive contains non-portable path separators."
    }
}
finally {
    $archiveCheck.Dispose()
}
Remove-Item -LiteralPath $publishRoot -Recurse -Force

@{
    Configuration = $Configuration
    Fingerprint = Get-BrowserBuildFingerprint -Repository $repository
    CreatedUtc = [DateTime]::UtcNow.ToString("O")
} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8

Write-Host "Browser site: $siteRoot"
Write-Host "Upload archive: $archivePath"
