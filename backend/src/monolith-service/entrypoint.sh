#!/bin/sh
set -e

# Run migrations using the SDK from the build stage
echo "Running database migrations..."
dotnet ef database update --no-build

# Start the application
echo "Starting application..."
exec dotnet MonolithService.dll