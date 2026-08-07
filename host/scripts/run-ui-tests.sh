#!/usr/bin/env bash
# Start API (optional) and run Microsoft Playwright UI tests.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
PORT="${PORT:-5080}"
START_API="${START_API:-1}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:${PORT}}"
export PLAYWRIGHT_BASE_URL="${PLAYWRIGHT_BASE_URL:-http://127.0.0.1:${PORT}}"
API_PID=""
cleanup() {
  if [[ -n "${API_PID}" ]] && kill -0 "${API_PID}" 2>/dev/null; then
    kill "${API_PID}" 2>/dev/null || true
    wait "${API_PID}" 2>/dev/null || true
  fi
}
trap cleanup EXIT
wait_for_url() {
  local url="$1" max="${2:-90}"
  echo "==> Waiting for ${url}"
  for ((i=1; i<=max; i++)); do
    if curl -sf -o /dev/null --max-time 2 "${url}" 2>/dev/null \
       || curl -sk -o /dev/null --max-time 2 "${url}" 2>/dev/null; then
      echo "    up after ${i}s"; return 0
    fi
    sleep 1
  done
  return 1
}
if [[ "${START_API}" == "1" ]]; then
  echo "==> Starting API: $ASPNETCORE_URLS"
  dotnet run --project host/PageToMovie.Api --no-launch-profile > /tmp/pagetomovie-api-ui.log 2>&1 &
  API_PID=$!
  wait_for_url "${PLAYWRIGHT_BASE_URL}" 90 || { tail -40 /tmp/pagetomovie-api-ui.log; exit 1; }
fi
FILTER_ARGS=()
[[ -n "${FILTER:-}" ]] && FILTER_ARGS=(--filter "${FILTER}")
dotnet test host/PageToMovie.UiTests/PageToMovie.UiTests.csproj --logger "console;verbosity=normal" "${FILTER_ARGS[@]}"
