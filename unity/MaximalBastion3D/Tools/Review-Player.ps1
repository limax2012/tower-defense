[CmdletBinding()]
param(
    [ValidateSet('Verify', 'Interface', 'InterfaceTest', 'Tour', 'Autoplay', 'RenderDiagnostics', 'AudioDiagnostics', 'Motion', 'Wreckage')]
    [string]$Mode = 'Verify',
    [string]$PlayerPath,
    [string]$CaptureDirectory,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$TimeoutSeconds = 180,
    [switch]$Video
)

$ErrorActionPreference = 'Stop'
if ($Video -and $Mode -notin @('Motion', 'Wreckage')) { throw '-Video is supported for Motion and Wreckage review modes.' }
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
$flag = switch ($Mode) { 'Interface' { '--ui-review' } 'InterfaceTest' { '--ui-verify' } 'Tour' { '--tour' } 'Autoplay' { '--autoplay' } 'RenderDiagnostics' { '--render-diagnostics' } 'AudioDiagnostics' { '--audio-diagnostics' } 'Motion' { '--motion-review' } 'Wreckage' { '--wreckage-review' } default { '--verify' } }
if ($Mode -eq 'Autoplay' -and $TimeoutSeconds -eq 180) { $TimeoutSeconds = 2400 }
if ($Video -and $TimeoutSeconds -eq 180) { $TimeoutSeconds = 300 }
$log = Join-Path $CaptureDirectory 'player.log'
$startedAt = [DateTime]::UtcNow
$arguments = @('-screen-fullscreen', '0', '-screen-width', $Width, '-screen-height', $Height,
    '-logFile', ('"' + $log + '"'), $flag, '--capture-directory', ('"' + $CaptureDirectory + '"'))
if ($Mode -ne 'AudioDiagnostics') { $arguments = @('-batchmode') + $arguments }
if ($Video) { $arguments += '--video' }
$process = Start-Process -FilePath ([IO.Path]::GetFullPath($PlayerPath)) -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id
    throw "Player review exceeded $TimeoutSeconds seconds. Inspect $log"
}
if ($process.ExitCode -ne 0) { throw "Player review failed with exit code $($process.ExitCode). Inspect $log" }
$runtimeErrorPattern = '(?im)^(?:[A-Za-z0-9_.]*(?:Exception|Error):|Assertion failed|Shader error|Not allowed to access (?:vertices|triangles|indices|normals|tangents|colors|uvs?)\b|Failed to capture screen shot)'
if ((Get-Content -LiteralPath $log -Raw) -match $runtimeErrorPattern) {
    throw "Player review logged a runtime error. Inspect $log"
}
$screenshots = @(Get-ChildItem -LiteralPath $CaptureDirectory -Filter '*.png' | Where-Object { $_.LastWriteTimeUtc -ge $startedAt -and $_.Length -gt 1024 })
$requiredImages = switch ($Mode) { 'Interface' { 16 } 'Motion' { 14 } 'Wreckage' { 15 } 'AudioDiagnostics' { 0 } default { 6 } }
if ($screenshots.Count -lt $requiredImages) { throw "The player produced $($screenshots.Count) fresh screenshots; expected at least $requiredImages. Inspect $log" }
if ($Mode -eq 'InterfaceTest') {
    $report = Join-Path $CaptureDirectory 'interface-verification.txt'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt -or
        (Get-Content -LiteralPath $report -Raw) -notmatch '(?m)^Passed: True\s*$') {
        throw "Interface interaction checks failed. Inspect $report"
    }
}
if ($Mode -eq 'RenderDiagnostics') {
    $report = Join-Path $CaptureDirectory 'render-diagnostics.txt'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt -or
        (Get-Content -LiteralPath $report -Raw) -notmatch '(?m)^AA coverage verified: True\s*$' -or
        (Get-Content -LiteralPath $report -Raw) -notmatch 'retained=True') {
        throw "Rendered anti-aliasing or fog checks failed. Inspect $report"
    }
}
if ($Mode -eq 'AudioDiagnostics') {
    $report = Join-Path $CaptureDirectory 'audio-verification.txt'
    $audio = Join-Path $CaptureDirectory 'capture-calibration.wav'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt -or
        (Get-Content -LiteralPath $report -Raw) -notmatch '(?m)^Passed: True\s*$' -or
        -not (Test-Path -LiteralPath $audio) -or (Get-Item -LiteralPath $audio).LastWriteTimeUtc -lt $startedAt -or
        (Get-Item -LiteralPath $audio).Length -lt 10000) {
        throw "Mixed game audio capture was silent, out of sync or missing. Inspect $report"
    }
}
if ($Mode -eq 'Motion') {
    $report = Join-Path $CaptureDirectory 'motion-verification.txt'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt) {
        throw "Motion verification report is missing or stale. Inspect $report"
    }
    $motionText = Get-Content -LiteralPath $report -Raw
    if ($motionText -match '(?m)^FAIL\b' -or [regex]::Matches($motionText, '(?m)^PASS\b').Count -lt 36) {
        throw "Motion contact, scale, timing or recoil checks failed. Inspect $report"
    }
}
if ($Mode -eq 'Wreckage') {
    $report = Join-Path $CaptureDirectory 'wreckage-review.txt'
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedAt) {
        throw "Wreckage verification report is missing or stale. Inspect $report"
    }
    $wreckageText = Get-Content -LiteralPath $report -Raw
    if ($wreckageText -notmatch '(?m)^Errors: 0\s*$' -or
        [regex]::Matches($wreckageText, '(?m)^t[1-5]_[a-z]+: emitted=[1-9][0-9]*, remaining fragments=0\s*$').Count -ne 5) {
        throw "Chassis defeat effects did not emit and clean up all five enemy types. Inspect $report"
    }
}
if ($Video) {
    $videoName = if ($Mode -eq 'Motion') { 'Model-motion-review.mp4' } else { 'Chassis-response.mp4' }
    $videoPath = Join-Path $CaptureDirectory $videoName
    if (-not (Test-Path -LiteralPath $videoPath) -or (Get-Item -LiteralPath $videoPath).LastWriteTimeUtc -lt $startedAt -or
        (Get-Item -LiteralPath $videoPath).Length -lt 10000) { throw "Review video is missing, stale or incomplete: $videoPath" }
    $probe = & ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,width,height,nb_frames -of json $videoPath
    if ($LASTEXITCODE -ne 0) { throw "Review video could not be read: $videoPath" }
    $streams = @(($probe | ConvertFrom-Json).streams)
    if ($streams.Count -ne 1 -or $streams[0].width -ne $Width -or $streams[0].height -ne $Height -or
        [int]$streams[0].nb_frames -lt 60) { throw "Review video does not contain the expected rendered frames: $videoPath" }
}
Assert-PlayerFileManifest -Expected $playerFiles -Actual @(Get-PlayerFileManifest -Root (Split-Path $PlayerPath -Parent))
[ordered]@{
    passed = $true
    mode = $Mode
    video = [bool]$Video
    executable = $PlayerPath
    verifiedUtc = [DateTime]::UtcNow.ToString('O')
    screenshots = $screenshots.Count
    files = $playerFiles
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $verificationManifest -Encoding UTF8
Write-Output "$Mode review produced $($screenshots.Count) screenshots in $CaptureDirectory"
