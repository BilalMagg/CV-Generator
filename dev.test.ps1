#!/usr/bin/env pwsh
# dev.test.ps1 - Local TEST launcher for CV-Generator
#
# Reuses the same infra containers as dev.ps1 (Postgres x7, Keycloak, Kafka,
# MinIO) but points every .NET service at sibling test databases
# (*_test_db) and the cv-realm-test Keycloak realm. The test data is wiped
# on every startup, so tests always start from a clean slate.
#
# This launcher CANNOT run at the same time as dev.ps1 -they share the same
# host ports (8080 for gateway, 5001-5007 for services). Stop dev before
# starting test mode and vice versa.
#
# USAGE:
#   .\dev.test.ps1                          -- start test stack
#   .\dev.test.ps1 -Stop                    -- kill processes + remove sentinel
#   .\dev.test.ps1 -InfraOnly               -- Docker infra + reset only, no dotnet/ng
#   .\dev.test.ps1 -NoAgents                -- skip AI agents Docker compose
#   .\dev.test.ps1 -NoFrontend              -- skip Angular ng serve
#   .\dev.test.ps1 -NoReset                 -- skip the data wipe (e.g., debugging)
#   .\dev.test.ps1 -Service user-service    -- start only one .NET service

param(
    [switch]$Stop,
    [switch]$InfraOnly,
    [switch]$NoAgents,
    [switch]$NoFrontend,
    [switch]$NoReset,
    [string]$Service = ""
)

$ROOT          = $PSScriptRoot
$PID_FILE      = "$ROOT\.dev.test.pids"
$SENTINEL_FILE = "$ROOT\.test-mode"
$SHELL_EXE     = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }

# =============================================================================
# FUNCTION DEFINITIONS
# =============================================================================

function Write-Step([string]$msg) { Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  !! $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  XX $msg" -ForegroundColor Red }

function Import-DotEnv([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    foreach ($line in (Get-Content $Path)) {
        $line = $line.Trim()
        if ($line -eq '' -or $line.StartsWith('#')) { continue }
        if ($line -match '^([^=]+?)\s*=\s*(.*)$') {
            $k = $Matches[1].Trim()
            $v = $Matches[2].Trim()
            $v = $v -replace '^"(.*)"$', '$1'
            $v = $v -replace "^'(.*)'$", '$1'
            [System.Environment]::SetEnvironmentVariable($k, $v, "Process")
        }
    }
}

function Wait-Healthy([string]$Container, [int]$MaxSecs = 120) {
    Write-Step "Waiting for $Container..."
    $deadline = (Get-Date).AddSeconds($MaxSecs)
    while ((Get-Date) -lt $deadline) {
        $status = docker inspect --format "{{.State.Health.Status}}" $Container 2>$null
        if ($status -eq "healthy") {
            Write-Ok "$Container ready"
            return
        }
        Start-Sleep -Seconds 4
    }
    Write-Warn "$Container did not become healthy within ${MaxSecs}s -- continuing anyway"
}

function New-TempScript([string]$Name, [string[]]$Lines) {
    $path = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "cv-test-$Name.ps1")
    $Lines | Set-Content $path -Encoding UTF8
    return $path
}

function Start-InNewWindow([string]$ScriptPath) {
    $proc = Start-Process $SHELL_EXE -ArgumentList "-NoExit", "-File", $ScriptPath -PassThru
    Add-Content $PID_FILE $proc.Id
    return $proc
}

function Start-DotnetService([string]$Name, [hashtable]$Config) {
    $dir    = $Config.Dir
    $envMap = $Config.Env
    $port   = ""
    if ($envMap.ContainsKey("PORT")) { $port = ":$($envMap["PORT"])" }

    $lines = @("Set-Location '$($dir -replace "'", "''")'" )
    foreach ($kv in ($envMap.GetEnumerator() | Sort-Object Key)) {
        $v = $kv.Value -replace "'", "''"
        $lines += "`$env:$($kv.Key) = '$v'"
    }
    $lines += "Write-Host '[TEST $Name] dotnet run --no-build$port' -ForegroundColor Yellow"
    $lines += "dotnet run --no-build"

    $script = New-TempScript $Name $lines
    $proc   = Start-InNewWindow $script
    Write-Ok "$Name  PID $($proc.Id)$port"
}

function Start-Frontend {
    Write-Host ""
    Write-Host "=== Starting Angular Frontend (test mode) ===" -ForegroundColor Magenta
    $lines = @(
        "Set-Location '$($ROOT -replace "'", "''")\\frontend'",
        "Write-Host '[TEST frontend] npm start' -ForegroundColor Yellow",
        "npm start"
    )
    $script = New-TempScript "frontend" $lines
    $proc   = Start-InNewWindow $script
    Write-Ok "Frontend  PID $($proc.Id)  http://localhost:4200"
}

function Start-Infra {
    Write-Host ""
    Write-Host "=== Starting Docker Infrastructure ===" -ForegroundColor Magenta
    $netExists = docker network ls --format "{{.Name}}" | Where-Object { $_ -eq "cv-network" }
    if (-not $netExists) {
        docker network create cv-network | Out-Null
        Write-Ok "Created cv-network"
    }
    docker compose -f "$ROOT\docker-compose.infra.yml" up -d
    Write-Ok "Containers started (or already running)"

    Wait-Healthy "cv-kafka" 120
    foreach ($db in @("cv-user-db","cv-content-db","cv-workflow-db","cv-application-db","cv-job-offer-db","cv-notification-db","cv-cv-db")) {
        Wait-Healthy $db 60
    }
    Wait-Healthy "cv-keycloak" 180

    Write-Step "Waiting 20s for databases to fully warm up..."
    Start-Sleep -Seconds 20
    Write-Ok "Databases ready"
}

function Reset-TestData {
    Write-Host ""
    Write-Host "=== Wiping test data ===" -ForegroundColor Magenta
    $resetScript = "$ROOT\tests\scripts\reset-test-data.ps1"
    if (-not (Test-Path $resetScript)) {
        Write-Fail "reset-test-data.ps1 not found at $resetScript"
        exit 1
    }
    & $resetScript
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Reset failed -aborting"
        exit 1
    }
}

function Start-Agents {
    Write-Host ""
    Write-Host "=== Starting AI Agents (Docker) ===" -ForegroundColor Magenta
    $env:GATEWAY_HOST = "host.docker.internal"
    $env:GATEWAY_PORT = "8080"
    $env:CV_NETWORK   = "cv-network"
    docker compose -f "$ROOT\ai_agents\docker-compose.yml" up -d
    Write-Ok "AI agents started on ports 8001-8006"
}

function Invoke-Stop {
    Write-Host ""
    Write-Host "=== Stopping CV-Generator test stack ===" -ForegroundColor Magenta

    if (Test-Path $PID_FILE) {
        Write-Step "Killing tracked processes..."
        foreach ($line in (Get-Content $PID_FILE)) {
            $pidVal = $line.Trim()
            if ($pidVal) {
                try { Stop-Process -Id ([int]$pidVal) -Force -ErrorAction SilentlyContinue } catch {}
            }
        }
        Remove-Item $PID_FILE -Force
    }

    $devPorts = @(8080, 5001, 5002, 5003, 5004, 5005, 5006, 5007, 18082, 18083, 18084, 18085, 18088)
    foreach ($port in $devPorts) {
        $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        foreach ($conn in $conns) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc -and $proc.Name -notmatch "^(System|svchost|lsass|services)$") {
                Write-Step "Killing $($proc.Name) (PID $($conn.OwningProcess)) on port $port"
                Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Write-Ok "Local processes stopped"

    if (Test-Path $SENTINEL_FILE) {
        Remove-Item $SENTINEL_FILE -Force
        Write-Ok "Removed .test-mode sentinel"
    }

    Write-Warn "Infra containers left running (shared with dev). Run 'docker compose -f docker-compose.infra.yml down' to fully stop."
}

# =============================================================================
# EXECUTION
# =============================================================================

Import-DotEnv "$ROOT\.env"
Import-DotEnv "$ROOT\backend\src\notification-service\.env"

# Short aliases -identical to dev.ps1 except DB names and realm.
$PG_USER = "postgres"
$PG_PASS = "postgres"
$KAFKA   = "127.0.0.1:29092"
$KC_URL  = "http://127.0.0.1:9090/realms/cv-realm-test"
$PG_OPTS = "Timeout=30;Command Timeout=60"

$SERVICES = [ordered]@{}

$SERVICES["user-service"] = @{
    Dir = "$ROOT\backend\src\user-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5001"
        GRPC_PORT                              = "18082"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5433;Database=user_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        JWT_AUTHORITY                          = $KC_URL
        KAFKA_BOOTSTRAP_SERVERS                = $KAFKA
    }
}

$SERVICES["user-content-service"] = @{
    Dir = "$ROOT\backend\src\user-content-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5002"
        GRPC_PORT                              = "18083"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5434;Database=content_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        "Kafka__BootstrapServers"              = $KAFKA
    }
}

$SERVICES["workflow-service"] = @{
    Dir = "$ROOT\backend\src\workflow-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5003"
        GRPC_PORT                              = "18084"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5435;Database=workflow_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        JOB_EXTRACTOR_URL                      = "http://127.0.0.1:8001/api/v1/"
        SEARCH_AGENT_URL                       = "http://127.0.0.1:8002/api/v1/"
        TEMPLATE_AGENT_URL                     = "http://127.0.0.1:8003/api/v1/"
        CV_OPTIMIZER_URL                       = "http://127.0.0.1:8004/api/v1/"
        CONTACT_AGENT_URL                      = "http://127.0.0.1:8005/api/v1/"
        KAFKA_BOOTSTRAP_SERVERS                = $KAFKA
    }
}

$SERVICES["application-service"] = @{
    Dir = "$ROOT\backend\src\application-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5004"
        GRPC_PORT                              = "18085"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5436;Database=application_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        JWT_AUTHORITY                          = $KC_URL
        USER_SERVICE_GRPC_URL                  = "http://127.0.0.1:18082"
        KAFKA_BOOTSTRAP_SERVERS                = $KAFKA
    }
}

$SERVICES["cv-service"] = @{
    Dir = "$ROOT\backend\src\cv-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5005"
        GRPC_PORT                              = "18088"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5439;Database=cv_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        JWT_AUTHORITY                          = $KC_URL
        KAFKA_BOOTSTRAP_SERVERS                = $KAFKA
        MINIO_ENDPOINT                         = "http://127.0.0.1:9000"
        MINIO_ACCESS_KEY                       = "minioadmin"
        MINIO_SECRET_KEY                       = "minioadmin"
    }
}

$SERVICES["notification-service"] = @{
    Dir = "$ROOT\backend\src\notification-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5006"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5438;Database=notification_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        "Kafka__BootstrapServers"              = $KAFKA
        USER_SERVICE_GRPC_URL                  = "http://127.0.0.1:18082"
        APPLICATION_SERVICE_GRPC_URL           = "http://127.0.0.1:18085"
        "Smtp__Host"                           = "smtp.gmail.com"
        "Smtp__Port"                           = "587"
        "Smtp__EnableSsl"                      = "true"
        "Smtp__Username"                       = "$($env:Smtp__Username)"
        "Smtp__Password"                       = "$($env:Smtp__Password)"
        "Smtp__FromEmail"                      = "$($env:Smtp__FromEmail)"
    }
}

$SERVICES["job-offer-service"] = @{
    Dir = "$ROOT\backend\src\job-offer-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5007"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5437;Database=job_offer_test_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        "Kafka__BootstrapServers"              = $KAFKA
        JWT_AUTHORITY                          = $KC_URL
    }
}

$SERVICES["api-gateway"] = @{
    Dir = "$ROOT\api_gateway"
    Env = @{
        ASPNETCORE_ENVIRONMENT  = "Development"
        GATEWAY_HOST            = "localhost"
        GATEWAY_PORT            = "8080"
        KEYCLOAK_HOST           = "127.0.0.1"
        KEYCLOAK_PORT           = "9090"
        KEYCLOAK_EXTERNAL_HOST  = "localhost"
        KEYCLOAK_EXTERNAL_PORT  = "9090"
        KEYCLOAK_REALM          = "cv-realm-test"
        KEYCLOAK_CLIENT_ID      = "cv-gateway"
        KEYCLOAK_CLIENT_SECRET  = "change-me-in-production"
        KEYCLOAK_ADMIN_USERNAME = "admin"
        KEYCLOAK_ADMIN_PASSWORD = "admin"
        KAFKA_HOST              = "127.0.0.1"
        KAFKA_PORT              = "29092"
        FRONTEND_HOST           = "localhost"
        FRONTEND_PORT           = "4200"
        USER_SERVICE_HOST          = "127.0.0.1"
        USER_SERVICE_PORT          = "5001"
        CONTENT_SERVICE_HOST       = "127.0.0.1"
        CONTENT_SERVICE_PORT       = "5002"
        WORKFLOW_SERVICE_HOST      = "127.0.0.1"
        WORKFLOW_SERVICE_PORT      = "5003"
        APPLICATION_SERVICE_HOST   = "127.0.0.1"
        APPLICATION_SERVICE_PORT   = "5004"
        CV_SERVICE_HOST            = "127.0.0.1"
        CV_SERVICE_PORT            = "5005"
        NOTIFICATION_SERVICE_HOST  = "127.0.0.1"
        NOTIFICATION_SERVICE_PORT  = "5006"
        JOB_OFFER_SERVICE_HOST     = "127.0.0.1"
        JOB_OFFER_SERVICE_PORT     = "5007"
        GOOGLE_CLIENT_ID        = "$($env:GOOGLE_CLIENT_ID)"
        GOOGLE_CLIENT_SECRET    = "$($env:GOOGLE_CLIENT_SECRET)"
        GITHUB_CLIENT_ID        = "$($env:GITHUB_CLIENT_ID)"
        GITHUB_CLIENT_SECRET    = "$($env:GITHUB_CLIENT_SECRET)"
    }
}

# ---- Entry point ------------------------------------------------------------

if ($Stop) { Invoke-Stop; exit 0 }

Clear-Content $PID_FILE -ErrorAction SilentlyContinue

# Sentinel file: tells Cypress that the test stack is the one currently running.
Set-Content -Path $SENTINEL_FILE -Value "test-mode active since $(Get-Date -Format o)" -Encoding UTF8

Start-Infra

if (-not $NoReset) {
    Reset-TestData
} else {
    Write-Warn "Skipping data reset (-NoReset). Test DBs may carry state from prior runs."
}

if (-not $NoAgents) {
    Start-Agents
}

if (-not $InfraOnly) {
    Write-Host ""
    Write-Host "=== Building backend solution ===" -ForegroundColor Magenta
    Write-Step "dotnet build (shared libs must be compiled before parallel run)"
    $buildResult = & dotnet build "$ROOT\backend\CV_Generator.sln" --configuration Debug 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed. Fix errors above before starting services."
        Write-Host ($buildResult | Select-String "error" | Out-String)
        exit 1
    }
    Write-Ok "Build succeeded"

    Write-Host ""
    Write-Host "=== Starting .NET Services (test mode) ===" -ForegroundColor Magenta

    $toStart = if ($Service -ne "") {
        $Service -split ',' | ForEach-Object { $_.Trim() }
    } else {
        @($SERVICES.Keys)
    }

    foreach ($name in $toStart) {
        if (-not $SERVICES.ContainsKey($name)) {
            Write-Fail "Unknown service '$name'. Valid: $($SERVICES.Keys -join ', ')"
            continue
        }
        Start-DotnetService $name $SERVICES[$name]
        Start-Sleep -Milliseconds 1500
    }

    if (-not $NoFrontend) {
        Start-Frontend
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Yellow
Write-Host "  CV-Generator TEST stack is running" -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Service           URL" -ForegroundColor White
Write-Host "  ---------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Frontend          http://localhost:4200" -ForegroundColor White
Write-Host "  API Gateway       http://localhost:8080" -ForegroundColor White
Write-Host "  Keycloak realm    cv-realm-test" -ForegroundColor Yellow
Write-Host "  Postgres DBs      *_test_db on the existing containers" -ForegroundColor Yellow
Write-Host "  ---------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Sentinel file     $SENTINEL_FILE" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Now run your tests:" -ForegroundColor White
Write-Host "    k6 run tests/performance/scenarios/register.js" -ForegroundColor DarkGray
Write-Host "    cd frontend; npx cypress run" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  To stop:  .\dev.test.ps1 -Stop" -ForegroundColor DarkGray
Write-Host ""
