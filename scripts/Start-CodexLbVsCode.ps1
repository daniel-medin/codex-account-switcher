param(
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'

$experimentRoot = Join-Path $env:LOCALAPPDATA 'CodexLbSwitcherExperiment'
$codexHome = Join-Path $experimentRoot 'codex-home'
$userData = Join-Path $experimentRoot 'vscode-user-data'
$extensions = Join-Path $experimentRoot 'vscode-extensions'
$project = Join-Path $experimentRoot 'test-project'

foreach ($path in @($codexHome, $userData, $extensions, $project)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$config = Join-Path $codexHome 'config.toml'
if (-not (Test-Path -LiteralPath $config)) {
    $configText = @'
model = "gpt-5.6-sol"
model_provider = "codex-lb"

[model_providers.codex-lb]
name = "openai"
base_url = "http://127.0.0.1:2455/backend-api/codex"
wire_api = "responses"
supports_websockets = true
requires_openai_auth = true
'@
    [System.IO.File]::WriteAllText(
        $config,
        $configText + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

$marker = Join-Path $project 'context-marker.txt'
if (-not (Test-Path -LiteralPath $marker)) {
    'The experiment marker is violet compass.' | Set-Content -LiteralPath $marker -Encoding utf8
}

$env:CODEX_HOME = $codexHome

if ($PrepareOnly) {
    Write-Host "Prepared isolated Codex test home: $codexHome"
    return
}

$code = Get-Command code -ErrorAction Stop
& $code.Source --install-extension openai.chatgpt --user-data-dir $userData --extensions-dir $extensions
if ($LASTEXITCODE -ne 0) {
    throw 'VS Code could not install the Codex extension in the isolated profile.'
}

Write-Host 'Opening the isolated VS Code profile and disposable project.'
Write-Host "Test Codex home: $codexHome"
& $code.Source --new-window --disable-workspace-trust --user-data-dir $userData --extensions-dir $extensions $project
if ($LASTEXITCODE -ne 0) {
    throw 'VS Code could not open the isolated test window.'
}
