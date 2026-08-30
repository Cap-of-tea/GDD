#!/bin/bash
# Launch GDD.Headless (internal :9700) and the Caddy token proxy (public :$PORT).
# If either process exits, stop the container so Railway restarts it cleanly.
set -e

# All interfaces by default: Caddy and GDD share this container, and the image is
# also used where nothing else can reach 9700. A deployment that puts GDD on
# loopback (compose on VSServer) presets the variable and it is kept.
export GDD__BindAddress="${GDD__BindAddress:-0.0.0.0}"

echo "[entrypoint] starting GDD.Headless on ${GDD__BindAddress}:9700 ..."
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
