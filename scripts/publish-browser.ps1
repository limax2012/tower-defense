param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$buildRoot = Join-Path $repository ".build"
$publishRoot = Join-Path $buildRoot "browser-publish"
$siteRoot = Join-Path $buildRoot "browser"
$archivePath = Join-Path $buildRoot "MinimalBastion-Browser.zip"
$projectPath = Join-Path $repository "src\MinimalBastion.Web\MinimalBastion.Web.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }

New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
foreach ($target in @($publishRoot, $siteRoot)) {
    $resolvedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $target))
    if ($resolvedParent -ne [System.IO.Path]::GetFullPath($buildRoot)) {
        throw "Browser package target must remain inside .build."
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
Compress-Archive -Path (Join-Path $siteRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Browser site: $siteRoot"
Write-Host "Upload archive: $archivePath"
