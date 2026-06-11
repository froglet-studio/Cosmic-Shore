#!/usr/bin/env bash
# Fresh-container bootstrap for the Cosmic Shore port loop (see PORT_PLAN.md).
set -e
if ! command -v dotnet >/dev/null 2>&1 && [ ! -x /opt/dotnet/dotnet ]; then
  echo "installing .NET SDK (LTS)..."
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel LTS --install-dir /opt/dotnet
  grep -q '/opt/dotnet' ~/.bashrc || echo 'export PATH=/opt/dotnet:$PATH' >> ~/.bashrc
fi
export PATH=/opt/dotnet:$PATH
echo "installing headless GL (screenshot verification)..."
apt-get install -y --no-install-recommends xvfb libgl1 libglx-mesa0 libgl1-mesa-dri libglfw3 >/dev/null 2>&1 || true
cd "$(dirname "$0")"
dotnet build && dotnet test
echo "Port toolchain ready — read PORT_PLAN.md 'NEXT UP' and continue the loop."
