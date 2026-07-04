#!/bin/bash
# Launch GDD.Headless (internal :9700) and the Caddy token proxy (public :$PORT).
# If either process exits, stop the container so Railway restarts it cleanly.
set -e

export GDD__BindAddress=0.0.0.0

echo "[entrypoint] starting GDD.Headless on 127.0.0.1:9700 ..."
dotnet /app/GDD.Headless.dll --headless &
GDD_PID=$!

echo "[entrypoint] starting Caddy proxy on :${PORT:-8080} ..."
caddy run --config /etc/caddy/Caddyfile --adapter caddyfile &
CADDY_PID=$!

# Exit as soon as either process dies (bash `wait -n`).
wait -n "$GDD_PID" "$CADDY_PID"
EXIT_CODE=$?
echo "[entrypoint] a process exited (code $EXIT_CODE); shutting down."
kill "$GDD_PID" "$CADDY_PID" 2>/dev/null || true
exit "$EXIT_CODE"
