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
    --platform manylinux2014_x86_64 --implementation cp .
```

(`deploy/wheelhouse/` is git-ignored; the build falls back to PyPI when
it's empty.)

## 4. Developer data services

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

## 5. How the pieces talk

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

## 6. Troubleshooting

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
