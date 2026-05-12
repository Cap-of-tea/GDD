#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GDD_EXE="$SCRIPT_DIR/GDD.Headless"
BASE_URL="http://localhost:9700/mcp"
GDD_PID=""
GDD_ARGS=""
for arg in "$@"; do
    case "$arg" in
        --headed) GDD_ARGS="$GDD_ARGS --headed" ;;
    esac
done

cleanup() {
    if [ -n "$GDD_PID" ] && kill -0 "$GDD_PID" 2>/dev/null; then
        kill "$GDD_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT

test_alive() {
    local probe='{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}'
    curl -sf -o /dev/null -X POST "$BASE_URL" \
        -H "Content-Type: application/json" \
        -d "$probe" 2>/dev/null
}

ensure_running() {
    if test_alive; then return; fi

    if [ ! -x "$GDD_EXE" ]; then
        echo "GDD.Headless not found at $GDD_EXE" >&2
        exit 1
    fi

    "$GDD_EXE" $GDD_ARGS &
    GDD_PID=$!

    for i in $(seq 1 20); do
        sleep 1
        if test_alive; then return; fi
    done

    echo "GDD MCP server did not respond after 20s" >&2
    exit 1
}

ensure_running

while IFS= read -r line; do
    [ -z "$line" ] && continue

    if echo "$line" | grep -q '"notifications/'; then
        curl -sf -o /dev/null -X POST "$BASE_URL" \
            -H "Content-Type: application/json" \
            -d "$line" 2>/dev/null || true
        continue
    fi

    response=$(curl -sf -X POST "$BASE_URL" \
        -H "Content-Type: application/json" \
        -d "$line" \
        --max-time 120 2>/dev/null) || {
        ensure_running
        response=$(curl -sf -X POST "$BASE_URL" \
            -H "Content-Type: application/json" \
            -d "$line" \
            --max-time 120 2>/dev/null) || {
            response="{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32000,\"message\":\"GDD server unreachable\"}}"
        }
    }
    echo "$response"
done
