# =============================================================================
# Propel — single application image (backend + gateway + AI agents + frontend)
# =============================================================================
# Data services (Postgres, Keycloak, MinIO) come from official images via
# docker-compose. The texlive-less default image degrades gracefully (see
# Phase 1 capability plumbing); build with --build-arg WITH_LATEX=true for a
# texlive-bundled variant when PDF compilation must work offline.
#
#   docker build -f deploy/Dockerfile -t propel:latest --context ..
#   docker build -f deploy/Dockerfile -t propel:full --context .. --build-arg WITH_LATEX=true
# =============================================================================

ARG RUNTIME_BASE=mcr.microsoft.com/dotnet/aspnet:10.0-noble

# ─── Stage 1: frontend build (Angular → static files) ────────────────────────
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
# BuildKit cache mount: reuses the npm HTTP cache across sibling stages and
# same-machine rebuilds (GitHub runners rely on the layer cache instead).
RUN --mount=type=cache,id=propel-npm,target=/root/.npm npm ci --no-audit --no-fund --cache /root/.npm
COPY frontend/ .
RUN npx ng build

# ─── Stage 2: python agents (a venv on the same base as the runtime) ─────────
# pip install prefers deploy/wheelhouse/ (manylinux wheels, generated locally
# with the command in deploy/README.md) when it contains wheels — this makes
# the build offline/resumable. Otherwise it downloads from PyPI.
FROM ${RUNTIME_BASE} AS agents
RUN apt-get update && apt-get install -y --no-install-recommends \
        python3.12 python3.12-venv \
    && rm -rf /var/lib/apt/lists/*
RUN python3.12 -m venv /opt/venv
ENV PATH="/opt/venv/bin:${PATH}"
WORKDIR /src/agents
COPY deploy/wheelhouse/ /wh/
COPY monolith/agents/pyproject.toml ./
COPY monolith/agents/shared/ ./shared/
COPY monolith/agents/agents/ ./agents/
COPY monolith/agents/main.py ./
# pip cache mount: faster re-installs (wheelhouse path ignores it; the PyPI
# path keeps downloaded wheels in /root/.cache/pip)
RUN --mount=type=cache,id=propel-pip,target=/root/.cache/pip \
    if ls /wh/*.whl >/dev/null 2>&1; then \
        echo ">> installing from wheelhouse (/wh)"; \
        pip install --no-index --find-links=/wh --upgrade pip setuptools wheel hatchling; \
        pip install --no-index --find-links=/wh --no-build-isolation .; \
    else \
        echo ">> installing from PyPI"; \
        pip install --upgrade pip setuptools wheel hatchling; \
        pip install .; \
    fi

# ─── Stage 3: .NET backend publish ────────────────────────────────────────────
# ─── Stage 3: .NET publishes (backend + gateway) ─────────────────────────────
# ONE stage publishing both projects in a single RUN: BuildKit runs stages in
# PARALLEL and two concurrent `dotnet restore`s sharing the same cache mount
# corrupt each other's download temp files (seen on CI: gateway restore died on
# a temp file the backend stage deleted). Sequential in one stage = shared
# cache is safe (gateway reuses every package the backend restored).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet
WORKDIR /src
COPY monolith/backend/ ./monolith/backend/
COPY monolith/gateway/ ./monolith/gateway/
RUN --mount=type=cache,id=propel-nuget,target=/root/.nuget/packages \
    dotnet publish monolith/backend/CV_Generator.csproj -c Release -o /publish/backend ; \
    dotnet publish monolith/gateway/ApiGateway.csproj -c Release -o /publish/gateway

# ─── Stage 5: runtime ────────────────────────────────────────────────────────
FROM ${RUNTIME_BASE}
ARG WITH_LATEX=false

RUN apt-get update && apt-get install -y --no-install-recommends \
        supervisor nginx curl ca-certificates \
        python3.12 python3.12-venv \
        libpango-1.0-0 libpangocairo-1.0-0 libgdk-pixbuf-2.0-0 libffi8 \
        fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

# texlive is opt-in: the default install has no LaTeX engine and the app keeps
# working (tex-only degradation, compile on Overleaf).
RUN if [ "$WITH_LATEX" = "true" ]; then \
        apt-get update \
        && apt-get install -y --no-install-recommends texlive-latex-base texlive-latex-recommended texlive-fonts-recommended texlive-latex-extra \
        && rm -rf /var/lib/apt/lists/* ; \
    fi

# .NET apps
COPY --from=dotnet /publish/backend /app/backend
COPY --from=dotnet /publish/gateway /app/gateway

# Python agents (venv includes its interpreter binaries)
COPY --from=agents /opt/venv /opt/venv
COPY --from=agents /src/agents /app/agents

# Frontend static files served by nginx
RUN rm -rf /usr/share/nginx/html/*
COPY --from=frontend /src/frontend/dist/frontend/browser/ /usr/share/nginx/html/

COPY deploy/supervisord.conf /etc/supervisor/supervisord.conf
COPY deploy/nginx.conf /etc/nginx/sites-available/propel.conf

RUN ln -sf /etc/nginx/sites-available/propel.conf /etc/nginx/sites-enabled/propel.conf \
    && rm -f /etc/nginx/sites-enabled/default \
    && mkdir -p /var/log/supervisor /var/log/nginx

EXPOSE 4200 8080

ENTRYPOINT ["/usr/bin/supervisord", "-n", "-c", "/etc/supervisor/supervisord.conf"]
