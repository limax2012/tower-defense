param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5080,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$siteRoot = Join-Path $repository ".build\releases\browser"

if ($Publish -or -not (Test-Path -LiteralPath (Join-Path $siteRoot "index.html"))) {
    & (Join-Path $PSScriptRoot "publish-browser.ps1") -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Browser publish failed with exit code $LASTEXITCODE." }
}

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if ($null -eq $python) {
    throw "Python 3 is required to serve the static browser release locally."
}

$address = "http://127.0.0.1:$Port/"
Write-Host "Serving the optimized browser release at $address"
Write-Host "Press Ctrl+C to stop."
& $python.Source -m http.server $Port --bind 127.0.0.1 --directory $siteRoot
