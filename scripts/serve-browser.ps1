param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5080,
    [switch]$Publish,
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$siteRoot = Join-Path $repository ".build\releases\browser"
$statePath = Join-Path $repository ".build\releases\.browser-build-state.json"
$projectPath = Join-Path $repository "src\MinimalBastion.Web\MinimalBastion.Web.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }
$address = "http://127.0.0.1:$Port/"
. (Join-Path $PSScriptRoot "browser-build-state.ps1")

if ($Rebuild -and -not $Publish) {
    throw "-Rebuild requires -Publish."
}

if (-not $Publish) {
    $dotnetDirectory = Split-Path -Parent $dotnet
    $env:Path = "$dotnetDirectory;$env:Path"
    $previousUrls = $env:ASPNETCORE_URLS
    try {
        $env:ASPNETCORE_URLS = $address.TrimEnd("/")
        Write-Host "Serving the browser development build at $address"
        Write-Host "Use -Publish only when rebuilding the optimized release package."
        Write-Host "Press Ctrl+C to stop."
        & $dotnet run --project $projectPath -c Debug --no-launch-profile
        if ($LASTEXITCODE -ne 0) { throw "Browser development server failed with exit code $LASTEXITCODE." }
    }
    finally {
        $env:ASPNETCORE_URLS = $previousUrls
    }
    exit 0
}

$releaseCurrent = Test-BrowserReleaseCurrent `
    -Repository $repository `
    -SiteRoot $siteRoot `
    -StatePath $statePath `
    -Configuration Release
if ($Rebuild -or -not $releaseCurrent) {
    Write-Host "Building the optimized browser release. WebAssembly AOT compilation can take several minutes."
    & (Join-Path $PSScriptRoot "publish-browser.ps1") -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Browser publish failed with exit code $LASTEXITCODE." }
}
else {
    Write-Host "The optimized browser release is current; reusing the existing build."
}

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if ($null -eq $python) {
    throw "Python 3 is required to serve the static browser release locally."
}

Write-Host "Serving the optimized browser release at $address"
Write-Host "Press Ctrl+C to stop."
& $python.Source -m http.server $Port --bind 127.0.0.1 --directory $siteRoot
