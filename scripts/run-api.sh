#!/usr/bin/env bash
# Run only the Home OS API on http://localhost:5080.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/backend/src/HomeOs.Api"
exec dotnet run
