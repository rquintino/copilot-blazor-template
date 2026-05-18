#!/usr/bin/env bash
set -euo pipefail

echo "=== Installing .NET tools ==="
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef

echo "=== Installing Playwright CLI ==="
npm install -g @playwright/cli@latest --minimum-release-age=0
playwright-cli install || true
playwright-cli install --skills || true

echo "=== Installing Playwright browsers ==="
npx playwright install --with-deps chromium

echo "=== Restoring .NET packages ==="
dotnet restore

echo "=== Build ==="
dotnet build

echo "=== Setup complete ==="
dotnet --version
node --version
npm --version
