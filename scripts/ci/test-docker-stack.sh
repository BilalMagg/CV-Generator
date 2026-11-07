#!/usr/bin/env bash
# =============================================================================
# Propel - runtime smoke-test suite for the Docker compose stack
# =============================================================================
# Validates a RUNNING stack (default: deploy/docker-compose.yml, the same file
# friends use) end to end:
#   containers healthy / backend + gateway + agents health endpoints /
#   MinIO reachable / DB reachable with EF migrations applied /
#   Keycloak realm served / SPA served / host ports published /
#   gateway auth enforcement (401) / nginx /api proxying /
#   no supervisor program crashed (no restart loops)
#
# SINGLE SOURCE OF TRUTH: .github/workflows/docker-ci.yml invokes exactly this
# suite, so local results match CI results (bash on linux runners; run from
# the repo root; Git Bash on Windows works too).
#
# Usage:
#   bash scripts/ci/test-docker-stack.sh                # test a running stack
#   bash scripts/ci/test-docker-stack.sh --up           # boot it first
#   bash scripts/ci/test-docker-stack.sh --up --down    # ...and tear it down
#   bash scripts/ci/test-docker-stack.sh --latex        # image has texlive (:full)
#   bash scripts/ci/test-docker-stack.sh --only  't0[1-6]'   # run some tests
#   bash scripts/ci/test-docker-stack.sh --skip  't1[0-2]'   # skip host-port tests
#
# Options / environment overrides:
#   -f, --compose FILE     compose file       (default deploy/docker-compose.yml)
#       --up               docker compose up -d --wait before testing
#       --down             docker compose down -v after the summary
#       --latex            assert a real LaTeX engine is available (:full image)
#       --only RE / --skip RE    filter tests by name (regex)
#       --compose FILE     replace/add a compose file (repeatable, merges like
#                          docker compose -f a -f b — handy for override files)
#   GATEWAY_WAIT (default 120)   seconds to wait for the gateway before testing
#   WAIT_TIMEOUT (default 300)   --wait timeout for compose --up
#   MIGRATION_ATTEMPTS (40) x MIGRATION_INTERVAL (5)   migrations poll budget
#   HOST_FE/HOST_GW/HOST_KC (4200/8080/9090)           host-side published ports
#   APP_CONTAINER/PG_CONTAINER/.../KC_CONTAINER        container names
#
# Exit code: 1 if any test failed, 2 on setup problems, 0 otherwise.
# Skipped tests never fail the run.
# =============================================================================
set -u

COMPOSE_ARGS=(-f "deploy/docker-compose.yml")
DO_UP=0; DO_DOWN=0; LATEX=0; ONLY=""; SKIPRE=""
GATEWAY_WAIT="${GATEWAY_WAIT:-120}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-300}"
MIGRATION_ATTEMPTS="${MIGRATION_ATTEMPTS:-40}"
MIGRATION_INTERVAL="${MIGRATION_INTERVAL:-5}"

# container names must match container_name: in the compose files
APP_C="${APP_CONTAINER:-propel-app}"
PG_C="${PG_CONTAINER:-propel-postgres}"
KDB_C="${KDB_CONTAINER:-propel-keycloak-db}"
MINIO_C="${MINIO_CONTAINER:-propel-minio}"
KC_C="${KC_CONTAINER:-propel-keycloak}"

# host-side published ports (as configured in the friends' compose file)
HOST_FE="${HOST_FE:-4200}"
HOST_GW="${HOST_GW:-8080}"
HOST_KC="${HOST_KC:-9090}"

if [ -t 1 ]; then
  C_G='\033[32m'; C_R='\033[31m'; C_Y='\033[33m'; C_D='\033[2m'; C_0='\033[0m'
else
  C_G=''; C_R=''; C_Y=''; C_D=''; C_0=''
fi

compose() { docker compose "${COMPOSE_ARGS[@]}" "$@"; }

PASSED=0; FAILED=0; SKIPPED=0; FAILED_NAMES=""

pass() { PASSED=$((PASSED + 1)); printf "  ${C_G}PASS${C_0} %s (%ss)\n" "$1" "$(( $(date +%s) - $2 ))"; }
fail() { FAILED=$((FAILED + 1)); FAILED_NAMES="$FAILED_NAMES $1"; printf "  ${C_R}FAIL${C_0} %s (%ss)\n" "$1" "$(( $(date +%s) - $2 ))"; }

run_test() {
  local name="$1" fn="run_$1" t0 rc
  if [ -n "$SKIPRE" ] && printf '%s' "$name" | grep -Eq "$SKIPRE"; then
    SKIPPED=$((SKIPPED + 1)); printf "  ${C_Y}SKIP${C_0} %s (by --skip)\n" "$name"; return
  fi
  if [ -n "$ONLY" ] && [ -z "$(printf '%s' "$name" | grep -E "$ONLY")" ]; then
    SKIPPED=$((SKIPPED + 1)); printf "  ${C_Y}SKIP${C_0} %s (not in --only)\n" "$name"; return
  fi
  t0=$(date +%s)
  # each test runs in a subshell: a bad command inside one test must not stop
  # the whole suite
  if ( "$fn" ) >/tmp/propel-test-$name.log 2>&1; then rc=0; else rc=1; fi
  if [ "$rc" = 0 ]; then pass "$name" "$t0"
  else fail "$name" "$t0"
    echo "    ${C_D}output:${C_0}"
    sed -n '1,40p' /tmp/propel-test-$name.log | sed 's/^/      /'
  fi
  rm -f /tmp/propel-test-$name.log
}

# ── t01: every data-service container healthy, app container running ─────────
run_t01_containers_healthy() {
  local rc=0 c st
  for c in "$PG_C" "$KDB_C" "$MINIO_C" "$KC_C"; do
    st=$(docker inspect -f '{{.State.Health.Status}}' "$c" 2>/dev/null) || st="missing"
    echo "  $c health: $st"
    [ "$st" = "healthy" ] || rc=1
  done
  st=$(docker inspect -f '{{.State.Status}}' "$APP_C" 2>/dev/null) || st="missing"
  echo "  $APP_C state: $st"
  [ "$st" = "running" ] || rc=1
  return $rc
}

# ── t02: monolith backend health endpoint (in-container :5000) ────────────────
run_t02_backend_health() {
  local out
  out=$(docker exec "$APP_C" curl -fsS http://localhost:5000/health) || { echo "  backend :5000/health unreachable"; return 1; }
  echo "  $out"
  echo "$out" | grep -q '"status":"ok"'
}

# ── t03: agents sidecar health ────────────────────────────────────────────────
run_t03_agents_sidecar() {
  local out
  out=$(docker exec "$APP_C" curl -fsS http://localhost:8000/health) || { echo "  agents :8000/health unreachable"; return 1; }
  echo "  $out"
  echo "$out" | grep -q '"status":"ok"'
}

# ── t04: LLM provider catalog served (works keyless — keys configured in-app) ─
run_t04_llm_catalog() {
  local out
  out=$(docker exec "$APP_C" curl -fsS http://localhost:8000/api/llm/providers) || { echo "  /api/llm/providers unreachable"; return 1; }
  echo "  ${out:0:160}..."
  echo "$out" | grep -q '"providers"'
}

# ── t05: PDF capability probe — texlive-less image must degrade cleanly ───────
run_t05_pdf_capabilities() {
  local out
  out=$(docker exec "$APP_C" curl -fsS http://localhost:8000/api/agents/pdf/capabilities) || { echo "  capabilities endpoint unreachable"; return 1; }
  echo "  $out"
  echo "$out" | grep -q '"text_pdf_available":true' || { echo "  text PDF generation must always be available"; return 1; }
  if [ "$LATEX" = 1 ]; then
    echo "$out" | grep -q '"latex_engine_available":true' || { echo "  --latex: expected a real LaTeX engine (did you test the :full image?)"; return 1; }
  fi
}

# ── t06: MinIO reachable from the app container (in-network service name) ─────
run_t06_minio_reachable() {
  local code
  code=$(docker exec "$APP_C" curl -s -o /dev/null -w '%{http_code}' http://minio:9000/minio/health/live)
  echo "  minio /health/live from app container -> $code"
  [ "$code" = "200" ]
}

# ── t07: postgres accepts connections on the app database ────────────────────
run_t07_postgres_ready() {
  local out
  out=$(compose exec -T postgres pg_isready -U postgres -d cv_monolith) || return 1
  echo "  $out"
}

# ── t08: EF migrations ran at backend startup (poll — startup applies 40+) ────
run_t08_migrations_ran() {
  local i n ok=""
  for i in $(seq 1 "$MIGRATION_ATTEMPTS"); do
    n=$(compose exec -T postgres psql -U postgres -d cv_monolith \
      -tAc 'select count(*) from "__EFMigrationsHistory"' 2>/dev/null) || n=""
    echo "  attempt $i: migration history rows: ${n:-n/a}"
    if [ "${n:-0}" -gt 0 ] 2>/dev/null; then ok=1; break; fi
    sleep "$MIGRATION_INTERVAL"
  done
  if [ -z "$ok" ]; then
    echo "  migrations did not complete - dumping diagnostics"
    echo "  existing tables:"
    compose exec -T postgres psql -U postgres -d cv_monolith -tAc \
      "select tablename from pg_tables where schemaname='public' order by 1" 2>/dev/null | sed 's/^/    /'
    echo "  backend log (migration/startup/error lines):"
    docker exec "$APP_C" sh -c \
      'grep -iE "migrat|now listening|migration failed|exception|error" /var/log/supervisor/backend.log | head -80' 2>/dev/null | sed 's/^/    /' || true
    return 1
  fi
}

# ── t09: Keycloak realm imported and issuing its OIDC document ────────────────
run_t09_keycloak_realm() {
  local out
  # in-container via the service name: independent of host port publishing
  out=$(docker exec "$APP_C" curl -fsS http://keycloak:8080/realms/cv-realm/.well-known/openid-configuration) \
    || { echo "  realm cv-realm openid-configuration unreachable (import failed?)"; return 1; }
  echo "  ${out:0:160}..."
  echo "$out" | grep -q '"issuer"' || { echo "  issuer document incomplete"; return 1; }
  echo "$out" | grep -q 'realms/cv-realm' || { echo "  wrong realm in issuer document"; return 1; }
}

# ── t10: all three host ports published and serving what friends expect ──────
run_t10_host_ports_published() {
  local rc=0 out
  out=$(curl -fsS "http://localhost:$HOST_FE" 2>/dev/null) || { echo "  frontend not served on :$HOST_FE"; rc=1; }
  echo "$out" | grep -q '<app-root' || { echo "  :$HOST_FE is not the Angular SPA"; rc=1; }
  echo "  :$HOST_FE serves the Angular SPA"
  curl -fsS "http://localhost:$HOST_GW/health" >/dev/null 2>&1 || { echo "  gateway /health not reachable on :$HOST_GW"; rc=1; }
  echo "  :$HOST_GW gateway healthy"
  out=$(curl -fsS "http://localhost:$HOST_KC/realms/cv-realm/.well-known/openid-configuration" 2>/dev/null) \
    || { echo "  keycloak realm not reachable on :$HOST_KC"; rc=1; }
  echo "$out" | grep -q '"issuer"' || { echo "  :$HOST_KC served but issuer document incomplete"; rc=1; }
  echo "  :$HOST_KC keycloak realm serves its OIDC document"
  return $rc
}

# ── t11: the gateway enforces auth on proxied /api routes ────────────────────
run_t11_gateway_auth_401() {
  local code
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$HOST_GW/api/llm-settings")
  echo "  GET :$HOST_GW/api/llm-settings without auth -> $code"
  [ "$code" = "401" ]
}

# ── t12: nginx (frontend) proxies /api through to the gateway/auth path ──────
run_t12_frontend_api_proxy_401() {
  local code
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$HOST_FE/api/llm-settings")
  echo "  GET :$HOST_FE/api/llm-settings (nginx -> gateway -> backend) -> $code"
  [ "$code" = "401" ]
}

# ── t13: no supervisor program crashed / restart-looped ───────────────────────
run_t13_supervisor_stable() {
  local hits
  # "exited ... not expected" appears on every unexpected crash; FATAL means a
  # program gave up for good (startretries exhausted)
  docker exec "$APP_C" sh -c 'grep -iE "FATAL|not expected" /var/log/supervisor/supervisord.log' > /tmp/propel-sup.log 2>&1
  if [ -s /tmp/propel-sup.log ]; then
    echo "  supervisor recorded unexpected process exits:"
    sed -n '1,10p' /tmp/propel-sup.log | sed 's/^/    /'
    rm -f /tmp/propel-sup.log
    return 1
  fi
  rm -f /tmp/propel-sup.log
  echo "  supervisor log has no FATAL / unexpected exits"
}

TESTS=(
  t01_containers_healthy
  t02_backend_health
  t03_agents_sidecar
  t04_llm_catalog
  t05_pdf_capabilities
  t06_minio_reachable
  t07_postgres_ready
  t08_migrations_ran
  t09_keycloak_realm
  t10_host_ports_published
  t11_gateway_auth_401
  t12_frontend_api_proxy_401
  t13_supervisor_stable
)

wait_for_gateway() {
  local i
  for i in $(seq 1 $((GATEWAY_WAIT / 5))); do
    if docker exec "$APP_C" curl -fsS http://localhost:8080/health >/dev/null 2>&1; then
      echo "gateway ready after $((i * 5))s"
      return 0
    fi
    sleep 5
  done
  echo "gateway not ready after ${GATEWAY_WAIT}s — dumping app logs" >&2
  compose logs app --tail 200 || true
  return 1
}

usage() { grep -E '^#   (-f|--up|--down|--latex|--only|--skip|GATEWAY_WAIT|bash scripts)' "$0" | sed 's/^# *//'; exit 0; }

while [ $# -gt 0 ]; do
  case "$1" in
    -f|--compose)
      [ -f "$2" ] || { echo "compose file not found: $2 — run from the repo root or pass -f <path>" >&2; exit 2; }
      COMPOSE_ARGS+=(-f "$2"); shift 2 ;;
    --up)               DO_UP=1; shift ;;
    --down)             DO_DOWN=1; shift ;;
    --latex)            LATEX=1; shift ;;
    --only)             ONLY="$2"; shift 2 ;;
    --skip)             SKIPRE="$2"; shift 2 ;;
    -h|--help)          usage ;;
    *) echo "unknown option: $1 (see --help)" >&2; exit 2 ;;
  esac
done

echo "compose: ${COMPOSE_ARGS[*]}"

if [ "$DO_UP" = 1 ]; then
  echo "== booting the stack (compose up --wait, timeout ${WAIT_TIMEOUT}s)"
  compose up -d --no-build --wait --wait-timeout "$WAIT_TIMEOUT" || { echo "compose up failed" >&2; exit 2; }
  compose ps
else
  if ! wait_for_gateway; then
    echo "stack not reachable — start it first (docker compose -f deploy/docker-compose.yml up -d) or pass --up" >&2
    exit 2
  fi
fi

echo ""
echo "== Propel Docker stack smoke tests =="
T_START=$(date +%s)
for t in "${TESTS[@]}"; do run_test "$t"; done
T_TOTAL=$(( $(date +%s) - T_START ))

echo ""
failcol=""
[ "$FAILED" -gt 0 ] && failcol=$(printf "$C_R")
printf "summary: ${C_G}%d passed${C_0}, ${failcol}%d failed${C_0}, ${C_Y}%d skipped${C_0} in ${T_TOTAL}s\n" \
  "$PASSED" "$FAILED" "$SKIPPED"
[ -n "$FAILED_NAMES" ] && echo "failed:$FAILED_NAMES"

if [ "$DO_DOWN" = 1 ]; then
  echo "== tearing the stack down"
  compose down -v --remove-orphans || true
fi

if [ "$FAILED" -gt 0 ]; then exit 1; else exit 0; fi
