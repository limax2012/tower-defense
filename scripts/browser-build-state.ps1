function Get-BrowserBuildFingerprint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository
    )

    $files = @()
    foreach ($sourceRoot in @(
        (Join-Path $Repository "src\MinimalBastion"),
        (Join-Path $Repository "src\MinimalBastion.Web")
    )) {
        $files += Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }

    foreach ($buildInput in @(
        (Join-Path $Repository "Directory.Build.props"),
        (Join-Path $Repository "Directory.Build.targets"),
        (Join-Path $Repository "Directory.Packages.props"),
        (Join-Path $Repository "global.json"),
        (Join-Path $Repository "NuGet.config"),
        (Join-Path $Repository "scripts\browser-build-state.ps1"),
        (Join-Path $Repository "scripts\publish-browser.ps1")
    )) {
        if (Test-Path -LiteralPath $buildInput -PathType Leaf) {
            $files += Get-Item -LiteralPath $buildInput
        }
    }

    $repositoryPath = [System.IO.Path]::GetFullPath($Repository).TrimEnd('\', '/')
    $records = foreach ($file in $files | Sort-Object FullName -Unique) {
        $relativePath = $file.FullName.Substring($repositoryPath.Length + 1).Replace('\', '/')
        $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "$relativePath|$fileHash"
    }

    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $records))
        return [System.BitConverter]::ToString($hasher.ComputeHash($bytes)).Replace("-", "")
    }
    finally {
        $hasher.Dispose()
    }
}

function Test-BrowserReleaseCurrent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$SiteRoot,
        [Parameter(Mandatory = $true)]
        [string]$StatePath,
        [string]$Configuration = "Release"
    )

    if (-not (Test-Path -LiteralPath (Join-Path $SiteRoot "index.html") -PathType Leaf) -or
        -not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        return $false
    }

    try {
        $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        return $state.Configuration -eq $Configuration -and
            $state.Fingerprint -eq (Get-BrowserBuildFingerprint -Repository $Repository)
    }
    catch {
        return $false
    }
}
