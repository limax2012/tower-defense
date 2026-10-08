function Get-PlayerFileManifest {
    param([Parameter(Mandatory)][string]$Root)
    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $prefix = $resolvedRoot + [IO.Path]::DirectorySeparatorChar
    foreach ($file in Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse | Sort-Object FullName) {
        if (-not $file.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Player file is outside its build directory: $($file.FullName)"
        }
        if ($file.Extension -eq '.log') { continue }
        [pscustomobject]@{
            path = $file.FullName.Substring($prefix.Length).Replace('\', '/')
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
}

function Assert-PlayerFileManifest {
    param([Parameter(Mandatory)]$Expected, [Parameter(Mandatory)]$Actual)
    $expectedFiles = @($Expected)
    $actualFiles = @($Actual)
    if ($expectedFiles.Count -ne $actualFiles.Count) { throw 'The player file set changed after verification.' }
    $lookup = @{}
    foreach ($file in $actualFiles) { $lookup[$file.path] = $file }
    foreach ($file in $expectedFiles) {
        $current = $lookup[$file.path]
        if (-not $current -or $current.bytes -ne $file.bytes -or $current.sha256 -ne $file.sha256) {
            throw "The player changed after verification: $($file.path)"
        }
    }
}
