#!/usr/bin/env bash
# Start PageToMovie.Api (serves UI) on a fixed port.
# Usage: ./host/scripts/run-api-ui.sh
#        PORT=5090 ./host/scripts/run-api-ui.sh
#        HTTPS=1 ./host/scripts/run-api-ui.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
PORT="${PORT:-5080}"
HTTPS_PORT="${HTTPS_PORT:-7123}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
if [[ -z "${ASPNETCORE_URLS:-}" ]]; then
  if [[ "${HTTPS:-0}" == "1" ]]; then
    export ASPNETCORE_URLS="http://127.0.0.1:${PORT};https://localhost:${HTTPS_PORT}"
  else
    export ASPNETCORE_URLS="http://127.0.0.1:${PORT}"
  fi
fi
echo "==> PageToMovie.Api  ASPNETCORE_URLS=$ASPNETCORE_URLS"
echo "    export PLAYWRIGHT_BASE_URL=http://127.0.0.1:${PORT}"
exec dotnet run --project host/PageToMovie.Api --no-launch-profile
