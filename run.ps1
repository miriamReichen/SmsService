# One command to run the project locally (Windows):
#   .\run.cmd           Everything: fake provider, API, React client. Opens the UI and Swagger.
#   .\run.cmd server    Server only: fake provider and API. Opens Swagger.
# (or: powershell -ExecutionPolicy Bypass -File .\run.ps1 [all|server])
# Requires: SQL Server default instance on localhost (Windows Authentication).
# Ports: provider 5100, API 5000 (Swagger at /swagger), client 5173.
# Press Ctrl+C to stop all processes.

param(
    [ValidateSet('all', 'server')]
    [string]$Mode = 'all'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue' # hide Invoke-WebRequest progress noise
$root = $PSScriptRoot
$server = Join-Path $root 'server'
$client = Join-Path $root 'client'
$withClient = $Mode -eq 'all'

$ports = [ordered]@{ Provider = 5100; Api = 5000 }
if ($withClient) { $ports.Client = 5173 }

$apiUrl = "http://localhost:$($ports.Api)"
$swaggerUrl = "$apiUrl/swagger"
$clientUrl = 'http://localhost:5173'

function Assert-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "'$name' was not found. $hint" }
}

function Wait-Http($url, $name, $timeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3 | Out-Null
            Write-Host "  $name is up: $url" -ForegroundColor Green
            return
        } catch { Start-Sleep -Seconds 1 }
    }
    throw "$name did not start within $timeoutSeconds seconds ($url)."
}

Write-Host "== Mode: $Mode" -ForegroundColor Cyan
Write-Host '== Checking prerequisites' -ForegroundColor Cyan
Assert-Command dotnet 'Install the .NET 9 SDK.'
if ($withClient) { Assert-Command npm 'Install Node.js (18+).' }

foreach ($entry in $ports.GetEnumerator()) {
    if (Get-NetTCPConnection -LocalPort $entry.Value -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $($entry.Value) ($($entry.Key)) is already in use. Stop the process using it and try again."
    }
}

Write-Host '== Checking SQL Server (localhost, default instance MSSQLSERVER)' -ForegroundColor Cyan
$sql = Get-Service -Name 'MSSQLSERVER' -ErrorAction SilentlyContinue
if (-not $sql) {
    throw "SQL Server default instance (MSSQLSERVER) was not found. Install SQL Server, or change ConnectionStrings:Default in server\src\SmsService.API\appsettings.json."
}
if ($sql.Status -ne 'Running') {
    throw "SQL Server service MSSQLSERVER is $($sql.Status). Start it (services.msc, or 'Start-Service MSSQLSERVER' as administrator) and run again."
}
Write-Host '  SQL Server is running. The API creates/migrates the SmsService database on startup.' -ForegroundColor Green

Write-Host '== Building server' -ForegroundColor Cyan
dotnet build (Join-Path $server 'SmsService.sln') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }

if ($withClient -and -not (Test-Path (Join-Path $client 'node_modules'))) {
    Write-Host '== Installing client packages (first run only)' -ForegroundColor Cyan
    Push-Location $client
    try { npm install --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' } }
    finally { Pop-Location }
}

$env:ASPNETCORE_ENVIRONMENT = 'Development' # Swagger is enabled only in Development
$processes = @()
try {
    Write-Host '== Starting fake SMS provider' -ForegroundColor Cyan
    $processes += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList @(
        'run', '--no-build', '--no-launch-profile',
        '--project', (Join-Path $server 'src\SmsService.FakeProvider'),
        '--urls', "http://localhost:$($ports.Provider)")
    Wait-Http "http://localhost:$($ports.Provider)/" 'Fake provider'

    Write-Host '== Starting API (applies migrations and seed on startup)' -ForegroundColor Cyan
    $processes += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList @(
        'run', '--no-build', '--no-launch-profile',
        '--project', (Join-Path $server 'src\SmsService.API'),
        '--urls', $apiUrl)
    Wait-Http "$apiUrl/employees" 'API'

    if ($withClient) {
        Write-Host '== Starting React client' -ForegroundColor Cyan
        $processes += Start-Process npm.cmd -PassThru -NoNewWindow -WorkingDirectory $client -ArgumentList @('run', 'dev')
        Wait-Http "$clientUrl/" 'Client'
    }

    Write-Host ''
    Write-Host 'Everything is running:' -ForegroundColor Green
    if ($withClient) { Write-Host "  UI:      $clientUrl" -ForegroundColor Green }
    Write-Host "  Swagger: $swaggerUrl" -ForegroundColor Green
    Write-Host "  API:     $apiUrl" -ForegroundColor Green
    Write-Host 'Press Ctrl+C to stop.' -ForegroundColor Yellow

    Start-Process $swaggerUrl
    if ($withClient) { Start-Process $clientUrl }

    while ($true) {
        foreach ($p in $processes) {
            if ($p.HasExited) { throw "A process exited unexpectedly (pid $($p.Id), exit code $($p.ExitCode))." }
        }
        Start-Sleep -Seconds 2
    }
}
finally {
    Write-Host '== Stopping processes' -ForegroundColor Cyan
    foreach ($p in $processes) {
        # /T kills the whole tree (npm.cmd -> node/vite, dotnet run -> app).
        if ($p -and -not $p.HasExited) { taskkill /PID $p.Id /T /F 2>&1 | Out-Null }
    }
}
