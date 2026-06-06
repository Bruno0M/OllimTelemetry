#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

LOG_DIR="/tmp/ollim-dev/ollim"
mkdir -p "$LOG_DIR"
LOG_FILE="$LOG_DIR/daemon.log"

# Kill any existing dev daemon
if [ -f "$LOG_DIR/daemon.pid" ]; then
  OLD_PID=$(cat "$LOG_DIR/daemon.pid")
  kill "$OLD_PID" 2>/dev/null && echo "Stopped previous daemon (PID $OLD_PID)" || true
  rm -f "$LOG_DIR/daemon.pid"
fi

dotnet run --project "$ROOT_DIR/src/OllimTelemetry.Cli" -- --run-daemon >> "$LOG_FILE" 2>&1 &
echo $! > "$LOG_DIR/daemon.pid"
echo "Daemon started (PID $(cat "$LOG_DIR/daemon.pid")) — log: $LOG_FILE"

tail -f "$LOG_FILE"
