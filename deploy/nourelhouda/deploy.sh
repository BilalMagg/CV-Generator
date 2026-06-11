#!/usr/bin/env bash
set -euo pipefail

if ! command -v docker &>/dev/null; then
  curl -fsSL https://get.docker.com | sh
fi

sudo docker compose up -d
