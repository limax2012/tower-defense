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
$archivePath = Join-Path $releasesRoot "MinimalBastion-Browser.zip"
$statePath = Join-Path $releasesRoot ".browser-build-state.json"
$projectPath = Join-Path $repository "src\MinimalBastion.Web\MinimalBastion.Web.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }
. (Join-Path $PSScriptRoot "browser-build-state.ps1")

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

New-Item -ItemType Directory -Path $siteRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishedSite "*") -Destination $siteRoot -Recurse -Force

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
