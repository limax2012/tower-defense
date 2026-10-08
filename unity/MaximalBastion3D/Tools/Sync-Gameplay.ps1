[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$gameplayProject = Join-Path $repositoryRoot 'src/MaximalBastion.Gameplay/MaximalBastion.Gameplay.csproj'
$output = Join-Path $repositoryRoot 'src/MaximalBastion.Gameplay/bin/Release/netstandard2.1'
if (-not $SkipBuild) {
    $localDotnet = Join-Path $repositoryRoot '.dotnet/dotnet.exe'
    $dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
    & $dotnet build $gameplayProject -c Release --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Portable gameplay build failed.' }
}
$plugins = Join-Path $projectRoot 'Assets/Plugins/Gameplay'
New-Item -ItemType Directory -Force $plugins | Out-Null
# Unity's .NET Standard 2.1 runtime supplies these framework assemblies.
$frameworkAssemblies = @('System.Buffers.dll', 'System.Memory.dll', 'System.Numerics.Vectors.dll', 'System.Threading.Tasks.Extensions.dll')
$copied = @()
Get-ChildItem -LiteralPath $output -Filter '*.dll' | Where-Object { $_.Name -notin $frameworkAssemblies } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $plugins $_.Name) -Force
    $copied += $_.Name
}
if ('MaximalBastion.Gameplay.dll' -notin $copied) { throw 'The portable gameplay assembly was not found.' }
$contentSource = Join-Path $repositoryRoot 'src/MaximalBastion/ContentData'
$contentTarget = Join-Path $projectRoot 'Assets/StreamingAssets/ContentData'
New-Item -ItemType Directory -Force $contentTarget | Out-Null
Get-ChildItem -LiteralPath $contentSource -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($contentSource.Length).TrimStart('\', '/')
    $destination = Join-Path $contentTarget $relative
    New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Write-Output ("Gameplay plugins: " + ($copied -join ', '))
Write-Output "Authored content: $contentTarget"
foreach ($fixtureName in @('runtime-conformance.json', 'runtime-conformance-mono.json', 'runtime-conformance-unity.json')) {
    $fixtureSource = Join-Path $repositoryRoot ('.artifacts/3d-development/' + $fixtureName)
    if (Test-Path -LiteralPath $fixtureSource) {
        $verificationTarget = Join-Path $projectRoot 'Assets/StreamingAssets/Verification'
        New-Item -ItemType Directory -Force $verificationTarget | Out-Null
        Copy-Item -LiteralPath $fixtureSource -Destination (Join-Path $verificationTarget $fixtureName) -Force
        Write-Output "Runtime conformance fixture synchronized: $fixtureName"
    }
}
