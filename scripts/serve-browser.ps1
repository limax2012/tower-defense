param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5080,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$siteRoot = Join-Path $repository ".build\releases\browser"
$projectPath = Join-Path $repository "src\MinimalBastion.Web\MinimalBastion.Web.csproj"
$localDotnet = Join-Path $repository ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }
$address = "http://127.0.0.1:$Port/"

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

& (Join-Path $PSScriptRoot "publish-browser.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { throw "Browser publish failed with exit code $LASTEXITCODE." }

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if ($null -eq $python) {
    throw "Python 3 is required to serve the static browser release locally."
}

Write-Host "Serving the optimized browser release at $address"
Write-Host "Press Ctrl+C to stop."
& $python.Source -m http.server $Port --bind 127.0.0.1 --directory $siteRoot
