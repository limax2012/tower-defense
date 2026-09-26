param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoAot
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $repository 'src/MaximalBastion.Mobile'
$publishRoot = Join-Path $repository '.build/mobile-runtime'
$assetRoot = Join-Path $mobileRoot 'assets/runtime'
$localDotnet = Join-Path $repository '.dotnet/dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:Path = "$(Split-Path $dotnet);$env:Path"
$aot = if ($Configuration -eq 'Release' -and -not $NoAot) { 'true' } else { 'false' }

foreach ($target in @($publishRoot, $assetRoot)) {
    $absolute = [IO.Path]::GetFullPath($target)
    $allowed = @([IO.Path]::GetFullPath((Join-Path $repository '.build/mobile-runtime')),
        [IO.Path]::GetFullPath((Join-Path $mobileRoot 'assets/runtime')))
    if ($absolute -notin $allowed) { throw 'Mobile output must remain in the designated generated directories.' }
    if (Test-Path -LiteralPath $absolute) { Remove-Item -LiteralPath $absolute -Recurse -Force }
}

& $dotnet publish (Join-Path $repository 'src/MaximalBastion.Web/MaximalBastion.Web.csproj') `
    -c $Configuration -o $publishRoot "/p:RunAOTCompilation=$aot" `
    --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false /p:BlazorWebAssemblyJiterpreter=false
if ($LASTEXITCODE -ne 0) { throw "Shared mobile runtime publish failed: $LASTEXITCODE" }
$siteRoot = Join-Path $publishRoot 'wwwroot'
foreach ($required in @('index.html', 'native-bootstrap.js', 'js/game-host.js', 'Content/Audio/MainMenuLoop.xnb')) {
    if (-not (Test-Path -LiteralPath (Join-Path $siteRoot $required))) { throw "Runtime is missing $required" }
}
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
Copy-Item -Path (Join-Path $siteRoot '*') -Destination $assetRoot -Recurse -Force
$prefix = [IO.Path]::GetFullPath($assetRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$files = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -File | ForEach-Object {
    $_.FullName.Substring($prefix.Length).Replace('\', '/')
} | Sort-Object)
@{ protocol = 1; configuration = $Configuration; aot = ($aot -eq 'true'); files = $files } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $assetRoot 'runtime-manifest.json') -Encoding UTF8

$directories = @('assets/runtime/') + @(Get-ChildItem -LiteralPath $assetRoot -Directory -Recurse | ForEach-Object {
    'assets/runtime/' + $_.FullName.Substring($prefix.Length).Replace('\', '/') + '/'
} | Sort-Object)
$assetLines = ($directories | ForEach-Object { '    - ' + $_ }) -join "`n"
$pubspecPath = Join-Path $mobileRoot 'pubspec.yaml'
$pubspec = [IO.File]::ReadAllText($pubspecPath)
$updated = [regex]::Replace($pubspec, '(?s)(    # runtime-assets-start\r?\n).*?(    # runtime-assets-end)',
    [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $match.Groups[1].Value + $assetLines + "`n" + $match.Groups[2].Value })
if ($updated -eq $pubspec -and -not $pubspec.Contains('runtime-assets-start')) { throw 'Runtime asset markers are missing from pubspec.yaml.' }
[IO.File]::WriteAllText($pubspecPath, $updated)
Write-Host "Shared C# runtime prepared: $assetRoot ($($files.Count) files, AOT $aot)"
