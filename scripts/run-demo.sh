#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export ASPNETCORE_ENVIRONMENT=Development
export DevAuth__Enabled=true
export AI__Provider=Mock
export ASPNETCORE_URLS=http://127.0.0.1:5080
if command -v dotnet >/dev/null 2>&1; then
  exec dotnet run --project src/BoltAI.Api --no-launch-profile --no-restore
elif [[ -x /workspace/tools/dotnet/dotnet ]]; then
  export DOTNET_CLI_HOME=/workspace/tools/dotnet-home
  export NUGET_PACKAGES=/workspace/tools/nuget
  exec /workspace/tools/dotnet/dotnet run --project src/BoltAI.Api --no-launch-profile --no-restore
else
  echo 'Install .NET SDK 10.0.401 or a later 10.0.4xx patch first.' >&2
  exit 1
fi
