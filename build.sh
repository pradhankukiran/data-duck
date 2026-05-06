#!/usr/bin/env bash
# Vercel build script. Installs .NET 10 SDK + wasm-tools workload,
# runs dotnet publish, and produces static output in publish/wwwroot/.
set -euo pipefail

DOTNET_INSTALL_DIR="$HOME/.dotnet"
export PATH="$DOTNET_INSTALL_DIR:$PATH"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "::: Installing .NET 10 SDK to $DOTNET_INSTALL_DIR :::"
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_INSTALL_DIR"

echo "::: dotnet --version :::"
dotnet --version

echo "::: Installing wasm-tools workload :::"
dotnet workload install wasm-tools

echo "::: npm install (for @duckdb/duckdb-wasm bundle) :::"
npm ci 2>/dev/null || npm install

echo "::: dotnet publish (Browser, Release) :::"
dotnet publish DataDuck.Browser/DataDuck.Browser.csproj -c Release -o publish

echo "::: Build done. Static output is in publish/wwwroot/ :::"
ls publish/wwwroot | head -10
