#!/usr/bin/env bash
# Run only the Home OS web app on http://localhost:5173 (installs deps if missing).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/frontend"
[ -d node_modules ] || npm install
exec npm run dev
