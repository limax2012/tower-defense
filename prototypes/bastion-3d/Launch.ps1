$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$python = (Get-Command python -ErrorAction Stop).Source
$url = 'http://127.0.0.1:5183'
$ready = $false
try { $ready = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2).Content.Contains('Cinderworks 3D') } catch {}
if (-not $ready) {
    $server = Start-Process -FilePath $python -ArgumentList @('tools/serve.py', '--port', '5183') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath (Join-Path $PSScriptRoot '.server.pid') -Value $server.Id
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { $ready = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 1).Content.Contains('Cinderworks 3D') } catch {}
        if ($ready) { break }
        Start-Sleep -Milliseconds 150
    }
}
if (-not $ready) { throw 'The local scene server could not start on port 5183.' }
Start-Process $url
