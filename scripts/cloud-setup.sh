#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME=/workspace/tools/dotnet-home
export NUGET_PACKAGES=/workspace/tools/nuget
export DOTNET_CLI_TELEMETRY_OPTOUT=1
sdk=/workspace/tools/dotnet/dotnet
if [[ ! -x "$sdk" ]] || [[ "$($sdk --version)" != '10.0.401' ]]; then
  curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh --output /tmp/bolt-dotnet-install.sh
  bash /tmp/bolt-dotnet-install.sh --version 10.0.401 --install-dir /workspace/tools/dotnet --no-path
fi
"$sdk" restore --locked-mode
"$sdk" build --no-restore
"$sdk" test --no-build --no-restore
