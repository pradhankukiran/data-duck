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

# Vercel's build image lacks libatomic.so.1, which the .NET-bundled emscripten Node
# is dynamically linked against. Swap the bundled Node binary for Vercel's system
# Node (which ships glibc-compatible). Local builds skip this branch since the
# bundled Node works on a normal Linux dev box.
BUNDLED_NODE="$DOTNET_INSTALL_DIR/packs/Microsoft.NET.Runtime.Emscripten.3.1.56.Node.linux-x64/10.0.7/tools/bin/node"
if [ -f "$BUNDLED_NODE" ] && ! "$BUNDLED_NODE" --version >/dev/null 2>&1; then
    SYSTEM_NODE="$(command -v node || true)"
    if [ -n "$SYSTEM_NODE" ]; then
        echo "::: Bundled emscripten Node failed. Replacing with $SYSTEM_NODE ($(node --version)) :::"
        rm -f "$BUNDLED_NODE"
        ln -sf "$SYSTEM_NODE" "$BUNDLED_NODE"
    fi
fi

echo "::: npm install (for @duckdb/duckdb-wasm bundle) :::"
npm ci 2>/dev/null || npm install

echo "::: dotnet publish (Browser, Release) :::"
dotnet publish DataDuck.Browser/DataDuck.Browser.csproj -c Release -o publish

echo "::: Build done. Static output is in publish/wwwroot/ :::"
ls publish/wwwroot | head -10
