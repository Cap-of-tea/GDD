<p align="center">
  <img src="Design/giggly-dazzling-duckling.png" alt="GDD Mascot" width="180" />
</p>

<h1 align="center">GDD — Giggly-Dazzling-Duckling</h1>

<p align="center">
  <strong>Cross-platform multi-browser testing tool</strong><br>
  Control N isolated Chromium instances via HTTP API, AI agents, or scripts
</p>

<p align="center">
  <a href="https://github.com/Cap-of-tea/GDD/releases/latest"><img src="https://img.shields.io/github/v/release/Cap-of-tea/GDD?style=flat-square&color=blue" alt="Release" /></a>
  <img src="https://img.shields.io/badge/.NET-8.0-purple?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6?style=flat-square" alt="Windows | Linux | macOS" />
  <img src="https://img.shields.io/badge/MCP-2024--11--05-green?style=flat-square" alt="MCP Protocol" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Non--Commercial-red?style=flat-square" alt="License" /></a>
</p>

---

## What is GDD?

GDD is a cross-platform multi-browser manager — run N isolated Chromium instances, each with its own profile, cookies, device emulation, geolocation, and network conditions.

GDD exposes 36 tools via HTTP API ([MCP protocol](https://modelcontextprotocol.io/) on `localhost:9700`). You can control browsers from:

- **AI agents** — Claude Code, Cursor, or any MCP-compatible client
- **Scripts & automation** — `curl`, Python, Node.js, or any HTTP client via JSON-RPC
- **CI/CD pipelines** — headless browser testing on any platform
- **Manual testing** — visible browser windows on any OS (default), or Windows GUI with video wall

**Three modes:**

| | Windows GUI | Headless | Headed |
| --- | --- | --- | --- |
| Binary | `GDD.exe` (BrowserXn) | `GDD.Headless --headless` | `GDD.Headless` |
| Browser engine | WebView2 | Playwright (Chromium) | Playwright (Chromium) |
| UI | WPF desktop with video wall | No UI — HTTP API only | Visible Chromium windows |
| Platforms | Windows only | Windows, Linux, macOS | Windows, Linux, macOS |
| Use case | Manual testing with live preview | CI/CD, scripted automation | Visual testing on any platform |
| MCP tools | 36 | 36 (identical) | 36 (identical) |

**Use cases:**

- Multi-device responsive testing (AI-driven or scripted)
- Multi-user scenario testing (chat, real-time apps, multiplayer)
- Cross-device visual regression checking
- Network condition testing (4G, 3G, offline)
- Geolocation and language-dependent feature testing
- CI/CD browser testing on any platform
- Manual multi-browser testing with visible windows on any platform

---

## Quick Start

### Windows GUI

1. Download `gdd-windows-gui.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run `GDD.exe` (self-contained, ~70 MB)
3. **Prerequisite:** [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (checked on startup)

### Windows (Headless)

1. Download `gdd-win-x64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:

   ```powershell
   .\GDD.Headless.exe
   # HTTP API starts on http://localhost:9700/mcp
   ```

### Linux

1. Download `gdd-linux-x64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:

   ```bash
   chmod +x GDD.Headless
   ./GDD.Headless
   ```

3. On Ubuntu/Debian you may need system libraries:

   ```bash
   sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1
   ```

### macOS — Apple Silicon (M1/M2/M3/M4)

1. Download `gdd-macos-arm64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:

   ```bash
   chmod +x GDD.Headless
   xattr -dr com.apple.quarantine . 2>/dev/null || true
   ./GDD.Headless
   ```

   > **First run:** GDD downloads Chromium (~80 MB). Run in foreground (not `&`) to see progress and errors. After Chromium is installed, you can run in background or via launchd.

   If Chromium auto-install fails (CDN timeouts), install manually using the bundled Playwright CLI:

   ```bash
   PLAYWRIGHT_BROWSERS_PATH="$(pwd)/.browsers" \
     ./.playwright/node/darwin-arm64/node \
     ./.playwright/package/cli.js install chromium
   xattr -dr com.apple.quarantine .browsers .playwright 2>/dev/null || true
   ```

### macOS — Intel

1. Download `gdd-macos-x64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:

   ```bash
   chmod +x GDD.Headless
   xattr -dr com.apple.quarantine . 2>/dev/null || true
   ./GDD.Headless
   ```

   > **First run:** GDD downloads Chromium (~80 MB). Run in foreground (not `&`) to see progress and errors. After Chromium is installed, you can run in background or via launchd.

   If Chromium auto-install fails, install manually:

   ```bash
   PLAYWRIGHT_BROWSERS_PATH="$(pwd)/.browsers" \
     ./.playwright/node/darwin-x64/node \
     ./.playwright/package/cli.js install chromium
   xattr -dr com.apple.quarantine .browsers .playwright 2>/dev/null || true
   ```

### Headed vs Headless Mode

By default, `GDD.Headless` launches in **headed** mode (visible Chromium windows). To run without UI:

```bash
./GDD.Headless --headless
```

All tools work identically in both modes. You can also set `"Headed": false` in `appsettings.json`.

### Connect

**Direct HTTP** — send JSON-RPC requests to GDD without any proxy or AI:

```bash
curl -X POST http://localhost:9700/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

**Claude Code / Cursor / MCP clients** — two connection methods:

Config file location:

| Client | Path |
| --- | --- |
| Claude Code (project) | `<project>/.mcp.json` |
| Claude Code (global) | `~/.claude/.mcp.json` |
| Cursor | `~/.cursor/mcp.json` |

> **Note:** MCP config is read at session start. After editing `.mcp.json`, restart Claude Code / Cursor for changes to take effect. Global and project configs are merged — servers from both are available simultaneously.

#### Option A: Direct URL (recommended)

Connect directly to GDD's HTTP endpoint. Requires GDD to be running (manually, via autostart, or as a service).

```json
{
  "mcpServers": {
    "gdd": {
      "url": "http://localhost:9700/mcp"
    }
  }
}
```

Instant connection, no intermediate proxy, no timeout issues. Works on all platforms.

#### Option B: stdio proxy (auto-launches GDD)

Proxy scripts start GDD automatically if it's not running and relay JSON-RPC via stdin/stdout.

Windows:

```json
{
  "mcpServers": {
    "gdd": {
      "command": "powershell",
      "args": ["-ExecutionPolicy", "Bypass", "-File", "C:/path/to/Scripts/mcp-proxy.ps1"]
    }
  }
}
```

Linux / macOS:

```json
{
  "mcpServers": {
    "gdd": {
      "command": "bash",
      "args": ["/path/to/Scripts/mcp-proxy.sh"]
    }
  }
}
```

For headless mode, add `"--headless"` to the `args` array.

> **Tip:** On first launch, GDD downloads Chromium (~80 MB) which can take time. If the MCP client times out waiting, run GDD manually first (`./GDD.Headless`), then restart the MCP session.

### macOS: Autostart via launchd

For persistent GDD on macOS, set up a launchd service that starts GDD at login and auto-restarts on crash:

```bash
cat > ~/Library/LaunchAgents/com.gdd.headless.plist << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.gdd.headless</string>
    <key>ProgramArguments</key>
    <array>
        <string>/path/to/GDD.Headless</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/path/to/gdd-directory</string>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/path/to/gdd-directory/logs/launchd-stdout.log</string>
    <key>StandardErrorPath</key>
    <string>/path/to/gdd-directory/logs/launchd-stderr.log</string>
</dict>
</plist>
PLIST

# Replace /path/to/ with your actual GDD directory, then:
mkdir -p /path/to/gdd-directory/logs
launchctl load ~/Library/LaunchAgents/com.gdd.headless.plist
```

Then use **Option A** (direct URL) in `.mcp.json` — no proxy needed.

Manage the service:

```bash
launchctl unload ~/Library/LaunchAgents/com.gdd.headless.plist  # stop
launchctl load ~/Library/LaunchAgents/com.gdd.headless.plist    # start
launchctl list | grep gdd                                        # status
```

### Linux: Autostart via systemd

```bash
mkdir -p ~/.config/systemd/user

cat > ~/.config/systemd/user/gdd.service << 'EOF'
[Unit]
Description=GDD Multi-Browser Testing Server

[Service]
ExecStart=/path/to/GDD.Headless
WorkingDirectory=/path/to/gdd-directory
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
EOF

# Replace /path/to/ with your actual GDD directory, then:
systemctl --user daemon-reload
systemctl --user enable --now gdd
systemctl --user status gdd   # check status
journalctl --user -u gdd -f   # view logs
```

### Use

**With AI:** Tell Claude — *"Open 3 phones and a desktop, navigate to my app, test the login flow on all devices"*

**Without AI:** Call any of the 36 tools via HTTP. Example — create a browser and navigate:

```bash
curl -X POST http://localhost:9700/mcp -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"gdd_add_players","arguments":{"count":1}}}'

curl -X POST http://localhost:9700/mcp -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"gdd_navigate","arguments":{"player_id":1,"url":"https://example.com"}}}'
```

---

## MCP Tools (36)

### Player Management

| Tool | Description |
| ------ | ------------- |
| `gdd_add_players` | Add N browser instances with optional device preset |
| `gdd_remove_player` | Remove a browser instance by player ID |
| `gdd_list_windows` | List all active browsers with current state |

### Navigation

| Tool | Description |
| ------ | ------------- |
| `gdd_navigate` | Navigate a browser to a URL |
| `gdd_wait` | Wait for a CSS selector to appear (with timeout) |
| `gdd_reload` | Reload current page (hard=true bypasses cache) |
| `gdd_back` | Navigate back in browser history |
| `gdd_forward` | Navigate forward in browser history |

### Interaction

| Tool | Description |
| ------ | ------------- |
| `gdd_tap` | Tap an element by CSS selector or coordinates |
| `gdd_swipe` | Simulate swipe gestures |
| `gdd_scroll` | Scroll page or specific element |
| `gdd_type` | Type text into input fields |
| `gdd_hover` | Hover over element (triggers mouseover/mouseenter) |
| `gdd_select` | Select option from `<select>` dropdown |
| `gdd_dialog` | Handle JS alert/confirm/prompt dialogs |

### Reading & Screenshots

| Tool | Description |
| ------ | ------------- |
| `gdd_read` | Read text content of an element |
| `gdd_read_all` | Read text from all matching elements |
| `gdd_screenshot` | Capture screenshot as JPEG at CSS pixel resolution |

### Emulation

| Tool | Description |
| ------ | ------------- |
| `gdd_set_device` | Set device preset (22 devices: phones, tablets, desktops) |
| `gdd_set_viewport` | Set custom viewport dimensions |
| `gdd_set_location` | Set geolocation, timezone, and locale |
| `gdd_set_network` | Set network conditions (4G, 3G, offline) |
| `gdd_set_language` | Set browser language |

### State & Diagnostics

| Tool | Description |
| ------ | ------------- |
| `gdd_get_state` | Get browser state: URL, title, device, auth status |
| `gdd_get_console` | Get console output and uncaught exceptions |
| `gdd_get_network` | Get network requests with timing and status |
| `gdd_get_notifications` | Get received push notifications |
| `gdd_get_performance` | Get performance metrics (JS heap, DOM nodes, FPS) |
| `gdd_clear_logs` | Clear console and/or network logs |

### Auth & Execution

| Tool | Description |
| ------ | ------------- |
| `gdd_quick_auth` | Auto-register and login with generated credentials |
| `gdd_execute_js` | Execute arbitrary JavaScript and return result |

### Browser Storage

| Tool | Description |
| ------ | ------------- |
| `gdd_storage` | Read/write/clear localStorage/sessionStorage |
| `gdd_cookies` | Read or clear browser cookies |

### Help

| Tool | Description |
| ------ | ------------- |
| `gdd_get_manual` | Returns the full GDD manual (M2M self-learning API) |

### Updates

| Tool | Description |
| ------ | ------------- |
| `gdd_check_update` | Check if a newer version of GDD is available |
| `gdd_update` | Download and install update (requires `confirm=true`, restarts GDD) |

---

## Device Presets

<details>
<summary><strong>Phones (11)</strong></summary>

| Device | Resolution | Scale | Touch |
| -------- | --------- | ----- | ----- |
| iPhone SE | 375 x 667 | 2.0x | Yes |
| iPhone 14 | 390 x 844 | 3.0x | Yes |
| iPhone 15 Pro | 393 x 852 | 3.0x | Yes |
| iPhone 15 Pro Max | 430 x 932 | 3.0x | Yes |
| iPhone 16 Pro | 402 x 874 | 3.0x | Yes |
| iPhone 16 Pro Max | 440 x 956 | 3.0x | Yes |
| Pixel 9 | 412 x 915 | 2.625x | Yes |
| Pixel 9 Pro | 412 x 915 | 2.625x | Yes |
| Galaxy S24 | 360 x 780 | 3.0x | Yes |
| Galaxy S24 Ultra | 412 x 915 | 3.0x | Yes |
| OnePlus 12 | 412 x 915 | 3.5x | Yes |

</details>

<details>
<summary><strong>Tablets (6)</strong></summary>

| Device | Resolution | Scale |
| -------- | --------- | ----- |
| iPad Mini | 744 x 1133 | 2.0x |
| iPad Air | 820 x 1180 | 2.0x |
| iPad Pro 11" | 834 x 1194 | 2.0x |
| iPad Pro 13" | 1024 x 1366 | 2.0x |
| Galaxy Tab S9 | 800 x 1280 | 2.0x |
| Pixel Tablet | 800 x 1280 | 2.0x |

</details>

<details>
<summary><strong>Desktops (5)</strong></summary>

| Device | Resolution | Scale |
| -------- | --------- | ----- |
| Laptop HD | 1366 x 768 | 1.0x |
| Laptop HiDPI | 1440 x 900 | 2.0x |
| Desktop 1080p | 1920 x 1080 | 1.0x |
| Desktop 1440p | 2560 x 1440 | 1.0x |
| Desktop 4K | 3840 x 2160 | 2.0x |

</details>

---

## Architecture

```text
┌──────────────────────────────────────────────────┐
│         Client (AI agent / curl / script)        │
│         POST /mcp  <->  JSON-RPC 2.0            │
└────────────────────┬─────────────────────────────┘
                     │ stdio proxy or direct HTTP
┌────────────────────▼─────────────────────────────┐
│              McpServer (HTTP :9700)               │
│   Transports: Streamable HTTP + SSE              │
└────────────────────┬─────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────┐
│           McpToolRegistry (36 tools)             │
│  Player · Navigation · Interaction · Read        │
│  Emulation · Auth · State · Diagnostics · Help   │
└────────────────────┬─────────────────────────────┘
                     │ IMainThreadDispatcher
┌────────────────────▼─────────────────────────────┐
│              IPlayerManager                      │
│   MainViewModel (WPF) / HeadlessPlayerManager    │
└────────────────────┬─────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────┐
│          IBrowserEngine Instances (Players)       │
│  WebView2ControlAdapter  |  PlaywrightEngine     │
│  (Windows GUI)           |  (Headless/Headed)    │
│    Each: own profile, CDP session, emulation     │
└──────────────────────────────────────────────────┘
```

### Project Structure

```text
BrowserXn.sln
├── src/
│   ├── GDD.Core/              ← Shared library (net8.0)
│   │   ├── Abstractions/      ← IBrowserEngine, IPlayerManager, ...
│   │   ├── Mcp/               ← MCP server, tools, protocol
│   │   ├── Models/            ← Device, Location, Network presets
│   │   ├── Services/          ← CDP, Emulation, Monitoring services
│   │   └── Collections/       ← RingBuffer
│   ├── BrowserXn/             ← Windows GUI (net8.0-windows, WPF)
│   │   ├── Engines/           ← WebView2ControlAdapter
│   │   ├── Platform/          ← WpfDispatcher, WebView2CdpSubscription
│   │   ├── ViewModels/        ← MVVM (MainViewModel : IPlayerManager)
│   │   ├── Views/             ← WPF XAML + VideoWallPanel
│   │   ├── Controls/          ← BrowserCellControl, OverlayWindow
│   │   ├── Converters/        ← XAML value converters
│   │   ├── Themes/            ← Dark theme styles
│   │   └── Interop/           ← Win32 P/Invoke (DWM, User32)
│   └── GDD.Headless/          ← Headless runner (net8.0, cross-platform)
│       ├── Engines/           ← PlaywrightEngine
│       ├── Platform/          ← ConsoleDispatcher, HeadlessPlayerManager
│       └── Scripts/           ← mcp-proxy.sh, mcp-proxy.ps1
└── .github/workflows/         ← CI/CD (build all platforms + release)
```

### Tech Stack

| Layer | Technology |
| ------- | --------- |
| Runtime | .NET 8.0 (self-contained) |
| Shared Core | GDD.Core (platform-independent) |
| UI (Windows) | WPF + XAML + CommunityToolkit.Mvvm |
| Browser (Windows GUI) | Microsoft WebView2 (Chromium) |
| Browser (Headless) | Microsoft Playwright (Chromium) |
| DI | Microsoft.Extensions.Hosting |
| Logging | Serilog (file + console) |
| Protocol | MCP (Model Context Protocol) |
| Browser Control | Chrome DevTools Protocol (CDP) |

---

## Configuration

`appsettings.json` next to the executable:

```json
{
  "GDD": {
    "FrontendUrl": "about:blank",
    "BackendUrl": "http://localhost:8080/api/v1",
    "BotToken": "",
    "McpPort": 9700,
    "DataFolderRoot": ""
  }
}
```

| Key | Description | Default |
| ----- | ------------- | ------- |
| `FrontendUrl` | Default URL for new browsers | `about:blank` |
| `BackendUrl` | Backend API for auth service | `http://localhost:8080/api/v1` |
| `BotToken` | Telegram bot token (for TG testing) | — |
| `McpPort` | MCP server port (auto-fallback +1..+9) | `9700` |
| `DataFolderRoot` | Browser profile storage root | `%LOCALAPPDATA%\GDD\Profiles` (Win), `~/.local/share/GDD/Profiles` (Linux/macOS) |
| `Headed` | Launch visible browser windows (headless only) | `true` (use `--headless` CLI flag to override) |

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Windows GUI

```powershell
dotnet build src/BrowserXn/BrowserXn.csproj
# Requires: Windows 10/11, WebView2 Runtime
```

Single-file publish:

```powershell
dotnet publish src/BrowserXn/BrowserXn.csproj -c Release -p:PublishSingleFile=true -o ./publish/win-gui
```

### Cross-platform (Windows, Linux, macOS)

```bash
dotnet build src/GDD.Headless/GDD.Headless.csproj

# Self-contained publish:
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r linux-x64 --self-contained -o ./publish/linux-x64
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r win-x64 --self-contained -o ./publish/win-x64

# Install Chromium manually (auto-installed on first run):
PLAYWRIGHT_BROWSERS_PATH=publish/linux-x64/.browsers \
  publish/linux-x64/.playwright/node/linux-x64/node \
  publish/linux-x64/.playwright/package/cli.js install chromium
```

---

## Documentation

- [GDD-MANUAL.md](GDD-MANUAL.md) — Full usage manual with agent rules and workflow examples
- [GDD-ARCHITECTURE.md](GDD-ARCHITECTURE.md) — Architecture deep-dive and cross-platform design
- [GDD-PROMPT.md](GDD-PROMPT.md) — Claude agent instructions for MCP integration

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

**imVS©, freeware for private use.**

Source Available — Non-Commercial. Free for personal use, education, and research. Commercial use, beta testing services, and use in commercial projects require a paid license. See [LICENSE](LICENSE) for full terms.

For commercial licensing inquiries: **[2vsmirnov@gmail.com](mailto:2vsmirnov@gmail.com)**

---

<p align="center">
  <sub>Built with .NET 8, WebView2, Playwright, and the Model Context Protocol</sub>
</p>
