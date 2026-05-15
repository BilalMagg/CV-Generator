#!/bin/bash

# Exit on any error
set -e

echo "Starting SonarQube scan for .NET backend..."

# The network name should match the one defined in docker-compose.sonar.yml
NETWORK_NAME="cv-network"

# SonarQube token - you should ideally pass this as an environment variable or argument
SONAR_TOKEN=${SONAR_TOKEN:-"sqa_YOUR_TOKEN_HERE"}
SONAR_HOST=${SONAR_HOST:-"http://cv-sonarqube:9000"}

# Get the absolute path to the backend directory
BACKEND_DIR=$(pwd)
if [[ "$BACKEND_DIR" != *"/backend"* ]]; then
    BACKEND_DIR="$BACKEND_DIR/backend"
fi

# Organization key (required for SonarCloud)
SONAR_ORG=${SONAR_ORG:-""}

ORG_ARG=""
if [ -n "$SONAR_ORG" ]; then
    ORG_ARG="/o:$SONAR_ORG"
fi

echo "Running scan in directory: $BACKEND_DIR"

docker run --rm \
    --network $NETWORK_NAME \
    -e SONAR_TOKEN=$SONAR_TOKEN \
    -e SONAR_HOST=$SONAR_HOST \
    -e SONAR_ORG=$SONAR_ORG \
    -v "$BACKEND_DIR:/app" \
    -w /app \
    mcr.microsoft.com/dotnet/sdk:8.0 \
    bash -c "\
        apt-get update && apt-get install -y openjdk-17-jre && \
        dotnet tool install --global dotnet-sonarscanner && \
        export PATH=\"\$PATH:/root/.dotnet/tools\" && \
        dotnet sonarscanner begin /k:cv-generator-backend /d:sonar.host.url=\$SONAR_HOST /d:sonar.login=\$SONAR_TOKEN \$ORG_ARG && \
        dotnet build && \
        dotnet sonarscanner end /d:sonar.login=\$SONAR_TOKEN"

echo "Backend SonarQube scan completed."
