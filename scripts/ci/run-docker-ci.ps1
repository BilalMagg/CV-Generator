<# ==========================================================================
 run-docker-ci.ps1 — Propel remote Docker CI control (GitHub Actions via gh)

 Usage examples:

   # push/PR builds run automatically. To run manually (with default inputs):
   ./scripts/ci/run-docker-ci.ps1 -Ref dev

   # with inputs (texlive variant, skip tests):
   ./scripts/ci/run-docker-ci.ps1 -Ref dev -Inputs @{ build_full = "true" }

   # watch a specific run (or just fetch its result + failed logs):
   ./scripts/ci/run-docker-ci.ps1 -RunId 1234567890

 Requires: GitHub CLI (gh) authenticated; no paid software.
 Exit code 0 on success, 1 when the workflow fails or cannot be triggered.
 ========================================================================== #>
[CmdletBinding()]
param(
    [string]$Workflow = "docker-ci.yml",
    [string]$Ref,
    [string]$RunId,
    [hashtable]$Inputs = @{},
    [switch]$NoWatch
)

$ErrorActionPreference = "Stop"

function Get-GitBranch {
    git rev-parse --abbrev-ref HEAD 2>$null
}

function Get-RunStatus([string]$Id) {
    gh run list --workflow="$Workflow" --limit 20 --json databaseId,status,conclusion,headBranch,event,createdAt `
        | ConvertFrom-Json | Where-Object { $_.databaseId -eq $Id }
}

function Show-FailedLogs([string]$Id) {
    Write-Host "`n── failed step logs for run $Id ──────────────────────────" -ForegroundColor Yellow
    gh run view $Id --log-failed
}

try {
    if (-not $Ref) { $Ref = Get-GitBranch }
    if ([string]::IsNullOrWhiteSpace($Ref)) { $Ref = "main" }

    if ([string]::IsNullOrWhiteSpace($RunId)) {
        # ── trigger a new run ────────────────────────────────────────────
        $ghArgs = @("workflow", "run", $Workflow, "--ref", $Ref)
        $keys = @()
        foreach ($k in $Inputs.Keys) {
            $ghArgs += @("-f", "$k=$($Inputs[$k])")
            $keys += $k
        }
        if ($keys.Count -gt 0) {
            Write-Host "Triggering $Workflow on $Ref with inputs: $($keys -join ', ')" -ForegroundColor Cyan
        } else {
            Write-Host "Triggering $Workflow on $Ref" -ForegroundColor Cyan
        }
        gh @ghArgs
        if ($LASTEXITCODE -ne 0) { Write-Host "gh workflow run failed" -ForegroundColor Red; exit 1 }

        Start-Sleep -Seconds 8   # allow the run to register

        $run = gh run list --workflow="$Workflow" --limit 5 --json databaseId,status,event,createdAt,headBranch `
            | ConvertFrom-Json `
            | Where-Object { $_.event -eq "workflow_dispatch" } `
            | Sort-Object { [datetime]$_.createdAt } -Descending `
            | Select-Object -First 1
        $RunId = $run.databaseId
        if (-not $RunId) { Write-Host "could not locate the new run id" -ForegroundColor Red; exit 1 }
        Write-Host "Run started: $RunId" -ForegroundColor Green
    }

    # ── watch / report ───────────────────────────────────────────────────
    if (-not $NoWatch) {
        gh run watch $RunId --exit-status --interval 15
        $watchRc = $LASTEXITCODE
    } else {
        do {
            Start-Sleep -Seconds 20
            $run = Get-RunStatus $RunId
            Write-Host ("run {0}: {1}" -f $RunId, $run.status)
        } while ($run.status -eq "in_progress" -or $run.status -eq "queued")
        $watchRc = if ($run.conclusion -eq "success") { 0 } else { 1 }
    }

    $run = Get-RunStatus $RunId
    Write-Host "`n── final status ──────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host ("run {0} [{1}] status={2} conclusion={3}" -f $RunId, $run.headBranch, $run.status, $run.conclusion)

    if ($watchRc -ne 0 -or $run.conclusion -ne "success") {
        Show-FailedLogs $RunId
        exit 1
    }

    Write-Host "SUCCESS" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "error: $_" -ForegroundColor Red
    exit 1
}
