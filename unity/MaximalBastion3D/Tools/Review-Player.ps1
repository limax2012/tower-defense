[CmdletBinding()]
param(
    [ValidateSet('Verify', 'Interface', 'InterfaceTest', 'Tour', 'Autoplay')]
    [string]$Mode = 'Verify',
    [string]$PlayerPath,
    [string]$CaptureDirectory,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Player-Manifest.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
if (-not $PlayerPath) { $PlayerPath = Join-Path $repositoryRoot '.artifacts/3d-development/Windows/MaximalBastion3D.exe' }
if (-not (Test-Path -LiteralPath $PlayerPath)) { throw 'Build the Windows player before running review captures.' }
$PlayerPath = [IO.Path]::GetFullPath($PlayerPath)
$playerFiles = @(Get-PlayerFileManifest -Root (Split-Path $PlayerPath -Parent))
if (-not $CaptureDirectory) { $CaptureDirectory = Join-Path $repositoryRoot ".artifacts/3d-development/review-$($Mode.ToLowerInvariant())" }
$CaptureDirectory = [IO.Path]::GetFullPath($CaptureDirectory)
New-Item -ItemType Directory -Force $CaptureDirectory | Out-Null
$verificationManifest = Join-Path $CaptureDirectory 'verified-player.json'
if (Test-Path -LiteralPath $verificationManifest) { Remove-Item -LiteralPath $verificationManifest }
$flag = switch ($Mode) { 'Interface' { '--ui-review' } 'InterfaceTest' { '--ui-verify' } 'Tour' { '--tour' } 'Autoplay' { '--autoplay' } default { '--verify' } }
if ($Mode -eq 'Autoplay' -and $TimeoutSeconds -eq 180) { $TimeoutSeconds = 2400 }
$log = Join-Path $CaptureDirectory 'player.log'
$startedAt = [DateTime]::UtcNow
$arguments = @('-batchmode', '-screen-fullscreen', '0', '-screen-width', $Width, '-screen-height', $Height,
    '-logFile', ('"' + $log + '"'), $flag, '--capture-directory', ('"' + $CaptureDirectory + '"'))
$process = Start-Process -FilePath ([IO.Path]::GetFullPath($PlayerPath)) -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id
    throw "Player review exceeded $TimeoutSeconds seconds. Inspect $log"
}
if ($process.ExitCode -ne 0) { throw "Player review failed with exit code $($process.ExitCode). Inspect $log" }
if ((Get-Content -LiteralPath $log -Raw) -match '(?m)^(?:[A-Za-z0-9_.]*(?:Exception|Error):|Assertion failed|Shader error)') {
    throw "Player review logged a runtime error. Inspect $log"
}
$screenshots = @(Get-ChildItem -LiteralPath $CaptureDirectory -Filter '*.png' | Where-Object { $_.LastWriteTimeUtc -ge $startedAt -and $_.Length -gt 1024 })
$requiredImages = if ($Mode -eq 'Interface') { 16 } else { 6 }
if ($screenshots.Count -lt $requiredImages) { throw "The player produced $($screenshots.Count) fresh screenshots; expected at least $requiredImages. Inspect $log" }
if ($Mode -eq 'InterfaceTest') {
    $report = Join-Path $CaptureDirectory 'interface-verification.txt'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt -or
        (Get-Content -LiteralPath $report -Raw) -notmatch '(?m)^Passed: True\s*$') {
        throw "Interface interaction checks failed. Inspect $report"
    }
}
Assert-PlayerFileManifest -Expected $playerFiles -Actual @(Get-PlayerFileManifest -Root (Split-Path $PlayerPath -Parent))
[ordered]@{
    passed = $true
    mode = $Mode
    executable = $PlayerPath
    verifiedUtc = [DateTime]::UtcNow.ToString('O')
    screenshots = $screenshots.Count
    files = $playerFiles
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $verificationManifest -Encoding UTF8
Write-Output "$Mode review produced $($screenshots.Count) screenshots in $CaptureDirectory"
