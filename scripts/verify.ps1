param(
    [string]$ArtifactDirectory = "",
    [switch]$SkipVisuals
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnetExecutable = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnetExecutable)) {
    $installedDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $installedDotnet) {
        throw "The .NET 10 SDK was not found. Install it or provide a workspace-local SDK at: $dotnetExecutable"
    }
    $dotnetExecutable = $installedDotnet.Source
}

if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot ".artifacts\verification"
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
$isolatedBuildRoot = Join-Path $env:TEMP "MinimalBastionVerification"
$isolatedOutput = Join-Path $isolatedBuildRoot "bin\"
$testLog = Join-Path $ArtifactDirectory "tests.txt"
$uiDirectory = Join-Path $ArtifactDirectory "ui"

New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $uiDirectory | Out-Null
$env:PATH = "$(Split-Path -Parent $dotnetExecutable);$env:PATH"

Push-Location $repositoryRoot
try {
    & $dotnetExecutable build "MinimalBastion.sln" -c Debug "-p:BaseOutputPath=$isolatedOutput"
    if ($LASTEXITCODE -ne 0) { throw "Isolated verification build failed." }

    $outputDirectory = Join-Path $isolatedOutput "Debug\net10.0"
    $testAssembly = Join-Path $outputDirectory "MinimalBastion.Tests.dll"
    $gameAssembly = Join-Path $outputDirectory "MinimalBastion.dll"

    & $dotnetExecutable $testAssembly 2>&1 | Tee-Object -FilePath $testLog
    if ($LASTEXITCODE -ne 0) { throw "Regression tests failed." }

    if (-not $SkipVisuals) {
        & $dotnetExecutable $gameAssembly --verify-ui $uiDirectory
        if ($LASTEXITCODE -ne 0) { throw "Hidden UI verification failed." }
    }
}
finally {
    Pop-Location
}

Write-Host "Verification complete."
Write-Host "Artifacts: $ArtifactDirectory"
