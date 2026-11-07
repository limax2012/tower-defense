param(
    [Parameter(Mandatory = $true)][string]$Trace,
    [Parameter(Mandatory = $true)][string]$Output,
    [ValidateRange(0.25, 120)][double]$Speed = 12,
    [ValidateRange(1, 120)][int]$Fps = 24,
    [ValidateRange(1, 1000)][int]$FromWave = 1,
    [ValidateRange(0, 1000000)][int]$MaxFrames = 0,
    [string]$Ffmpeg = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$tracePath = [IO.Path]::GetFullPath($Trace)
$outputPath = [IO.Path]::GetFullPath($Output)
if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) { throw "Replay trace does not exist: $tracePath" }
if (-not [IO.Path]::GetExtension($outputPath).Equals('.mp4', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output filename must end in .mp4.'
}
if ([string]::IsNullOrWhiteSpace($Ffmpeg)) {
    $encoderCommand = Get-Command ffmpeg -ErrorAction Stop
    $Ffmpeg = $encoderCommand.Source
}
$encoderPath = (Get-Command $Ffmpeg -ErrorAction Stop).Source
$dotnetExecutable = Join-Path $repositoryRoot '.dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable)) { $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source }
$env:PATH = "$(Split-Path -Parent $dotnetExecutable);$env:PATH"
$buildRoot = Join-Path $repositoryRoot '.artifacts/replay-export'
$gameAssembly = Join-Path $buildRoot 'bin/MaximalBastion/release/MaximalBastion.dll'

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & $dotnetExecutable build src/MaximalBastion/MaximalBastion.csproj -c Release --artifacts-path $buildRoot --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Replay exporter build failed.' }
    }
    if (-not (Test-Path -LiteralPath $gameAssembly -PathType Leaf)) { throw "Replay exporter assembly is missing: $gameAssembly" }
    $arguments = @($gameAssembly, '--render-strategy-replay', $tracePath, $outputPath,
        '--speed', $Speed.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--fps', $Fps.ToString([Globalization.CultureInfo]::InvariantCulture), '--ffmpeg', $encoderPath)
    if ($FromWave -ne 1) { $arguments += @('--from-wave', "$FromWave") }
    if ($MaxFrames -gt 0) { $arguments += @('--max-frames', "$MaxFrames") }
    & $dotnetExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Replay rendering or video encoding failed.' }
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) { throw 'The exporter did not produce its requested video.' }
    Write-Output "Replay video: $outputPath"
}
finally { Pop-Location }
