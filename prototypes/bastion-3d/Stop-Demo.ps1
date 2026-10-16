$ErrorActionPreference = 'Stop'
$pidFile = Join-Path $PSScriptRoot '.server.pid'
if (Test-Path -LiteralPath $pidFile) {
    $serverId = [int](Get-Content -LiteralPath $pidFile)
    $serverProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $serverId" -ErrorAction SilentlyContinue
    if ($serverProcess -and $serverProcess.CommandLine -match 'tools/serve.py' -and $serverProcess.CommandLine -match '5183') {
        Stop-Process -Id $serverId
    }
    Remove-Item -LiteralPath $pidFile
}
