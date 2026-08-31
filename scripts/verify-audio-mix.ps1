param(
    [string]$Ffmpeg
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$audioRoot = Join-Path $repository "src\MinimalBastion\Content\Audio"

if ([string]::IsNullOrWhiteSpace($Ffmpeg)) {
    $command = Get-Command ffmpeg -ErrorAction Stop
    $Ffmpeg = $command.Source
}

$targets = @(
    [pscustomobject]@{
        Path = Join-Path $audioRoot "MainMenuLoop.wav"
        TargetLufs = -12.0
        LufsTolerance = 0.35
        MaximumTruePeak = -0.8
        MaximumLra = 7.0
    }
)
$targets += Get-ChildItem -LiteralPath (Join-Path $audioRoot "Music") -Filter *.ogg -File |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName
            TargetLufs = -16.0
            LufsTolerance = 0.35
            MaximumTruePeak = -1.5
            MaximumLra = 7.0
        }
    }

$failures = [System.Collections.Generic.List[string]]::new()
$measurements = foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target.Path -PathType Leaf)) {
        $failures.Add("Missing audio asset: $($target.Path)")
        continue
    }

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & $Ffmpeg -hide_banner -nostats -i $target.Path `
        -af "loudnorm=I=$($target.TargetLufs):TP=$($target.MaximumTruePeak):LRA=$($target.MaximumLra):print_format=json" `
        -f null - 2>&1 | Out-String
    $ffmpegExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorAction
    if ($ffmpegExitCode -ne 0) {
        $failures.Add("FFmpeg could not analyze $($target.Path).")
        continue
    }

    $match = [regex]::Match($output, '\{\s*"input_i"[\s\S]*?\}')
    if (-not $match.Success) {
        $failures.Add("FFmpeg did not report loudness for $($target.Path).")
        continue
    }

    $stats = $match.Value | ConvertFrom-Json
    $lufs = [double]$stats.input_i
    $truePeak = [double]$stats.input_tp
    $lra = [double]$stats.input_lra
    $name = Split-Path -Leaf $target.Path

    if ([Math]::Abs($lufs - $target.TargetLufs) -gt $target.LufsTolerance) {
        $failures.Add("$name measures $lufs LUFS; expected $($target.TargetLufs) +/- $($target.LufsTolerance).")
    }
    if ($truePeak -gt $target.MaximumTruePeak) {
        $failures.Add("$name reaches $truePeak dBTP; maximum is $($target.MaximumTruePeak) dBTP.")
    }
    if ($lra -gt $target.MaximumLra) {
        $failures.Add("$name has $lra LU LRA; maximum is $($target.MaximumLra) LU.")
    }

    [pscustomobject]@{
        File = $name
        LUFS = $lufs
        TruePeakDb = $truePeak
        LRA = $lra
    }
}

$measurements | Format-Table -AutoSize
if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { [Console]::Error.WriteLine($failure) }
    exit 1
}

Write-Host "Verified $($measurements.Count) mastered music assets."
