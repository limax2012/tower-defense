param(
    [ValidateSet('Android', 'Web')][string]$Target = 'Android',
    [switch]$Debug,
    [switch]$NoAot,
    [string]$Flutter = ''
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
if (-not $Flutter) {
    $bundled = Join-Path $repository '.build/toolchains/flutter/bin/flutter.bat'
    $Flutter = if (Test-Path -LiteralPath $bundled) { $bundled } else { (Get-Command flutter -ErrorAction Stop).Source }
}
. (Join-Path $PSScriptRoot 'release-version.ps1')
$version = Get-MaximalBastionVersion -Repository $repository
$configuration = if ($Debug) { 'Debug' } else { 'Release' }
& (Join-Path $PSScriptRoot 'prepare-mobile.ps1') -Configuration $configuration -NoAot:$NoAot
if ($LASTEXITCODE -ne 0) { throw 'Shared engine preparation failed.' }
$mobileRoot = Join-Path $repository 'src/MaximalBastion.Mobile'
Push-Location $mobileRoot
try {
    & $Flutter pub get
    if ($LASTEXITCODE -ne 0) { throw 'Flutter dependencies could not be resolved.' }
    & $Flutter analyze
    if ($LASTEXITCODE -ne 0) { throw 'Flutter analysis failed.' }
    & $Flutter test
    if ($LASTEXITCODE -ne 0) { throw 'Flutter tests failed.' }
    if ($Target -eq 'Web') {
        & $Flutter build web --no-web-resources-cdn
        if ($LASTEXITCODE -ne 0) { throw 'Flutter web preview build failed.' }
        Write-Host "Mobile browser preview: $mobileRoot/build/web"
    } else {
        $mode = if ($Debug) { '--debug' } else { '--release' }
        & $Flutter build apk $mode
        if ($LASTEXITCODE -ne 0) { throw 'Android package build failed.' }
        $variant = if ($Debug) { 'debug' } else { 'release' }
        $output = Join-Path $repository ".build/releases/MaximalBastion-$version-Android-$variant.apk"
        New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $mobileRoot "build/app/outputs/flutter-apk/app-$variant.apk") -Destination $output -Force
        Write-Host "Android package (development signing): $output"
    }
} finally { Pop-Location }
