# Propel deployment

Everything needed to run Propel is in this folder. Two workflows:

| Who | How |
|---|---|
| **Friends / non-technical** | Single command: `docker compose up` — the whole app in one container + Postgres/Keycloak/MinIO beside it |
| **The developer's own machine** (normal dev) | Backend, gateway, agents and frontend run natively; only the stateful services are containerized via `docker-compose.data.yml` |

## 1. Requirements

- Docker Desktop (or any Docker Engine + compose plugin)
- Nothing else — no Python, no Node, no .NET on the host

## 2. Friends: run everything

From the repo root:

```bash
cp deploy/.env.example deploy/.env   # fill the Gmail values (optional)
docker compose -f deploy/docker-compose.yml up -d --build
```

Then open **http://localhost:4200** — the first user signs up through the
in-app registration flow (the imported Keycloak realm has registration
enabled). Configure LLM API keys under **Settings → AI Model** inside the app
(no `.env` editing needed — see below).

Exposed ports:

| Port | What |
|---|---|
| 4200 | Frontend (nginx proxies `/api` to the gateway) |
| 8080 | Gateway (used for the sign-in callback; also handy for API debugging) |
| 9090 | Keycloak (browser-facing login) |
| 9001 | MinIO console (read-only browsing) |

### Gmail (optional — needed to actually send application emails)

1. Create an OAuth client (type *Web application*) in your Google Cloud
   project and add the authorized redirect
   `http://localhost:4200/api/gmail/callback`.
2. Put the client id/secret and a `GMAIL_TOKEN_ENCRYPTION_KEY`
   (`openssl rand -base64 32`) into `deploy/.env`.
3. In the app, Mailbox → Connect Gmail.

### LLM API keys

Friends **never** touch `.env` for this. Open the app → **Settings → AI
Model**, paste the key(s) (OpenRouter, Groq, OpenAI, Google, Mistral…).
Keys are AES-encrypted in the local database and pushed into the agent
sidecar's memory — they never touch disk in plaintext and survive sidecar
restarts (automatically re-pushed when the settings page loads). Server-side
`.env` keys remain a valid fallback anyone who prefers config files.

## 3. The images

### `deploy/Dockerfile` — the application image

Single multi-stage build producing **one image** that contains:

- the .NET monolith backend (`:5000`)
- the API gateway (`:8080`, Keycloak auth + YARP proxy to the backend)
- the Python AI agents sidecar (`:8000`)
- nginx serving the compiled Angular frontend (`:4200`)

Postgres / Keycloak / MinIO are **not** baked in — they ship as official
images from the compose file, so the default image stays small.

Variants:

```bash
# default (no LaTeX engine — PDF compilation degrades gracefully,
# .tex is still generated and can be compiled on Overleaf)
docker build -f deploy/Dockerfile -t propel:latest .

# full variant with texlive (PDF compilation works offline)
docker build -f deploy/Dockerfile -t propel:full --build-arg WITH_LATEX=true .
```

### Accelerating the Python install (optional, offline builds)

The agents dependency set is large; on a slow/flaky connection the
`pip install` step can crawl. Generate a local **wheelhouse** once — the
Docker build automatically prefers it (offline, instant installs):

```bash
cd monolith/agents
../.venv/Scripts/python -m pip download -d ../../deploy/wheelhouse \
    --only-binary=:all: --python-version 312 \
    --platform manylinux2014_x86_64 --implementation cp . \
    hatchling setuptools wheel
```

The two download targets matter: without `hatchling setuptools wheel`
in the wheelhouse, offline installs abort ("No module named 'hatchling'"
/ "No matching distribution found for setuptools") — the build backend
is a build dependency, not a runtime one.

(`deploy/wheelhouse/` is git-ignored; the build falls back to PyPI when
it's empty.)

## 4. Remote CI (GitHub Actions)

`.github/workflows/docker-ci.yml` builds the real image on GitHub runners and
 boots the actual friends' compose stack to validate it (health endpoints,
 migrations, Keycloak realm, SPA serving, auth enforcement). No registry is
 needed — builds run and are tested on the runner; BuildKit layer cache is
 persisted via `type=gha` so subsequent builds skip unchanged stages.

```powershell
# automatic: every push to main/dev (unless only docs changed)
# manual trigger + live monitoring from PowerShell:
gh workflow run docker-ci.yml --ref dev
gh run watch <run-id>                    # live progress
gh run view <run-id> --log               # full logs
gh run view <run-id> --log-failed        # only failing steps
gh run download <run-id> -n docker-diagnostics   # logs captured on failure
```

Or use the helper (`scripts/ci/run-docker-ci.ps1`) which triggers, watches and
pulls failed logs automatically — exits nonzero on failure:

```powershell
./scripts/ci/run-docker-ci.ps1 -Ref dev
# texlive-variant build, runtime tests skipped:
./scripts/ci/run-docker-ci.ps1 -Ref dev -Inputs @{ build_full = "true"; run_tests = "false" }
```

CI builds use the PyPI path (the wheelhouse is git-ignored), so GitHub
runners also exercise the slow-network fallback path.

## 5. Runtime smoke tests

`scripts/ci/test-docker-stack.sh` is the SINGLE source of truth for runtime
validation — the GitHub workflow runs exactly this suite, and so can you,
against your own stack. 13 tests verify the app behaves as friends expect:

| # | Test | What it proves |
|---|---|---|
| t01 | containers_healthy | postgres / keycloak-db / minio / keycloak healthy, app running |
| t02 | backend_health | backend `:5000/health` returns `{"status":"ok"}` |
| t03 | agents_sidecar | agents sidecar `:8000/health` ok |
| t04 | llm_catalog | `/api/llm/providers` serves the catalog (works keyless) |
| t05 | pdf_capabilities | text-PDF stays available; `--latex` also asserts a real LaTeX engine |
| t06 | minio_reachable | app container reaches MinIO by its compose service name |
| t07 | postgres_ready | `pg_isready` accepts connections on `cv_monolith` |
| t08 | migrations_ran | `__EFMigrationsHistory` rows > 0 (polls through backend startup) |
| t09 | keycloak_realm | imported `cv-realm` serves its OIDC issuer document |
| t10 | host_ports_published | `:4200` serves the SPA, `:8080` gateway health, `:9090` realm |
| t11 | gateway_auth_401 | unauthenticated `/api` call via the gateway → 401 |
| t12 | frontend_api_proxy_401 | nginx `/api` → gateway → 401 (exact browser path) |
| t13 | supervisor_stable | no process crashed / restart-looped inside the image |

From the repo root:

```bash
# boot, test, tear down — the full one-command verification:
bash scripts/ci/test-docker-stack.sh --up --down

# test a stack you already started (docker compose up -d):
bash scripts/ci/test-docker-stack.sh

# custom host port mappings? skip the host-port tests (t10-t12)
bash scripts/ci/test-docker-stack.sh --skip 't1[0-2]'

# validating the texlive :full image (propel:full):
bash scripts/ci/test-docker-stack.sh --latex
```

Exit code is 1 when any test failed (safe in scripts); every failure prints
its own diagnosed evidence (response bodies, migration poll attempts, crash
lines) — no hidden state. Container names and host ports are overridable via
env (`APP_CONTAINER`, `HOST_FE`, `HOST_GW`, `HOST_KC`, …) if you renamed them.

## 6. Developer data services

`docker-compose.data.yml` mirrors your current local defaults — Postgres on
5432 (`postgres/bime`, db `cv_monolith`), Keycloak on 9090, MinIO on 9000
(+ console 9001):

```bash
docker compose -f deploy/docker-compose.data.yml up -d
```

Do **not** run this at the same time as the friends' stack (they reuse the
same container names). Stop one before starting the other:

```bash
docker compose -f deploy/docker-compose.data.yml down
docker compose -f deploy/docker-compose.yml up -d    # and vice-versa
```

## 7. How the pieces talk

- All app processes live inside one container, so they use `localhost`
  (`backend :5000`, `gateway :8080`, `agents :8000`).
- Data services are separate containers reached by compose service names
  (env vars: `CONNECTION_STRING`, `MINIO_ENDPOINT`, `JWT_AUTHORITY`).
- JWT validation uses Keycloak's **internal** URL
  (`JWT_AUTHORITY=http://keycloak:8080/realms/cv-realm`); the browser-facing
  login always uses **localhost:9090** (`KEYCLOAK_EXTERNAL_*`). The
  gateway already had this split; the backend simply validates tokens against
  the internal authority, and the same host is used at code-exchange time, so
  issuers match.

## 8. Troubleshooting

```bash
docker compose -f deploy/docker-compose.yml logs -f app       # supervisor logs
docker logs propel-app --tail 100
docker exec -it propel-app tail -50 /var/log/supervisor/backend.err.log
docker exec -it propel-app tail -50 /var/log/supervisor/agents.err.log
```

LLM provider errors? An agent step that needs an LLM surfaces the error
inline; configure a key under Settings → AI Model and retry.

No LaTeX engine? The default image compiles cover-letter PDFs fine, but
LaTeX-based CV PDF generation degrades gracefully: the app shows an amber
"LaTeX engine unavailable" banner, .tex is still generated (compile it on
Overleaf), or build the `propel:full` image (`--build-arg WITH_LATEX=true`)
for offline PDF compilation.
