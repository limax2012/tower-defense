param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$releasesRoot = Join-Path $repository ".build\releases"
$publishRoot = Join-Path $releasesRoot "windows"
$archivePath = Join-Path $releasesRoot "MinimalBastion-Windows.zip"
$projectPath = Join-Path $repository "src\MinimalBastion\MinimalBastion.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }

New-Item -ItemType Directory -Path $releasesRoot -Force | Out-Null
if ([System.IO.Path]::GetFullPath((Split-Path -Parent $publishRoot)) -ne
    [System.IO.Path]::GetFullPath($releasesRoot)) {
    throw "Windows package target must remain inside .build\releases."
}
if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }

$dotnetDirectory = Split-Path -Parent $dotnet
$env:Path = "$dotnetDirectory;$env:Path"
& $dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -o $publishRoot --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed with exit code $LASTEXITCODE." }

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Windows build: $publishRoot"
Write-Host "Upload archive: $archivePath"
