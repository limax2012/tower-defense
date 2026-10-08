[CmdletBinding()]
param(
    [string]$InstallRoot,
    [switch]$WindowsIl2Cpp,
    [switch]$InstallHub
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../..'))
$versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
$version = ($versionLine -split ': ', 2)[1].Trim()
$downloadRoot = Join-Path $repositoryRoot '.artifacts/3d-development'
if (-not $InstallRoot) { $InstallRoot = Join-Path $env:SystemDrive "UnityEditors/$version" }
$InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
if ($InstallRoot.Length -gt 45) { throw 'Choose an install root shorter than 46 characters; Unity installers can omit package files beyond the Windows path limit.' }
New-Item -ItemType Directory -Force $downloadRoot | Out-Null

function Install-SignedDownload {
    param([string]$Url, [string]$FileName, [string]$Integrity, [string[]]$InstallerArguments)
    $destination = Join-Path $downloadRoot $FileName
    if (-not (Test-Path -LiteralPath $destination)) {
        & curl.exe -L --fail --retry 3 --silent --show-error --output $destination $Url
        if ($LASTEXITCODE -ne 0) { throw "Download failed: $Url" }
    }
    $algorithm, $expected = $Integrity -split '-', 2
    $hash = Get-FileHash -LiteralPath $destination -Algorithm $algorithm.ToUpperInvariant()
    $hashBytes = [byte[]]::new($hash.Hash.Length / 2)
    for ($i = 0; $i -lt $hashBytes.Length; $i++) {
        $hashBytes[$i] = [Convert]::ToByte($hash.Hash.Substring($i * 2, 2), 16)
    }
    if ([Convert]::ToBase64String($hashBytes) -ne $expected) {
        throw "Checksum failed for $destination. Remove that installer and retry."
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $destination
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity') {
        throw "The downloaded installer does not have a valid Unity signature: $destination"
    }
    $process = Start-Process -FilePath $destination -ArgumentList $InstallerArguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed with exit code $($process.ExitCode): $destination" }
}

$release = Invoke-RestMethod "https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=$version&architecture=X86_64&platform=WINDOWS"
$release = $release.results | Where-Object version -EQ $version | Select-Object -First 1
if (-not $release) { throw "The official Unity registry did not return $version." }
$editor = $release.downloads | Where-Object { $_.platform -eq 'WINDOWS' -and $_.architecture -eq 'X86_64' } | Select-Object -First 1
if (-not (Test-Path -LiteralPath (Join-Path $InstallRoot 'Editor/Unity.exe'))) {
    Install-SignedDownload -Url $editor.url -FileName "UnitySetup64-$version.exe" -Integrity $editor.integrity -InstallerArguments @('/S', "/D=$InstallRoot")
}
if ($WindowsIl2Cpp -and -not (Test-Path -LiteralPath (Join-Path $InstallRoot 'Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_il2cpp')) ) {
    $module = $editor.modules | Where-Object id -EQ 'windows-il2cpp'
    Install-SignedDownload -Url $module.url -FileName "UnitySetup-Windows-IL2CPP-$version.exe" -Integrity $module.integrity -InstallerArguments @('/S', "/D=$InstallRoot")
}
if ($InstallHub -and -not (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'Unity Hub/Unity Hub.exe'))) {
    $hubResponse = Invoke-WebRequest 'https://public-cdn.cloud.unity3d.com/hub/prod/latest.yml'
    $hubMetadata = if ($hubResponse.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($hubResponse.Content) } else { [string]$hubResponse.Content }
    $match = [regex]::Match($hubMetadata, '(?m)^\s*- url: (.*-x64\.exe)\r?\n\s*sha512: ([A-Za-z0-9+/=]+)')
    if (-not $match.Success) { throw 'The official Hub metadata did not contain a Windows x64 installer.' }
    $hubFile = $match.Groups[1].Value.Trim()
    Install-SignedDownload -Url "https://public-cdn.cloud.unity3d.com/hub/prod/$hubFile" -FileName ([IO.Path]::GetFileName($hubFile)) -Integrity "sha512-$($match.Groups[2].Value)" -InstallerArguments @('/S')
}
Write-Output "Unity editor: $(Join-Path $InstallRoot 'Editor/Unity.exe')"
Write-Output 'Sign in to Unity Hub to activate an eligible Unity license, then run Tools/Invoke-Unity.ps1 -Task Import.'
