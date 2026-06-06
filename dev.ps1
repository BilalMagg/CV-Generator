#!/usr/bin/env pwsh
# dev.ps1 - Local development launcher for CV-Generator
#
# Infrastructure (Kafka, Keycloak, DBs, MinIO) runs in Docker.
# AI agents run in Docker (Python FastAPI).
# All .NET services run with dotnet run.
# Angular runs with ng serve (npm start).
#
# USAGE:
#   .\dev.ps1                          -- start everything
#   .\dev.ps1 -Stop                    -- kill local processes + tear down Docker
#   .\dev.ps1 -InfraOnly               -- Docker infra only, no dotnet/ng
#   .\dev.ps1 -NoAgents                -- skip AI agents Docker compose
#   .\dev.ps1 -NoFrontend              -- skip Angular ng serve
#   .\dev.ps1 -Service user-service    -- start only one .NET service
#   .\dev.ps1 -Service "user-service,api-gateway"  -- start a subset

param(
    [switch]$Stop,
    [switch]$InfraOnly,
    [switch]$NoAgents,
    [switch]$NoFrontend,
    [string]$Service = ""
)

$ROOT     = $PSScriptRoot
$PID_FILE = "$ROOT\.dev.pids"
$SHELL_EXE = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }

# =============================================================================
# FUNCTION DEFINITIONS  (all defined before any execution code)
# =============================================================================

function Write-Step([string]$msg) { Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  !! $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  XX $msg" -ForegroundColor Red }

# Reads key=value lines (with or without surrounding quotes) into process env.
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

# Wait for a Docker container to report healthy status.
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

# Write a temp ps1 file so subprocess receives env vars without quoting issues.
function New-TempScript([string]$Name, [string[]]$Lines) {
    $path = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "cv-dev-$Name.ps1")
    $Lines | Set-Content $path -Encoding UTF8
    return $path
}

function Start-InNewWindow([string]$ScriptPath) {
    $proc = Start-Process $SHELL_EXE -ArgumentList "-NoExit", "-File", $ScriptPath -PassThru
    Add-Content $PID_FILE $proc.Id
    return $proc
}

# Launch a .NET service in its own shell window with the correct env vars.
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
    $lines += "Write-Host '[$Name] dotnet run --no-build$port' -ForegroundColor Cyan"
    $lines += "dotnet run --no-build"

    $script = New-TempScript $Name $lines
    $proc   = Start-InNewWindow $script
    Write-Ok "$Name  PID $($proc.Id)$port"
}

# Launch Angular frontend in its own shell window.
function Start-Frontend {
    Write-Host ""
    Write-Host "=== Starting Angular Frontend ===" -ForegroundColor Magenta
    $lines = @(
        "Set-Location '$($ROOT -replace "'", "''")\\frontend'",
        "Write-Host '[frontend] npm start' -ForegroundColor Cyan",
        "npm start"
    )
    $script = New-TempScript "frontend" $lines
    $proc   = Start-InNewWindow $script
    Write-Ok "Frontend  PID $($proc.Id)  http://localhost:4200"
}

# Start Docker infrastructure (infra compose only).
function Start-Infra {
    Write-Host ""
    Write-Host "=== Starting Docker Infrastructure ===" -ForegroundColor Magenta
    # Ensure the shared Docker network exists before any compose file tries to use it.
    $netExists = docker network ls --format "{{.Name}}" | Where-Object { $_ -eq "cv-network" }
    if (-not $netExists) {
        docker network create cv-network | Out-Null
        Write-Ok "Created cv-network"
    }
    docker compose -f "$ROOT\docker-compose.infra.yml" up -d
    Write-Ok "Containers started"

    Wait-Healthy "cv-kafka" 120
    foreach ($db in @("cv-user-db","cv-content-db","cv-workflow-db","cv-application-db","cv-job-offer-db","cv-notification-db","cv-cv-db")) {
        Wait-Healthy $db 60
    }
    Wait-Healthy "cv-keycloak" 180

    # Give all PostgreSQL containers extra time to fully accept connections after
    # their health check passes. Without this, concurrent service startups race
    # to run migrations and hit connection timeouts.
    Write-Step "Waiting 20s for databases to fully warm up..."
    Start-Sleep -Seconds 20
    Write-Ok "Databases ready"
}

# Start AI agents in Docker, pointing them to the local API gateway.
function Start-Agents {
    Write-Host ""
    Write-Host "=== Starting AI Agents (Docker) ===" -ForegroundColor Magenta
    # host.docker.internal resolves to the Windows host from inside Docker.
    $env:GATEWAY_HOST = "host.docker.internal"
    $env:GATEWAY_PORT = "8080"
    $env:CV_NETWORK   = "cv-network"
    docker compose -f "$ROOT\ai_agents\docker-compose.yml" up -d
    Write-Ok "AI agents started on ports 8001-8006"
}

# Stop all tracked local processes and Docker services.
function Invoke-Stop {
    Write-Host ""
    Write-Host "=== Stopping CV-Generator ===" -ForegroundColor Magenta

    # 1. Kill by PID file (processes we spawned this session)
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

    # 2. Kill by well-known ports — catches stale processes from interrupted runs
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

    # 3. Tear down Docker
    Write-Step "Tearing down Docker infra..."
    docker compose -f "$ROOT\docker-compose.infra.yml" down
    if (Test-Path "$ROOT\ai_agents\docker-compose.yml") {
        docker compose -f "$ROOT\ai_agents\docker-compose.yml" down --remove-orphans 2>$null
    }
    Write-Ok "Docker services stopped"
}

# =============================================================================
# EXECUTION  (all functions are defined above)
# =============================================================================

# Load credentials into process environment before building $SERVICES.
Import-DotEnv "$ROOT\.env"
Import-DotEnv "$ROOT\backend\src\notification-service\.env"

# Short aliases used throughout the service definitions below.
#
# NOTE: We use 127.0.0.1 instead of localhost for every host->Docker connection.
# On Windows, localhost resolves to ::1 (IPv6) first, but Docker Desktop only
# forwards ports on 0.0.0.0 (IPv4). .NET hangs ~15s waiting for the IPv6
# connection before falling back, which exceeds Npgsql's connect timeout and
# causes the random "operation has timed out" failures during migrations.
$PG_USER = "postgres"
$PG_PASS = "postgres"
$KAFKA   = "127.0.0.1:29092"
$KC_URL  = "http://127.0.0.1:9090/realms/cv-realm"
$PG_OPTS = "Timeout=30;Command Timeout=60"

# ---- Service registry -------------------------------------------------------
$SERVICES = [ordered]@{}

$SERVICES["user-service"] = @{
    Dir = "$ROOT\backend\src\user-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5001"
        GRPC_PORT                              = "18082"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5433;Database=user_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
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
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5434;Database=content_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        "Kafka__BootstrapServers"              = $KAFKA
    }
}

$SERVICES["workflow-service"] = @{
    Dir = "$ROOT\backend\src\workflow-service"
    Env = @{
        ASPNETCORE_ENVIRONMENT                 = "Development"
        PORT                                   = "5003"
        GRPC_PORT                              = "18084"
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5435;Database=workflow_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
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
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5436;Database=application_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
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
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5439;Database=cv_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
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
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5438;Database=notification_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
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
        "ConnectionStrings__DefaultConnection" = "Host=127.0.0.1;Port=5437;Database=job_offer_db;Username=$PG_USER;Password=$PG_PASS;$PG_OPTS"
        "Kafka__BootstrapServers"              = $KAFKA
        JWT_AUTHORITY                          = $KC_URL
    }
}

$SERVICES["api-gateway"] = @{
    Dir = "$ROOT\api_gateway"
    Env = @{
        ASPNETCORE_ENVIRONMENT  = "Development"
        # Browser-facing hosts stay as "localhost" (these end up in redirect URLs).
        GATEWAY_HOST            = "localhost"
        GATEWAY_PORT            = "8080"
        # Internal server-to-server call goes via 127.0.0.1 (avoid IPv6 hang).
        KEYCLOAK_HOST           = "127.0.0.1"
        KEYCLOAK_PORT           = "9090"
        KEYCLOAK_EXTERNAL_HOST  = "localhost"
        KEYCLOAK_EXTERNAL_PORT  = "9090"
        KEYCLOAK_REALM          = "cv-realm"
        KEYCLOAK_CLIENT_ID      = "cv-gateway"
        KEYCLOAK_CLIENT_SECRET  = "change-me-in-production"
        KEYCLOAK_ADMIN_USERNAME = "admin"
        KEYCLOAK_ADMIN_PASSWORD = "admin"
        KAFKA_HOST              = "127.0.0.1"
        KAFKA_PORT              = "29092"
        FRONTEND_HOST           = "localhost"
        FRONTEND_PORT           = "4200"
        # YARP downstream targets — point to local dotnet ports, not Docker hostnames.
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

Start-Infra

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
    Write-Host "=== Starting .NET Services ===" -ForegroundColor Magenta

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
Write-Host "=====================================" -ForegroundColor Green
Write-Host "  CV-Generator local dev is running!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Service           URL" -ForegroundColor White
Write-Host "  ---------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Frontend          http://localhost:4200" -ForegroundColor White
Write-Host "  API Gateway       http://localhost:8080" -ForegroundColor White
Write-Host "  user-service      http://localhost:5001  gRPC :18082" -ForegroundColor White
Write-Host "  content-svc       http://localhost:5002  gRPC :18083" -ForegroundColor White
Write-Host "  workflow-svc      http://localhost:5003  gRPC :18084" -ForegroundColor White
Write-Host "  application-svc   http://localhost:5004  gRPC :18085" -ForegroundColor White
Write-Host "  cv-service        http://localhost:5005  gRPC :18088" -ForegroundColor White
Write-Host "  notification      http://localhost:5006" -ForegroundColor White
Write-Host "  job-offer         http://localhost:5007" -ForegroundColor White
Write-Host "  ---------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Keycloak          http://localhost:9090  (admin / admin)" -ForegroundColor DarkCyan
Write-Host "  Kafka UI          http://localhost:8090" -ForegroundColor DarkCyan
Write-Host "  MinIO console     http://localhost:9001  (minioadmin / minioadmin)" -ForegroundColor DarkCyan
Write-Host "  AI agents         http://localhost:8001-8006" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  To stop:  .\dev.ps1 -Stop" -ForegroundColor DarkGray
Write-Host ""
