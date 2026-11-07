param(
    [ValidateSet('smoke', 'standard', 'deep')][string]$Budget = 'smoke',
    [string]$OutputDirectory = '.build/planner/reusable-audit',
    [int]$Seed = 1337,
    [switch]$SkipBuild,
    [switch]$SkipMatrix
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dotnetExecutable = Join-Path $repositoryRoot '.dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable)) { $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source }
$env:PATH = "$(Split-Path -Parent $dotnetExecutable);$env:PATH"
$plannerAssembly = Join-Path $repositoryRoot '.artifacts/planning/bin/MaximalBastion.Tests/release/MaximalBastion.Tests.dll'
$limits = switch ($Budget) {
    'smoke' { @{ Simulations = 80; Seconds = 15; Breadth = 1; Workers = 2 } }
    'standard' { @{ Simulations = 2000; Seconds = 1200; Breadth = 10; Workers = 2 } }
    'deep' { @{ Simulations = 25000; Seconds = 14400; Breadth = 10; Workers = 4 } }
}

function Invoke-Planner([string[]]$Arguments) {
    & $dotnetExecutable $plannerAssembly @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Planner failed: $($Arguments -join ' ')" }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & $dotnetExecutable build tests/MaximalBastion.Tests/MaximalBastion.Tests.csproj -c Release --artifacts-path .artifacts/planning --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Planner build failed.' }
    }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $calibrations = [ordered]@{
        'hard-calibration' = 'tests/MaximalBastion.Tests/Fixtures/helix-bastion-signal-plan.json'
        'prism-calibration' = 'tests/MaximalBastion.Tests/Fixtures/prism-bastion-signal-plan.json'
        'rainline-calibration' = 'tests/MaximalBastion.Tests/Fixtures/rainline-bastion-signal-plan.json'
        'cinderworks-calibration' = 'tests/MaximalBastion.Tests/Fixtures/cinderworks-bastion-signal-plan.json'
    }
    @{ Budget = $Budget; Seed = $Seed; Limits = $limits; Matrix = (-not $SkipMatrix.IsPresent)
        Calibrations = @{ Plans = $calibrations; Simulations = 32; Seconds = 120; Seed = 1337 } } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'configuration.json')
    Invoke-Planner @('--planner-tests')
    Invoke-Planner @('--planner-diagnostics', '--output', (Join-Path $OutputDirectory 'diagnostics'))
    foreach ($control in $calibrations.GetEnumerator()) {
        Invoke-Planner @('--planner-layout-campaign', '--plan', $control.Value,
            '--simulations', '32', '--seconds', '120', '--beam', '1', '--output', (Join-Path $OutputDirectory $control.Key))
    }
    $common = @('--budget', $Budget, '--simulations', "$($limits.Simulations)", '--seconds', "$($limits.Seconds)",
        '--breadth', "$($limits.Breadth)", '--workers', "$($limits.Workers)", '--seed', "$Seed")
    foreach ($baseline in @('random', 'greedy', 'experienced', 'planner')) {
        Invoke-Planner (@('--planner-solve', '--map', 'relay_divide', '--difficulty', 'bastion', '--challenge', 'close_quarters',
            '--baseline', $baseline, '--output', (Join-Path $OutputDirectory "hard-$baseline")) + $common)
    }
    foreach ($family in @('unrestricted', 'concentrated', 'coverage', 'range', 'area', 'control', 'counter', 'economy', 'deployable')) {
        Invoke-Planner (@('--planner-solve', '--map', 'prism_circuit', '--difficulty', 'easy', '--challenge', 'standard',
            '--family', $family, '--output', (Join-Path $OutputDirectory "portfolio-$family")) + $common)
    }
    foreach ($ablation in @('no-placement', 'no-combat-purchases', 'no-support', 'no-saving', 'no-backtracking')) {
        Invoke-Planner (@('--planner-solve', '--map', 'prism_circuit', '--difficulty', 'easy', '--challenge', 'standard',
            '--family', 'coverage', "--$ablation", '--output', (Join-Path $OutputDirectory "ablation-$ablation")) + $common)
    }
    foreach ($profile in @('informed', 'low-micro')) {
        Invoke-Planner (@('--planner-solve', '--map', 'prism_circuit', '--difficulty', 'easy', '--challenge', 'standard',
            '--baseline', 'greedy', '--information', 'informed', '--execution', $profile,
            '--output', (Join-Path $OutputDirectory "profile-$profile")) + $common)
    }
    if (-not $SkipMatrix) {
        Invoke-Planner (@('--planner-audit', '--output', (Join-Path $OutputDirectory 'matrix')) + $common)
    }
}
finally { Pop-Location }
