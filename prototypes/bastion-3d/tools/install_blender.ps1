param(
    [string]$Version = '4.5.7',
    [string]$Destination = (Join-Path $PSScriptRoot '..\.tools\blender')
)

$ErrorActionPreference = 'Stop'
$toolRoot = [System.IO.Path]::GetFullPath($Destination)
$releaseSeries = ($Version.Split('.')[0..1] -join '.')
$archiveName = "blender-$Version-windows-x64.zip"
$archivePath = Join-Path $toolRoot $archiveName
$hashPath = Join-Path $toolRoot "blender-$Version.sha256"
$executable = Join-Path $toolRoot "blender-$Version-windows-x64\blender.exe"
$baseUrl = "https://download.blender.org/release/Blender$releaseSeries"
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $archivePath)) {
    & curl.exe --fail --location --retry 2 --output $archivePath "$baseUrl/$archiveName"
    if ($LASTEXITCODE -ne 0) { throw 'The Blender archive download failed.' }
}
if (-not (Test-Path -LiteralPath $hashPath)) {
    & curl.exe --fail --location --retry 2 --output $hashPath "$baseUrl/blender-$Version.sha256"
    if ($LASTEXITCODE -ne 0) { throw 'The Blender checksum download failed.' }
}
$hashLine = Get-Content -LiteralPath $hashPath | Where-Object { $_ -match [regex]::Escape($archiveName) } | Select-Object -First 1
if (-not $hashLine) { throw 'The official checksum file does not include the requested archive.' }
$expectedHash = ($hashLine.Trim() -split '\s+')[0]
$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actualHash -ine $expectedHash) { throw 'The Blender archive does not match its official SHA-256 checksum.' }
if (-not (Test-Path -LiteralPath $executable)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $toolRoot
}
New-Item -ItemType Directory -Path (Join-Path (Split-Path $executable) 'portable') -Force | Out-Null
& $executable --background --factory-startup --python-exit-code 1 --python-expr 'import bpy; print(bpy.app.version_string)'
if ($LASTEXITCODE -ne 0) { throw 'The Blender background launch failed.' }
Write-Output $executable
