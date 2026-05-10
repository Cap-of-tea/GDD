<p align="center">
  <img src="Design/giggly-dazzling-duckling.png" alt="GDD Mascot" width="180" />
</p>

<h1 align="center">GDD — Giggly-Dazzling-Duckling</h1>

<p align="center">
  <strong>Cross-platform multi-browser testing tool powered by AI</strong><br>
  Control N isolated Chromium instances from Claude Code via 34 MCP tools
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

GDD is a cross-platform tool that lets an AI agent (Claude Code) see and control multiple browser windows simultaneously — just like a human tester would, but faster and programmable.

Each browser is an isolated Chromium instance with its own profile, cookies, device emulation, geolocation, and network conditions. Claude connects via [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) and operates browsers through 34 specialized tools.

**Two modes:**

| | Windows GUI | Headless (Windows, Linux, macOS) |
|---|---|---|
| Browser engine | WebView2 | Playwright (Chromium) |
| UI | WPF desktop with video wall | No UI — fully controlled via MCP |
| Use case | Visual testing with live preview | CI/CD, servers, cross-platform |
| MCP tools | 34 | 34 (identical) |

**Use cases:**
- Automated multi-device responsive testing
- Multi-user scenario testing (chat, real-time apps, multiplayer)
- Cross-device visual regression checking
- Network condition testing (4G, 3G, offline)
- Geolocation-dependent feature testing
- CI/CD browser testing on Linux/macOS

---

## Quick Start

### Windows GUI

1. Download `gdd-windows-gui.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run `GDD.exe` (self-contained, ~70 MB)
3. **Prerequisite:** [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (checked on startup)

### Headless (Windows)

1. Download `gdd-headless-win-x64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:
   ```powershell
   .\GDD.Headless.exe
   # MCP server starts on http://localhost:9700/mcp
   ```
3. First run installs Chromium automatically via Playwright

### Headless (Linux)

1. Download `gdd-headless-linux-x64.tar.gz` from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:
   ```bash
   chmod +x GDD.Headless
   ./GDD.Headless
   ```
3. First run installs Chromium. On Ubuntu/Debian you may need:
   ```bash
   sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1
   ```

### Headless (macOS)

1. Download `gdd-headless-macos-arm64.tar.gz` (Apple Silicon) or `gdd-headless-macos-x64.tar.gz` (Intel) from [Releases](https://github.com/Cap-of-tea/GDD/releases/latest)
2. Extract and run:
   ```bash
   chmod +x GDD.Headless
   ./GDD.Headless
   ```

### Connect Claude Code

Add to your project's `.mcp.json`:

**Windows (GUI with auto-launch):**
```json
{
  "mcpServers": {
    "gdd": {
      "command": "powershell",
      "args": ["-ExecutionPolicy", "Bypass", "-File", "path/to/Scripts/mcp-proxy.ps1"]
    }
  }
}
```

**Windows (Headless with auto-launch):**
```json
{
  "mcpServers": {
    "gdd": {
      "command": "powershell",
      "args": ["-ExecutionPolicy", "Bypass", "-File", "path/to/Scripts/mcp-proxy.ps1"]
    }
  }
}
```

**Linux / macOS (Headless with auto-launch):**
```json
{
  "mcpServers": {
    "gdd": {
      "command": "bash",
      "args": ["path/to/Scripts/mcp-proxy.sh"]
    }
  }
}
```

The proxy scripts auto-launch GDD if it's not running — no need to start it manually.

### Use

Tell Claude:

> *"Open 3 phones and a desktop, navigate to my app, test the login flow on all devices"*

Claude will use GDD tools to add players, set devices, navigate, interact, and report results.

---

## MCP Tools (34)

### Player Management

| Tool | Description |
|------|-------------|
| `gdd_add_players` | Add N browser instances with optional device preset |
| `gdd_remove_player` | Remove a browser instance by player ID |
| `gdd_list_windows` | List all active browsers with current state |

### Navigation

| Tool | Description |
|------|-------------|
| `gdd_navigate` | Navigate a browser to a URL |
| `gdd_wait` | Wait for a CSS selector to appear (with timeout) |
| `gdd_reload` | Reload current page (hard=true bypasses cache) |
| `gdd_back` | Navigate back in browser history |
| `gdd_forward` | Navigate forward in browser history |

### Interaction

| Tool | Description |
|------|-------------|
| `gdd_tap` | Tap an element by CSS selector or coordinates |
| `gdd_swipe` | Simulate swipe gestures |
| `gdd_scroll` | Scroll page or specific element |
| `gdd_type` | Type text into input fields |
| `gdd_hover` | Hover over element (triggers mouseover/mouseenter) |
| `gdd_select` | Select option from `<select>` dropdown |
| `gdd_dialog` | Handle JS alert/confirm/prompt dialogs |

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

### Browser Storage

| Tool | Description |
|------|-------------|
| `gdd_storage` | Read/write/clear localStorage/sessionStorage |
| `gdd_cookies` | Read or clear browser cookies |

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
│           McpToolRegistry (34 tools)             │
│  Player · Navigation · Interaction · Read        │
│  Emulation · Auth · State · Diagnostics · Help   │
└────────────────────┬─────────────────────────────┘
                     │ IMainThreadDispatcher
┌────────────────────▼─────────────────────────────┐
│              IPlayerManager                      │
│      MainViewModel (WPF) / HeadlessManager       │
└────────────────────┬─────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────┐
│          IBrowserEngine Instances (Players)       │
│   WebView2ControlAdapter  |  PlaywrightEngine    │
│   (Windows GUI)           |  (Headless, x-plat)  │
│    Each: own profile, CDP session, emulation     │
└──────────────────────────────────────────────────┘
```

### Project Structure

```
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
│   │   ├── Views/             ← WPF XAML
│   │   └── Interop/           ← Win32 P/Invoke
│   └── GDD.Headless/          ← Headless runner (net8.0, cross-platform)
│       ├── Engines/           ← PlaywrightEngine
│       ├── Platform/          ← ConsoleDispatcher, HeadlessPlayerManager
│       └── Scripts/           ← mcp-proxy.sh, mcp-proxy.ps1
└── .github/workflows/         ← CI/CD (build all platforms + release)
```

### Tech Stack

| Layer | Technology |
|-------|-----------|
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
| `FrontendUrl` | Default URL for new browsers | `about:blank` |
| `BackendUrl` | Backend API for auth service | `http://localhost:8080/api/v1` |
| `BotToken` | Telegram bot token (for TG testing) | — |
| `McpPort` | MCP server port (auto-fallback +1..+9) | `9700` |
| `DataFolderRoot` | Browser profile storage root | OS-specific app data dir |

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

### Headless (any platform)

```bash
dotnet build src/GDD.Headless/GDD.Headless.csproj

# Self-contained publish:
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r linux-x64 --self-contained -o ./publish/linux-x64
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -r win-x64 --self-contained -o ./publish/win-x64

# Install Chromium (first run):
pwsh publish/linux-x64/playwright.ps1 install chromium
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

**Source Available — Non-Commercial.** Free for personal use, education, and research. Commercial use, beta testing services, and use in commercial projects require a paid license. See [LICENSE](LICENSE) for full terms.

For commercial licensing inquiries: **[2vsmirnov@gmail.com](mailto:2vsmirnov@gmail.com)**

---

<p align="center">
  <sub>Built with .NET 8, WebView2, Playwright, and the Model Context Protocol</sub>
</p>
