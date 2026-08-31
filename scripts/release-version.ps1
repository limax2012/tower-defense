function Get-MinimalBastionVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository
    )

    $versionPath = Join-Path $Repository "src\Directory.Build.props"
    [xml]$properties = Get-Content -LiteralPath $versionPath -Raw
    $version = [string]$properties.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
        throw "Invalid release version in ${versionPath}: $version"
    }
    return $version
}
