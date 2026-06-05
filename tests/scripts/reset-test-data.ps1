#!/usr/bin/env pwsh
# reset-test-data.ps1
#
# Drops + recreates the 7 service test databases inside the existing dev
# Postgres containers, then deletes + re-imports the cv-realm-test realm
# in the existing Keycloak container. Safe to run on every test-stack
# startup -- guarantees the test stack always starts from a known empty
# state without touching the dev databases or the cv-realm realm.
#
# Prerequisites:
#   - docker compose -f docker-compose.infra.yml up -d   (containers must be running)
#
# Exit codes:
#   0  success
#   1  a docker exec / HTTP call failed; check the inline error
#
# Usage:
#   .\tests\scripts\reset-test-data.ps1
#   .\tests\scripts\reset-test-data.ps1 -SkipKeycloak    # Postgres only
#   .\tests\scripts\reset-test-data.ps1 -SkipPostgres    # Realm only

param(
    [switch]$SkipPostgres,
    [switch]$SkipKeycloak,
    [string]$KeycloakUrl      = "http://127.0.0.1:9090",
    [string]$KeycloakAdminUser= "admin",
    [string]$KeycloakAdminPass= "admin",
    [string]$RealmFile        = ""
)

# NOTE: keep this at "Continue", not "Stop".
# In Windows PowerShell 5.1, docker writing a NOTICE to stderr (e.g.
# "database X does not exist, skipping" on DROP IF EXISTS) is wrapped as a
# NativeCommandError. With EAP=Stop that becomes a terminating error and
# kills the script even though docker exited 0. We rely on explicit
# $LASTEXITCODE checks after each docker call and try/catch around REST
# calls for real error handling.
$ErrorActionPreference = "Continue"
$ROOT = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ($RealmFile -eq "") {
    # Lives next to this script - intentionally NOT under
    # api_gateway/keycloak-realm/ so Keycloak's auto-importer never sees it.
    # We import it only via the admin REST API below, so we can refresh it
    # on demand without restarting Keycloak.
    $RealmFile = Join-Path $PSScriptRoot "cv-realm-test-realm.json"
}

function Write-Step([string]$msg) { Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  !! $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg) { Write-Host "  XX $msg" -ForegroundColor Red }

# -- Postgres: drop + recreate the 7 test databases --------------------------
function Reset-PostgresTestDbs {
    Write-Host ""
    Write-Host "=== Resetting Postgres test databases ===" -ForegroundColor Magenta

    $dbs = @(
        @{ Container = "cv-user-db";         Db = "user_test_db" },
        @{ Container = "cv-content-db";      Db = "content_test_db" },
        @{ Container = "cv-workflow-db";     Db = "workflow_test_db" },
        @{ Container = "cv-application-db";  Db = "application_test_db" },
        @{ Container = "cv-job-offer-db";    Db = "job_offer_test_db" },
        @{ Container = "cv-notification-db"; Db = "notification_test_db" },
        @{ Container = "cv-cv-db";           Db = "cv_test_db" }
    )

    foreach ($d in $dbs) {
        Write-Step "$($d.Container) -> $($d.Db)"

        # Terminate any leftover connections so DROP DATABASE doesn't block.
        # We swallow stdout via Out-Null and let stderr go where it likes -
        # do NOT use 2>&1 or *> here, it trips NativeCommandError on PS 5.1.
        $kill = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$($d.Db)' AND pid <> pg_backend_pid();"
        docker exec $d.Container psql -U postgres -d postgres -c $kill 2>&1 | Out-Null

        docker exec $d.Container psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS $($d.Db);" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "DROP failed on $($d.Container) for $($d.Db)"
            exit 1
        }

        docker exec $d.Container psql -U postgres -d postgres -c "CREATE DATABASE $($d.Db);" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "CREATE failed on $($d.Container) for $($d.Db)"
            exit 1
        }
        Write-Ok "$($d.Db) ready"
    }
}

# -- Keycloak: delete + re-import the cv-realm-test realm --------------------
function Wait-KeycloakReady {
    param([int]$TimeoutSec = 180)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $probeUrl = "$KeycloakUrl/realms/master/.well-known/openid-configuration"
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Method Get -Uri $probeUrl -TimeoutSec 5 -UseBasicParsing
            if ($r.StatusCode -eq 200) { return $true }
        } catch {
            # not ready yet, retry
        }
        Start-Sleep -Seconds 3
    }
    return $false
}

function Get-KeycloakAdminToken {
    $body = @{
        grant_type = "password"
        client_id  = "admin-cli"
        username   = $KeycloakAdminUser
        password   = $KeycloakAdminPass
    }
    $tokenUrl = "$KeycloakUrl/realms/master/protocol/openid-connect/token"
    try {
        $resp = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $body `
            -ContentType "application/x-www-form-urlencoded" -TimeoutSec 30
        return $resp.access_token
    } catch {
        Write-Fail "Could not get Keycloak admin token: $($_.Exception.Message)"
        Write-Warn "Is Keycloak healthy on $KeycloakUrl ?"
        exit 1
    }
}

function Reset-KeycloakTestRealm {
    Write-Host ""
    Write-Host "=== Resetting Keycloak cv-realm-test ===" -ForegroundColor Magenta

    if (-not (Test-Path $RealmFile)) {
        Write-Fail "Realm file not found: $RealmFile"
        exit 1
    }

    Write-Step "Waiting for Keycloak to be reachable at $KeycloakUrl ..."
    if (-not (Wait-KeycloakReady -TimeoutSec 180)) {
        Write-Fail "Keycloak did not respond on $KeycloakUrl within 180s."
        Write-Warn "Check 'docker logs cv-keycloak' - it may still be starting."
        exit 1
    }
    Write-Ok "Keycloak responding"

    Write-Step "Fetching admin token..."
    $token = Get-KeycloakAdminToken
    $headers = @{ Authorization = "Bearer $token" }

    Write-Step "Deleting cv-realm-test (idempotent)..."
    try {
        Invoke-RestMethod -Method Delete -Uri "$KeycloakUrl/admin/realms/cv-realm-test" `
            -Headers $headers -TimeoutSec 30 | Out-Null
        Write-Ok "Existing realm deleted"
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) {
            Write-Ok "Realm did not exist (404) - fresh import"
        } else {
            Write-Fail "Delete failed: $($_.Exception.Message)"
            exit 1
        }
    }

    Write-Step "Importing realm from $RealmFile..."
    $body = Get-Content -Raw -Path $RealmFile
    try {
        Invoke-RestMethod -Method Post -Uri "$KeycloakUrl/admin/realms" `
            -Headers $headers -Body $body -ContentType "application/json" -TimeoutSec 60 | Out-Null
        Write-Ok "cv-realm-test imported"
    } catch {
        Write-Fail "Realm import failed: $($_.Exception.Message)"
        exit 1
    }
}

# -- Entry point ------------------------------------------------------------─
if (-not $SkipPostgres) { Reset-PostgresTestDbs }
if (-not $SkipKeycloak) { Reset-KeycloakTestRealm }

Write-Host ""
Write-Host "  Test data reset complete." -ForegroundColor Green
Write-Host ""
