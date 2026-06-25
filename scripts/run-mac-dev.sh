#!/usr/bin/env bash
# Dev launcher for the native macOS app: builds the .NET engine + the SwiftUI app,
# wires the app to the freshly built engine via env vars, and runs it.
#
# Usage:  scripts/run-mac-dev.sh [--selftest]
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_HOME="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_ROOT="$DOTNET_HOME"
export PATH="$DOTNET_HOME:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "==> Building .NET engine (Release)"
dotnet build "$REPO/src/StatStudio.Engine/StatStudio.Engine.csproj" -c Release | tail -3

echo "==> Building SwiftUI app"
( cd "$REPO/mac/StatStudio" && swift build )

export STATSTUDIO_ENGINE_DLL="$REPO/src/StatStudio.Engine/bin/Release/net10.0/StatStudio.Engine.dll"
export DOTNET="$DOTNET_HOME/dotnet"

APP_BIN="$REPO/mac/StatStudio/.build/debug/StatStudio"
echo "==> Launching $APP_BIN"
exec "$APP_BIN" "$@"
