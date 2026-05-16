#!/usr/bin/env bash
set -euo pipefail

COMPOSE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$COMPOSE_DIR"

DBS=(
  "user-db"
  "content-db"
  "workflow-db"
  "application-db"
  "job-offer-db"
  "notification-db"
  "cv-db"
)

PORTS=(5433 5434 5435 5436 5437 5438 5439)
CONTAINERS=("cv-user-db" "cv-content-db" "cv-workflow-db" "cv-application-db" "cv-job-offer-db" "cv-notification-db" "cv-cv-db")

echo ""
echo "Available databases:"
echo "===================="
for i in "${!DBS[@]}"; do
  n=$((i + 1))
  running="STOPPED"
  if docker ps --format "{{.Names}}" 2>/dev/null | grep -q "${CONTAINERS[$i]}"; then
    running="RUNNING"
  fi
  printf "  %2d. %-18s  host:%-5s  [%s]\n" "$n" "${DBS[$i]}" "${PORTS[$i]}" "$running"
done

echo ""
echo -n "Enter numbers separated by space (e.g. 1 3 5): "
read -ra SELECTED

if [ ${#SELECTED[@]} -eq 0 ]; then
  echo "No selection. Exiting."
  exit 0
fi

SERVICES=""
for num in "${SELECTED[@]}"; do
  idx=$((num - 1))
  if [ "$idx" -ge 0 ] && [ "$idx" -lt "${#DBS[@]}" ]; then
    SERVICES="$SERVICES ${DBS[$idx]}"
  else
    echo "  Invalid number: $num (ignored)"
  fi
done

if [ -z "$SERVICES" ]; then
  echo "No valid selections. Exiting."
  exit 0
fi

echo ""
echo "Starting:$SERVICES"
echo ""

docker compose up -d $SERVICES

echo ""
echo "Done."
