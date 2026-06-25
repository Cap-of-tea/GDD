#!/bin/bash
set -euo pipefail

# Build .mcpb desktop extension bundles for Claude Desktop
# Called by CI after artifacts are downloaded.
#
# Usage: mcpb/build-mcpb.sh <version> <artifacts-dir> <icon-path> <output-dir>
# Example: mcpb/build-mcpb.sh 1.5.0 artifacts Design/gdd-logo-400.png dist

VERSION="$1"
ARTIFACTS_DIR="$(cd "$2" && pwd)"
ICON_PATH="$(realpath "$3")"
OUTPUT_DIR="$(mkdir -p "$4" && cd "$4" && pwd)"

build_bundle() {
  local RID="$1"
  local ARTIFACT_NAME="$2"
  local OUTPUT_NAME="$3"
  local ARTIFACT="$ARTIFACTS_DIR/$ARTIFACT_NAME"

  if [ ! -d "$ARTIFACT" ]; then
    echo "SKIP $RID: $ARTIFACT not found"
    return
  fi

  echo "---- Packaging $RID ----"
  local WORK
  WORK=$(mktemp -d)

  mkdir -p "$WORK/server"

  # Copy all server files including hidden (.playwright)
  cp -a "$ARTIFACT"/. "$WORK/server/"

  # Remove .browsers (auto-installed at first launch via PlaywrightSetup)
  rm -rf "$WORK/server/.browsers"
  rm -rf "$WORK/server/logs"
  rm -f  "$WORK/server/.gdd.pid"

  # Ensure scripts are executable (macOS)
  chmod +x "$WORK/server/Scripts/mcp-proxy.sh" 2>/dev/null || true
  chmod +x "$WORK/server/GDD.Headless" 2>/dev/null || true

  cp "$ICON_PATH" "$WORK/icon.png"

  # Generate platform-specific manifest
  local PLATFORMS MCP_COMMAND MCP_ARGS
  case "$RID" in
    win-x64)
      PLATFORMS='"win32"'
      MCP_COMMAND='powershell.exe'
      MCP_ARGS=', "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${__dirname}/server/Scripts/mcp-proxy.ps1"]'
      ;;
    osx-arm64|osx-x64)
      PLATFORMS='"darwin"'
      MCP_COMMAND='server/Scripts/mcp-proxy.sh'
      MCP_ARGS=''
      ;;
  esac

  cat > "$WORK/manifest.json" <<EOF
{
  "manifest_version": "0.3",
  "name": "gdd",
  "version": "$VERSION",
  "display_name": "GDD – Browser Automation",
  "description": "Browser automation MCP server with 37 tools: mobile device emulation, multi-player sessions, screenshots, DOM reading, network monitoring, and human-like input.",
  "author": {
    "name": "imVS",
    "url": "https://github.com/Cap-of-tea"
  },
  "server": {
    "type": "binary",
    "mcp_config": {
      "command": "$MCP_COMMAND"$MCP_ARGS
    }
  },
  "icon": "icon.png",
  "compatibility": {
    "platforms": [$PLATFORMS]
  },
  "documentation": "https://github.com/Cap-of-tea/GDD",
  "support": "https://github.com/Cap-of-tea/GDD/issues"
}
EOF

  local MCPB_FILE="$OUTPUT_DIR/GDD-${OUTPUT_NAME}.mcpb"
  (cd "$WORK" && zip -qr "$MCPB_FILE" . -x "*.DS_Store" "*.gitkeep")

  local SIZE
  SIZE=$(du -sh "$MCPB_FILE" | cut -f1)
  echo "OK $MCPB_FILE ($SIZE)"

  rm -rf "$WORK"
}

# Build bundles for Win + Mac (Claude Desktop platforms only)
# Args: RID, CI artifact name, output name
build_bundle "win-x64"   "GDD-Server-Windows"      "Server-Windows"
build_bundle "osx-arm64" "GDD-Server-macOS-ARM"    "Server-macOS-ARM"
build_bundle "osx-x64"   "GDD-Server-macOS-Intel"  "Server-macOS-Intel"

echo "Done. MCPB bundles in $OUTPUT_DIR"
