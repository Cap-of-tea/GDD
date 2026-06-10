# GDD — Architecture

## 1. What is GDD

GDD (Giggly-Dazzling-Duckling) — a cross-platform application for multi-browser testing of web applications. Manages N isolated Chromium instances ("players") via Chrome DevTools Protocol and provides 37 MCP tools for automation through Claude Code or any MCP client.

Three modes: **Windows GUI** (WPF + WebView2), **Headed** (Playwright, visible Chromium windows, default on Windows/Linux/macOS), and **Headless** (`--headless` — no UI, for CI/CD). The shared GDD.Core library contains all services and MCP tools, operating through `IBrowserEngine` and `IPlayerManager` abstractions.

**Key idea:** a single AI agent (Claude) sees and controls multiple browsers simultaneously — navigation, clicks, screenshots, device/network/geolocation emulation, console and network request monitoring.

---

## 2. Tech Stack

| Layer | Technology | Version |
| ----- | ---------- | ------- |
| Runtime | .NET 8.0 (GDD.Core, GDD.Headless), .NET 8.0-windows (BrowserXn) | net8.0 / net8.0-windows TFM |
| UI Framework | WPF (Windows GUI only) | built-in |
| Browser (Windows GUI) | Microsoft WebView2 | 1.0.2739.15 |
| Browser (Headless/Headed) | Microsoft Playwright (Chromium) | 1.49.0 |
| MVVM | CommunityToolkit.Mvvm | 8.3.2 |
| DI / Hosting | Microsoft.Extensions.Hosting | 8.0.1 |
| HTTP Client | Microsoft.Extensions.Http | 8.0.1 |
| Logging | Serilog (File + Console sinks) | 8.0.0 |
| Protocol | MCP (Model Context Protocol) via HTTP | 2024-11-05 |
| Browser Control | Chrome DevTools Protocol (CDP) | via WebView2 / Playwright CDPSession |
| Window Mgmt | Win32 P/Invoke (DWM, User32) | Windows GUI only |

---

## 3. Architecture

```text
┌──────────────────────────────────────────────────────┐
│       Client (AI agent / curl / script / MCP)        │
│              POST /mcp  ←→  JSON-RPC 2.0             │
└─────────────────────┬────────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────────┐
│                  McpServer (HTTP)                     │
│  Transports: Streamable HTTP (/mcp) + SSE (/sse)     │
│  Port: 9700 (auto-fallback 9700..9709)               │
└─────────────────────┬────────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────────┐
│              McpToolRegistry (37 tools)               │
│  PlayerTools · NavigationTools · InteractionTools     │
│  ReadTools · ExecutionTools · EmulationTools          │
│  AuthTools · StateTools · DiagnosticsTools · HelpTools│
└─────────────────────┬────────────────────────────────┘
                      │  Dispatcher.InvokeAsync
┌─────────────────────▼────────────────────────────────┐
│           IPlayerManager (abstraction)                │
│  MainViewModel (WPF) / HeadlessPlayerManager (CLI)    │
│  Orchestrates all services                            │
└───┬─────────┬──────────┬────────────┬────────────────┘
    │         │          │            │
    ▼         ▼          ▼            ▼
┌────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────┐
│IBrowser│ │Emulation │ │   Auth   │ │  Diagnostics  │
│Engine  │ │Services  │ │Services  │ │  Services     │
│WV2 or  │ │Device    │ │QuickAuth │ │Console        │
│Playwr. │ │Location  │ │TokenInj. │ │Network        │
│        │ │Network   │ │Telegram  │ │Performance    │
│        │ │Language  │ │          │ │Notifications  │
└────┬───┘ └────┬─────┘ └────┬─────┘ └───────┬───────┘
     │          │            │                │
     └──────────┴────────────┴────────────────┘
                      │
         Chrome DevTools Protocol (CDP)
         IBrowserEngine.CallCdpMethodAsync()
```

### Startup Flow

```text
App.OnStartup()
  → Host.CreateDefaultBuilder()
  → LoadConfig("appsettings.json") → AppConfig
  → ConfigureSerilog()
  → RegisterServices(DI container)
  → RegisterMcpTools() — 37 tools → McpToolRegistry
  → StartMcpServer() — HttpListener on :9700
  → new MainWindow { DataContext = MainViewModel }
```

### Request Flow (MCP tool call)

```text
Client → POST /mcp {"method":"tools/call","params":{"name":"gdd_navigate",...}}
  → McpServer.HandleStreamableHttp()
  → McpSessionContext.CurrentSessionId = sessionId  (AsyncLocal)
  → McpToolRegistry.InvokeAsync("gdd_navigate", args)
  → Dispatcher.InvokeAsync (→ UI thread or direct)
  → IPlayerManager.GetPlayer(id)  (filters by session ownership)
  → player.Engine.NavigateAsync(url)
  → WebView2.Navigate() or Playwright.GotoAsync()
  → JSON-RPC response → Client
```

---

## 4. Project Structure

```text
BrowserXn.sln
├── src/
│   ├── GDD.Core/                          ← Shared library (net8.0)
│   │   ├── GddVersion.cs                    Version constant (e.g. "1.4.1")
│   │   ├── Abstractions/
│   │   │   ├── IBrowserEngine.cs              Browser engine interface
│   │   │   ├── IBrowserEngineFactory.cs       Factory interface
│   │   │   ├── ICdpEventSubscription.cs       CDP event subscription abstraction
│   │   │   ├── IMainThreadDispatcher.cs       Thread dispatcher abstraction
│   │   │   ├── IPlayerManager.cs              Player management interface
│   │   │   └── IPlayerContext.cs              Per-player state interface
│   │   ├── Collections/
│   │   │   └── RingBuffer.cs                  Thread-safe circular buffer (500 entries)
│   │   ├── Mcp/
│   │   │   ├── McpServer.cs                   HTTP server (SSE + Streamable HTTP)
│   │   │   ├── McpSessionContext.cs           AsyncLocal session ID for multi-client isolation
│   │   │   ├── McpProtocol.cs                 JSON-RPC 2.0 DTOs
│   │   │   ├── McpToolRegistry.cs             Tool registry + error beacon
│   │   │   ├── McpResult.cs                   Tool result builder
│   │   │   └── Tools/
│   │   │       ├── PlayerTools.cs             add_players, remove_player, list_windows
│   │   │       ├── NavigationTools.cs         navigate, wait, reload, back, forward
│   │   │       ├── InteractionTools.cs        tap, swipe, scroll, type, hover, select, dialog
│   │   │       ├── ReadTools.cs               read, read_all, screenshot
│   │   │       ├── ExecutionTools.cs          execute_js
│   │   │       ├── AuthTools.cs               quick_auth
│   │   │       ├── EmulationTools.cs          set_device/viewport/location/network/language
│   │   │       ├── StateTools.cs              get_state, get_notifications
│   │   │       ├── DiagnosticsTools.cs        get_console, get_network, get_performance, clear_logs, storage, cookies
│   │   │       ├── HelpTools.cs               get_manual
│   │   │       └── UpdateTools.cs            check_update, update
│   │   ├── Models/
│   │   │   ├── AppConfig.cs                   FrontendUrl, BackendUrl, McpPort, etc.
│   │   │   ├── DevicePreset.cs                22 presets (phones/tablets/desktops)
│   │   │   ├── LocationPreset.cs              5 city presets
│   │   │   ├── NetworkPreset.cs               5 network conditions
│   │   │   ├── ConsoleEntry.cs                Console log record
│   │   │   ├── NetworkEntry.cs                Network request record
│   │   │   ├── AuthResult.cs                  Token + user data
│   │   │   ├── NoiseAuthState.cs              Frontend auth state shape
│   │   │   ├── PushNotification.cs            Push notification record
│   │   │   ├── TelegramUserConfig.cs          Telegram WebApp user
│   │   │   └── ApiEnvelope.cs                 Backend API response wrapper
│   │   └── Services/
│   │       ├── CdpService.cs                  CDP method caller wrapper
│   │       ├── DeviceEmulationService.cs      Device metrics + UA via CDP
│   │       ├── LocationEmulationService.cs    Geolocation + timezone + locale via CDP
│   │       ├── NetworkEmulationService.cs     Network throttling via CDP
│   │       ├── QuickAuthService.cs            Auto-register/login via backend API
│   │       ├── TokenInjectionService.cs       localStorage injection of auth tokens
│   │       ├── TelegramInitDataService.cs     HMAC-SHA256 signed initData
│   │       ├── TelegramInjectionService.cs    window.Telegram.WebApp API injection
│   │       ├── ConsoleInterceptionService.cs  CDP Runtime.consoleAPICalled listener
│   │       ├── NetworkMonitoringService.cs    CDP Network.* event listener
│   │       ├── NotificationInterceptionService.cs  Push notification capture
│   │       └── UpdateService.cs               Version check + download + apply via GitHub API
│   │
│   ├── BrowserXn/                         ← Windows GUI (net8.0-windows, WPF)
│   │   ├── Converters/                        XAML value converters
│   │   ├── Engines/                           WebView2ControlAdapter
│   │   ├── Interop/                           Win32 P/Invoke (DWM, User32)
│   │   ├── Platform/                          WpfDispatcher, WebView2CdpSubscription
│   │   ├── Services/                          DI registration
│   │   ├── Themes/                            Dark theme styles
│   │   ├── ViewModels/                        MVVM (MainViewModel : IPlayerManager)
│   │   └── Views/                             WPF XAML (5 views) + VideoWallPanel
│   │
│   └── GDD.Headless/                     ← CLI runner (headless/headed, net8.0, cross-platform)
│       ├── Engines/                           PlaywrightEngine
│       ├── Platform/                          ConsoleDispatcher, HeadlessPlayerManager
│       └── Scripts/                           mcp-proxy.sh, mcp-proxy.ps1
│
└── .github/workflows/                    ← CI/CD (build all platforms + release)
```

---

## 5. Core Components

### 5.1 Browser Engine (WebView2)

`IBrowserEngine` — an abstraction over the browser engine:

```csharp
interface IBrowserEngine : IAsyncDisposable
{
    int PlayerId { get; }
    string UserDataFolder { get; }
    bool IsInitialized { get; }
    string CurrentUrl { get; }

    Task InitializeAsync(object? hostHandle, string startUrl);
    Task NavigateAsync(string url);
    Task<string> ExecuteJavaScriptAsync(string script);
    Task CallCdpMethodAsync(string methodName, string parametersJson);
    Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson);
    Task<byte[]> CaptureScreenshotAsync(int quality = 80);
    Task InjectScriptOnDocumentCreatedAsync(string script);
    ICdpEventSubscription SubscribeToCdpEvent(string eventName);

    event EventHandler<NotificationEventArgs>? NotificationReceived;
    event EventHandler<string>? NavigationCompleted;
    event EventHandler<string>? TitleChanged;
}
```

Two implementations:

**WebView2ControlAdapter** (Windows GUI) — each player gets:

- An isolated user data folder (`%LOCALAPPDATA%\GDD\Profiles\Player_{id}`)
- Its own `CoreWebView2Environment` + `CoreWebView2Controller`
- Settings: status bar, context menu, zoom, and devtools are disabled
- Automatic Notifications and Geolocation permission grants
- Screenshots via CDP `Page.captureScreenshot` (JPEG, CSS pixel resolution, clip with scroll offset `pageX/pageY`)
- Load waiting via CDP `Page.loadEventFired`

**PlaywrightEngine** (Headless/Headed, cross-platform) — each player gets:

- An isolated `BrowserContext` with viewport, user agent, touch
- CDP session via `context.NewCDPSessionAsync(page)`
- Screenshots via `page.ScreenshotAsync()` with `ScreenshotScale.Css` (JPEG)
- Load waiting via `WaitForLoadStateAsync(LoadState.Load)`
- Launches visible Chromium windows by default; `--headless` disables UI (for CI/CD)

### 5.2 MCP Server

Custom HTTP server on `HttpListener` with two transports:

| Transport | Endpoints | Protocol |
| --------- | --------- | -------- |
| Streamable HTTP | `POST /mcp` | Request → JSON response |
| SSE | `GET /sse` + `POST /message?sessionId=` | Bidirectional SSE stream |

Port: 9700 with fallback to 9709. CORS enabled for all origins.

**Session isolation:** `McpSessionContext` (AsyncLocal) stores the session ID of the current request. `McpServer` sets it before calling `ProcessRequest` — for Streamable HTTP from the `Mcp-Session-Id` header, for SSE from the `sessionId` query parameter. `IPlayerManager.GetPlayer/GetPlayers` filters by session — each MCP client sees only its own players. Bind address `"*"` enables LAN access; session isolation prevents cross-client interference.

> ⚠ **Warning:** Players created via the Windows GUI (not through MCP) have `OwnerSessionId = null` and are accessible to ALL connected MCP sessions.

### 5.3 Emulation Services

All operate via CDP (Chrome DevTools Protocol):

**DeviceEmulationService** — 22 device presets:
- `Emulation.setDeviceMetricsOverride` (width, height, DPI, mobile)
- `Emulation.setUserAgentOverride` (User-Agent string)
- `Emulation.setTouchEmulationEnabled` (5 touch points)

**LocationEmulationService** — 5 cities + custom:
- `Emulation.setGeolocationOverride` (lat, lon, accuracy)
- `Emulation.setTimezoneOverride` (IANA timezone)
- `Emulation.setLocaleOverride` (BCP-47 locale)

**NetworkEmulationService** — 5 presets:
- `Network.emulateNetworkConditions` (offline, latency, throughput)
- Online / 4G (20ms) / Fast 3G (563ms) / Slow 3G (2s) / Offline

### 5.4 Diagnostics

**ConsoleInterceptionService:**
- CDP: `Runtime.consoleAPICalled`, `Runtime.exceptionThrown`
- Per-player `RingBuffer<ConsoleEntry>` with 500 entries
- Filtering by level (log/warn/error/info/debug)

**NetworkMonitoringService:**
- CDP: `Network.requestWillBeSent`, `responseReceived`, `loadingFinished`, `loadingFailed`
- Per-player `RingBuffer<NetworkEntry>` with 500 entries
- URL, status, MIME, duration, error text

**Performance:** `Performance.getMetrics` CDP — JS heap, DOM nodes, task duration

### 5.5 Authentication

**QuickAuthService** — auto-registration/login:
- Generates credentials: `player{id}@gdd.test` / `GDD-Player{id}!`
- POST `/auth/register` → fallback `/auth/login`
- Returns `AuthResult` (accessToken, sessionToken, user)

**TokenInjectionService** — token injection:
- Writes `NoiseAuthState` to `localStorage["noise-auth"]`
- Redirects to frontend URL

**TelegramInjectionService** — Telegram WebApp emulation:
- Injects `window.Telegram.WebApp` API
- HMAC-SHA256 signed `initData` via BotToken
- Mock CloudStorage, BackButton, MainButton

### 5.6 UI (WPF)

**MainWindow** — main interface:
- Toolbar: Add Player, Device Presets, Quick Auth, Navigate All
- VideoWallPanel: adaptive grid of player thumbnails
- Status bar: player count, status

**BrowserCellControl** — player thumbnail:
- Live DWM thumbnail rendering (Win32 DwmRegisterThumbnail)
- Click → opens OverlayWindow

**OverlayWindow** — floating window with live WebView2:
- Size matches device preset
- Title bar with device name

---

## 6. Platform Dependency Map

### Windows-only (16% codebase)

| Component | Dependency | Why Windows |
| --------- | ---------- | ----------- |
| `WebView2Engine` | Microsoft.Web.WebView2 | Chromium control for Windows (HWND parenting) |
| `DwmApi.cs` | dwmapi.dll, user32.dll | DWM thumbnail API, window management |
| `BrowserCellControl` | DWM thumbnails | Live thumbnails via Win32 |
| `OverlayWindow` | HwndSource, WndProc | Win32 message loop interop |
| XAML Views (5 files) | WPF | Windows-only UI framework |
| `MainViewModel` | Dispatcher, SystemParameters | WPF threading model |

### Platform-Agnostic (59% codebase)

| Component | Files | Notes |
| --------- | ----- | ----- |
| Services (CDP, auth, emulation) | 11 | Pure C#, operate via CDP JSON commands |
| Models (presets, DTOs) | 11 | POCOs |
| MCP Server + Protocol | 4 | HTTP/JSON-RPC, no OS dependencies |
| MCP Tools | 10 | Business logic → IPlayerManager calls |
| Collections (RingBuffer) | 1 | Thread-safe generic collection |
| Abstractions (interfaces) | 6 | IBrowserEngine, IBrowserEngineFactory, IPlayerManager, IPlayerContext, IMainThreadDispatcher, ICdpEventSubscription |

### Abstracted (25%)

| Component | Abstraction | Implementations |
| --------- | ----------- | --------------- |
| MCP Tools → IPlayerManager | Dispatcher.InvokeAsync | WpfDispatcher (GUI) / ConsoleDispatcher (CLI) |
| Services → IBrowserEngine | CallCdpMethodAsync | WebView2 CDP / Playwright CDPSession |
| AppConfig | `Environment.SpecialFolder.LocalApplicationData` | Cross-platform (.NET) |

---

## 7. Cross-Platform Architecture (Implemented)

### 7.1 Current Architecture

```text
┌──────────────────────────────────────────────────┐
│                  GDD.Core (net8.0)                │
│  Models · Services · MCP Server · MCP Tools       │
│  Abstractions · Collections                       │
└──────────────────────┬───────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        ▼                             ▼
┌──────────────┐              ┌────────────────┐
│ BrowserXn    │              │ GDD.Headless   │
│ (WPF+WV2)   │              │ (Playwright)   │
│ Windows GUI  │              │ Win/Linux/macOS│
└──────────────┘              └────────────────┘
```

GDD.Core is the shared library containing all platform-independent code. Both BrowserXn (Windows GUI) and GDD.Headless (cross-platform) reference it and provide platform-specific implementations of `IBrowserEngine`, `IPlayerManager`, and `IMainThreadDispatcher`.

### 7.2 Key Abstractions

```csharp
interface IBrowserEngine : IAsyncDisposable
{
    int PlayerId { get; }
    string UserDataFolder { get; }
    bool IsInitialized { get; }
    string CurrentUrl { get; }

    Task InitializeAsync(object? hostHandle, string startUrl);
    Task NavigateAsync(string url);
    Task<string> ExecuteJavaScriptAsync(string script);
    Task CallCdpMethodAsync(string methodName, string parametersJson);
    Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson);
    Task<byte[]> CaptureScreenshotAsync(int quality = 80);
    Task InjectScriptOnDocumentCreatedAsync(string script);
    ICdpEventSubscription SubscribeToCdpEvent(string eventName);

    event EventHandler<NotificationEventArgs>? NotificationReceived;
    event EventHandler<string>? NavigationCompleted;
    event EventHandler<string>? TitleChanged;
}

interface IPlayerManager
{
    IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null);
    IReadOnlyList<IPlayerContext> GetPlayers();
    IPlayerContext? GetPlayer(int playerId);
    void RemovePlayer(int playerId);
}

interface IMainThreadDispatcher
{
    Task InvokeAsync(Func<Task> action);
    Task InvokeAsync(Action action);
}
```

### 7.3 Platform Implementations

| Abstraction | BrowserXn (Windows GUI) | GDD.Headless (Cross-platform) |
| ----------- | ----------------------- | ----------------------------- |
| `IBrowserEngine` | `WebView2ControlAdapter` — WebView2 + HWND | `PlaywrightEngine` — Playwright Chromium |
| `IPlayerManager` | `MainViewModel` — WPF MVVM | `HeadlessPlayerManager` — console |
| `IMainThreadDispatcher` | `WpfDispatcher` — WPF UI thread | `ConsoleDispatcher` — direct execution |
| `ICdpEventSubscription` | `WebView2CdpSubscription` — WebView2 events | `PlaywrightCdpSubscription` — CDP session |

### 7.4 Headed Mode (GUI on Linux/macOS)

`GDD.Headless` launches Playwright with `Headless = false` by default, opening visible Chromium windows. Use `--headless` to run without UI. MCP tools work identically in both modes. This provides GUI-like visual testing on Linux/macOS without WPF or CefGlue.

Configuration: `AppConfig.Headed` property (default `true`) or `--headless`/`--headed` CLI flags. Proxy scripts also support these flags.

**Future — Full Management UI on All Platforms:**

| Approach | Quality | Performance | Complexity |
| -------- | ------- | ----------- | ---------- |
| Periodic screenshot polling (500ms) | Medium | Low CPU | Simple |
| CDP `Page.screencastFrame` stream | High | Medium CPU | Medium |
| Avalonia UI + CefGlue | Full GUI | Medium | High |

---

## 8. Build & CI/CD

GitHub Actions builds 5 targets on every push to master:

| Target | Runner | Output |
| ------ | ------ | ------ |
| `GDD-Desktop-Windows` | windows-latest | WPF + WebView2 single-file EXE |
| `GDD-Server-Windows` | windows-latest | Playwright (headless/headed) |
| `GDD-Server-Linux` | ubuntu-22.04 | Playwright (headless/headed) |
| `GDD-Server-macOS-ARM` | macos-14 | Apple Silicon (M1/M2/M3/M4) |
| `GDD-Server-macOS-Intel` | macos-14 | Intel Mac (cross-compiled on ARM, Chromium from CDN) |

Each build runs a smoke test: starts GDD.Headless, queries `tools/list` via HTTP, verifies 37 tools are registered. The osx-x64 target is cross-compiled on an ARM runner (macos-14) and skips the smoke test since ARM `pwsh` cannot load x64 .NET assemblies.

Tags matching `v*` trigger GitHub Releases with `.zip` (Windows) and `.tar.gz` (Linux/macOS) archives for all targets.

---

## 9. Summary

**Current state:** GDD.Core contains all platform-independent logic (~75% of the codebase). BrowserXn (Windows GUI) and GDD.Headless (cross-platform) implement platform-specific abstractions.

**Headless mode** via Playwright .NET works on Windows, Linux, and macOS with an identical set of 37 MCP tools.

**Windows GUI** provides visual preview with DWM thumbnails and live WebView2 windows.

**Potential future development:** Avalonia UI for cross-platform GUI with CDP screencast instead of DWM thumbnails.
