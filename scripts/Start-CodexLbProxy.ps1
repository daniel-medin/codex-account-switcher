$ErrorActionPreference = 'Stop'

$uvx = Get-Command uvx -ErrorAction Stop
$experimentRoot = Join-Path $env:LOCALAPPDATA 'CodexLbSwitcherExperiment'
$proxyData = Join-Path $experimentRoot 'proxy-data'
New-Item -ItemType Directory -Path $proxyData -Force | Out-Null

$env:CODEX_LB_DATA_DIR = $proxyData
Write-Host 'Starting codex-lb for the isolated switch experiment.'
Write-Host 'Dashboard: http://127.0.0.1:2455'
Write-Host 'Leave this terminal open while testing; press Ctrl+C to stop the proxy.'
& $uvx.Source codex-lb
exit $LASTEXITCODE
