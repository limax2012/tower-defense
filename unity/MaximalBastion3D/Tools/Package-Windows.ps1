[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VerificationDirectory,
    [string]$ReleasePath,
    [string]$OutputDirectory,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]+$')]
    [string]$Name = 'MaximalBastion3D-Cinderworks-Windows-x64'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Player-Manifest.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
if (-not $ReleasePath) { $ReleasePath = Join-Path $repositoryRoot '.artifacts/3d-development/Windows-Release' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot '.artifacts/3d-development/Packages' }
$ReleasePath = [IO.Path]::GetFullPath($ReleasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory.TrimEnd('\').Equals($ReleasePath, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputDirectory.StartsWith($ReleasePath + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Place the package output outside the player build directory.'
}
$executable = Join-Path $ReleasePath 'MaximalBastion3D.exe'
$buildInfoPath = Join-Path $ReleasePath 'build-info.json'
if (-not (Test-Path -LiteralPath $executable) -or -not (Test-Path -LiteralPath $buildInfoPath)) {
    throw 'BuildWindowsRelease must complete before packaging.'
}
$build = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
if ($build.configuration -ne 'Release') { throw 'Only the non-development Windows release build can be packaged.' }
$reviewPath = Join-Path ([IO.Path]::GetFullPath($VerificationDirectory)) 'verified-player.json'
if (-not (Test-Path -LiteralPath $reviewPath)) { throw 'Run Review-Player.ps1 -Mode InterfaceTest against this release before packaging.' }
$review = Get-Content -LiteralPath $reviewPath -Raw | ConvertFrom-Json
if (-not $review.passed -or $review.mode -ne 'InterfaceTest' -or
    -not [string]::Equals($review.executable, $executable, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The verification manifest must identify a successful interface test of this release executable.'
}
$reportPath = Join-Path ([IO.Path]::GetFullPath($VerificationDirectory)) 'interface-verification.txt'
if (-not (Test-Path -LiteralPath $reportPath) -or (Get-Content -LiteralPath $reportPath -Raw) -notmatch '(?m)^Passed: True\s*$') {
    throw 'The release interface verification report is missing or failed.'
}
$currentFiles = @(Get-PlayerFileManifest -Root $ReleasePath)
Assert-PlayerFileManifest -Expected $review.files -Actual $currentFiles
$runtimeFiles = @($currentFiles | Where-Object {
    $_.path -notmatch '(?i)(^|/)[^/]*(?:DoNotShip|BackUpThisFolder)[^/]*(/|$)' -and
    $_.path -notmatch '(?i)(^|/)(?:Logs|Verification)(/|$)' -and
    $_.path -notmatch '(?i)\.(?:log|pdb|mdb)$'
})
foreach ($required in @('MaximalBastion3D.exe', 'UnityPlayer.dll', 'MaximalBastion3D_Data/globalgamemanagers',
    'MaximalBastion3D_Data/Managed/Assembly-CSharp.dll', 'MaximalBastion3D_Data/Managed/MaximalBastion.Gameplay.dll')) {
    if ($runtimeFiles.path -notcontains $required) { throw "Release runtime file is missing: $required" }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$packageWork = Join-Path $OutputDirectory ('.work-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $packageWork | Out-Null
$controlsPath = Join-Path $packageWork 'README.txt'
@'
MAXIMAL BASTION 3D — CINDERWORKS
Windows x64 / Unity URP

Extract the complete folder, then run MaximalBastion3D.exe. Keep the executable,
UnityPlayer.dll, MonoBleedingEdge and MaximalBastion3D_Data together.

PLAY
Choose Play, a difficulty and a mode. Select a tower from the bottom toolbar,
then click a clear build deck. Click a built tower for upgrades, targeting,
protocols and selling. The Cinderworks map uses the authored 30-wave campaign,
with Endless after victory; Sandbox provides wave and enemy controls.

Left click             Place or select
Shift + left click     Repeat placement
Right click / Esc      Cancel placement
Space                  Start next wave
Esc                    Pause / return

CAMERA
Right mouse drag       Orbit; look around during free flight
Middle mouse drag      Pan
Mouse wheel            Zoom; move forward/back during free flight
W/A/S/D                Move
Q/E                    Lower / raise
F                      Toggle free flight
Shift                  Move faster
Home                   Return to overview

Pulse Plates are placed on the road; the Charge Forge occupies a build deck.
Save between waves from the pause menu. Completed waves also write an autosave.
Save data: %USERPROFILE%\AppData\LocalLow\Bastion\Maximal Bastion 3D

Settings contains volume, camera sensitivity, VSync, fullscreen and auto-wave.
Input is inactive while the game window is unfocused.

This package contains the Cinderworks 3D client. Other maps and online co-op are
available in the separate original clients, not in this 3D build.
Font licenses are included in LICENSES. Build details are in build-info.json.
'@ | Set-Content -LiteralPath $controlsPath -Encoding UTF8

$entries = [Collections.Generic.List[object]]::new()
foreach ($file in $runtimeFiles) {
    $entries.Add([pscustomobject]@{ path = $file.path; source = Join-Path $ReleasePath $file.path; bytes = $file.bytes; sha256 = $file.sha256 })
}
$extraFiles = @([pscustomobject]@{ path = 'README.txt'; source = $controlsPath })
foreach ($license in @('Barlow-OFL.txt', 'Oxanium-OFL.txt')) {
    $extraFiles += [pscustomobject]@{ path = 'LICENSES/' + $license; source = Join-Path $projectRoot ('Assets/Resources/Fonts/' + $license) }
}
foreach ($file in $extraFiles) {
    $source = Get-Item -LiteralPath $file.source
    $entries.Add([pscustomobject]@{ path = $file.path; source = $source.FullName; bytes = $source.Length;
        sha256 = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash.ToLowerInvariant() })
}
$manifestPath = Join-Path $packageWork 'package-manifest.json'
[ordered]@{
    name = $Name
    configuration = $build.configuration
    unityVersion = $build.unityVersion
    verifiedUtc = $review.verifiedUtc
    files = @($entries | Select-Object path, bytes, sha256)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$entries.Add([pscustomobject]@{ path = 'package-manifest.json'; source = $manifestPath; bytes = (Get-Item -LiteralPath $manifestPath).Length;
    sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() })

$temporaryZip = Join-Path $packageWork ($Name + '.zip')
$archive = [IO.Compression.ZipFile]::Open($temporaryZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $entries) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.source, $Name + '/' + $file.path,
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
try {
    if ($archive.Entries.Count -ne $entries.Count) { throw 'Package entry count does not match its manifest.' }
    foreach ($file in $entries) {
        $entry = $archive.GetEntry($Name + '/' + $file.path)
        if (-not $entry -or $entry.Length -ne $file.bytes) { throw "Package entry is missing or truncated: $($file.path)" }
        $stream = $entry.Open()
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose(); $algorithm.Dispose() }
        if ($actualHash -ne $file.sha256) { throw "Package entry checksum mismatch: $($file.path)" }
    }
} finally { $archive.Dispose() }

$zipPath = Join-Path $OutputDirectory ($Name + '.zip')
Move-Item -LiteralPath $temporaryZip -Destination $zipPath -Force
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
($zipHash + '  ' + [IO.Path]::GetFileName($zipPath)) | Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ASCII
Copy-Item -LiteralPath $controlsPath -Destination (Join-Path $OutputDirectory ($Name + '-README.txt')) -Force
Write-Output "Packaged $($entries.Count) verified files: $zipPath"
Write-Output "SHA256: $zipHash"
