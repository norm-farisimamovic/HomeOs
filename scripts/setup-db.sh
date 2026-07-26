#!/usr/bin/env bash
# One-time: create the 'homeos' database + user. Prompts for a MySQL admin password.
# Usage: scripts/setup-db.sh [admin-user]   (admin-user defaults to 'root')
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADMIN_USER="${1:-root}"
echo "Creating 'homeos' database + user via MySQL admin '$ADMIN_USER' (enter its password when asked)…"
mysql -u "$ADMIN_USER" -p < "$ROOT/scripts/setup-db.sql"
echo "✔ Database ready. /health/ready will now report the DB as Online."
