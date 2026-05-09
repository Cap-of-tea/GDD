<p align="center">
  <img src="Design/giggly-dazzling-duckling.png" alt="GDD Mascot" width="180" />
</p>

<h1 align="center">GDD — Giggly-Dazzling-Duckling</h1>

<p align="center">
  <strong>Multi-browser testing tool powered by AI</strong><br>
  Control N isolated Chromium instances from Claude Code via 25 MCP tools
</p>

<p align="center">
  <a href="https://github.com/Cap-of-tea/GDD/releases/latest"><img src="https://img.shields.io/github/v/release/Cap-of-tea/GDD?style=flat-square&color=blue" alt="Release" /></a>
  <img src="https://img.shields.io/badge/.NET-8.0-purple?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?style=flat-square" alt="Windows" />
  <img src="https://img.shields.io/badge/MCP-2024--11--05-green?style=flat-square" alt="MCP Protocol" />
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Cap-of-tea/GDD?style=flat-square" alt="License" /></a>
</p>

---

## What is GDD?

GDD is a Windows desktop application that lets an AI agent (Claude Code) see and control multiple browser windows simultaneously — just like a human tester would, but faster and programmable.

Each browser is an isolated Chromium instance (WebView2) with its own profile, cookies, device emulation, geolocation, and network conditions. Claude connects via [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) and operates browsers through 25 specialized tools.

**Use cases:**
- Automated multi-device responsive testing
- Multi-user scenario testing (chat, real-time apps, multiplayer)
- Cross-device visual regression checking
- Network condition testing (4G, 3G, offline)
- Geolocation-dependent feature testing

---

## Quick Start

### 1. Download

Grab the latest `GDD.exe` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest) — it's a single self-contained executable (~70 MB), no .NET installation required.

> **Prerequisite:** [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) must be installed. GDD checks for it on startup and offers to install it if missing.

### 2. Launch

Double-click `GDD.exe`. The MCP server starts automatically on port `9700`.

### 3. Connect Claude Code

Add to your project's `.mcp.json`:

```json
{
  "mcpServers": {
    "gdd": {
      "command": "powershell",
      "args": ["-ExecutionPolicy", "Bypass", "-File", "path/to/mcp-proxy-auto.ps1"]
    }
  }
}
```

The proxy script auto-launches GDD if it's not running — no need to start it manually.

### 4. Use

Tell Claude:

> *"Open 3 phones and a desktop, navigate to my app, test the login flow on all devices"*

Claude will use GDD tools to add players, set devices, navigate, interact, and report results.

---

## MCP Tools (25)

### Player Management

| Tool | Description |
|------|-------------|
| `gdd_add_players` | Add N new browser instances with device presets |
| `gdd_remove_player` | Remove a browser instance by player ID |
| `gdd_list_windows` | List all active browsers with current state |

### Navigation

| Tool | Description |
|------|-------------|
| `gdd_navigate` | Navigate a browser to a URL |
| `gdd_wait` | Wait for a CSS selector to appear (with timeout) |

### Interaction

| Tool | Description |
|------|-------------|
| `gdd_tap` | Tap an element by CSS selector or coordinates |
| `gdd_swipe` | Simulate swipe gestures |
| `gdd_scroll` | Scroll page or specific element |
| `gdd_type` | Type text into input fields |

### Reading & Screenshots

| Tool | Description |
|------|-------------|
| `gdd_read` | Read text content of an element |
| `gdd_read_all` | Read text from all matching elements |
| `gdd_screenshot` | Capture screenshot as base64 PNG |

### Emulation

| Tool | Description |
|------|-------------|
| `gdd_set_device` | Set device preset (22 devices: phones, tablets, desktops) |
| `gdd_set_viewport` | Set custom viewport dimensions |
| `gdd_set_location` | Set geolocation, timezone, and locale |
| `gdd_set_network` | Set network conditions (4G, 3G, offline) |
| `gdd_set_language` | Set browser language |

### State & Diagnostics

| Tool | Description |
|------|-------------|
| `gdd_get_state` | Get browser state: URL, title, device, auth status |
| `gdd_get_console` | Get console output and uncaught exceptions |
| `gdd_get_network` | Get network requests with timing and status |
| `gdd_get_notifications` | Get received push notifications |
| `gdd_get_performance` | Get performance metrics (JS heap, DOM nodes, FPS) |
| `gdd_clear_logs` | Clear console and/or network logs |

### Auth & Execution

| Tool | Description |
|------|-------------|
| `gdd_quick_auth` | Auto-register and login with generated credentials |
| `gdd_execute_js` | Execute arbitrary JavaScript and return result |

### Help

| Tool | Description |
|------|-------------|
| `gdd_get_manual` | Returns the full GDD manual (M2M self-learning API) |

---

## Device Presets

<details>
<summary><strong>Phones (11)</strong></summary>

| Device | Resolution | Scale | Touch |
|--------|-----------|-------|-------|
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
|--------|-----------|-------|
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
|--------|-----------|-------|
| Laptop HD | 1366 x 768 | 1.0x |
| Laptop HiDPI | 1440 x 900 | 2.0x |
| Desktop 1080p | 1920 x 1080 | 1.0x |
| Desktop 1440p | 2560 x 1440 | 1.0x |
| Desktop 4K | 3840 x 2160 | 2.0x |

</details>

### Quick Presets

| Preset | Devices | Use Case |
|--------|---------|----------|
| 3 Phones | iPhone SE, iPhone 15 Pro, Pixel 9 | Quick mobile check |
| All Phones | All 11 phone devices | Full mobile coverage |
| Responsive | iPhone 15 Pro + iPad Air + Desktop 1080p | Responsive design |
| Cross-Platform | 6 devices across all categories | Cross-platform QA |
| All Screens | 10 devices covering all sizes | Full coverage |

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│            Claude Code (MCP Client)              │
│         POST /mcp  <->  JSON-RPC 2.0            │
└────────────────────┬─────────────────────────────┘
                     │ stdio proxy or direct HTTP
┌────────────────────▼─────────────────────────────┐
│              McpServer (HTTP :9700)               │
│   Transports: Streamable HTTP + SSE              │
└────────────────────┬─────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────┐
│           McpToolRegistry (25 tools)             │
│  Player · Navigation · Interaction · Read        │
│  Emulation · Auth · State · Diagnostics          │
└────────────────────┬─────────────────────────────┘
                     │ Dispatcher.InvokeAsync
┌────────────────────▼─────────────────────────────┐
│           MainViewModel (MVVM hub)               │
│    ObservableCollection<BrowserCellViewModel>    │
└────────────────────┬─────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────┐
│          WebView2 Instances (Players)            │
│    Each: own profile, CDP session, emulation     │
│    DWM thumbnails for video wall rendering       │
└──────────────────────────────────────────────────┘
```

### Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8.0 (self-contained) |
| UI | WPF + XAML |
| Browser | Microsoft WebView2 (Chromium) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.Hosting |
| Logging | Serilog (rolling file) |
| Protocol | MCP (Model Context Protocol) |
| Browser Control | Chrome DevTools Protocol (CDP) |
| Window Management | Win32 P/Invoke (DWM, User32) |

---

## Configuration

`appsettings.json` next to the executable:

```json
{
  "GDD": {
    "FrontendUrl": "http://localhost:5173",
    "BackendUrl": "http://localhost:8080/api/v1",
    "BotToken": "",
    "McpPort": 9700,
    "DataFolderRoot": ""
  }
}
```

| Key | Description | Default |
|-----|-------------|---------|
| `FrontendUrl` | Default URL for new browsers | `http://localhost:5173` |
| `BackendUrl` | Backend API for auth service | `http://localhost:8080/api/v1` |
| `BotToken` | Telegram bot token (for TG testing) | — |
| `McpPort` | MCP server port (auto-fallback +1..+9) | `9700` |
| `DataFolderRoot` | Browser profile storage root | `%LOCALAPPDATA%\GDD` |

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

### Debug Build

```powershell
dotnet build src/BrowserXn/BrowserXn.csproj
# Output: src/BrowserXn/bin/Debug/net8.0-windows/GDD.exe
```

### Publish Single-File EXE

```powershell
dotnet publish src/BrowserXn/BrowserXn.csproj -c Release -p:PublishSingleFile=true -o ./publish
# Output: publish/GDD.exe (~70 MB, self-contained, no .NET required)
```

---

## Documentation

- [GDD-MANUAL.md](GDD-MANUAL.md) — Full usage manual with agent rules and workflow examples
- [GDD-ARCHITECTURE.md](GDD-ARCHITECTURE.md) — Architecture deep-dive and cross-platform analysis
- [GDD-PROMPT.md](GDD-PROMPT.md) — Claude agent instructions for MCP integration

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

[MIT](LICENSE) — free for personal and commercial use.

---

<p align="center">
  <sub>Built with WPF, WebView2, and the Model Context Protocol</sub>
</p>
