#!/usr/bin/env bash
# Run the Home OS API (:5080) and web (:5173) together. Ctrl+C stops both.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cleanup() { echo; echo "Stopping Home OS…"; kill 0; }
trap cleanup EXIT INT TERM

echo "▶ API  → http://localhost:5080  (Swagger: /swagger)"
( cd "$ROOT/backend/src/HomeOs.Api" && dotnet run ) &

echo "▶ Web  → http://localhost:5173"
( cd "$ROOT/frontend" && [ -d node_modules ] || npm install; npm run dev ) &

echo "Both starting… open http://localhost:5173 (Ctrl+C to stop)"
wait
