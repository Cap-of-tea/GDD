# GDD — Architecture & Cross-Platform Analysis

## 1. What is GDD

GDD (Giggly-Dazzling-Duckling) — десктопное приложение для мультибраузерного тестирования веб-приложений. Управляет N изолированными Chromium-инстансами ("players") через Chrome DevTools Protocol и предоставляет 25 MCP-инструментов для автоматизации через Claude Code или любой MCP-клиент.

**Ключевая идея:** один AI-агент (Claude) видит и управляет несколькими браузерами одновременно — навигация, клики, скриншоты, эмуляция устройств/сети/геолокации, мониторинг консоли и сетевых запросов.

---

## 2. Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET 8.0-windows | net8.0-windows TFM |
| UI Framework | WPF (XAML) | built-in |
| Browser Engine | Microsoft WebView2 | 1.0.2739.15 |
| MVVM | CommunityToolkit.Mvvm | 8.3.2 |
| DI / Hosting | Microsoft.Extensions.Hosting | 8.0.1 |
| HTTP Client | Microsoft.Extensions.Http | 8.0.1 |
| Logging | Serilog (File sink, daily rolling) | 8.0.0 |
| Protocol | MCP (Model Context Protocol) via HTTP | 2024-11-05 |
| Browser Control | Chrome DevTools Protocol (CDP) | via WebView2 |
| Window Mgmt | Win32 P/Invoke (DWM, User32) | — |

---

## 3. Architecture

```
┌──────────────────────────────────────────────────────┐
│                   MCP Client (Claude Code)           │
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
│              McpToolRegistry (25 tools)               │
│  PlayerTools · NavigationTools · InteractionTools     │
│  ReadTools · EmulationTools · AuthTools               │
│  StateTools · DiagnosticsTools                        │
└─────────────────────┬────────────────────────────────┘
                      │  Dispatcher.InvokeAsync
┌─────────────────────▼────────────────────────────────┐
│              MainViewModel (MVVM)                     │
│  Players: ObservableCollection<BrowserCellViewModel>  │
│  Orchestrates all services                            │
└───┬─────────┬──────────┬────────────┬────────────────┘
    │         │          │            │
    ▼         ▼          ▼            ▼
┌────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────┐
│WebView2│ │Emulation │ │   Auth   │ │  Diagnostics  │
│Engine  │ │Services  │ │Services  │ │  Services     │
│(CDP)   │ │Device    │ │QuickAuth │ │Console        │
│        │ │Location  │ │TokenInj. │ │Network        │
│        │ │Network   │ │Telegram  │ │Performance    │
│        │ │Language  │ │          │ │Notifications  │
└────┬───┘ └────┬─────┘ └────┬─────┘ └───────┬───────┘
     │          │            │                │
     └──────────┴────────────┴────────────────┘
                      │
         Chrome DevTools Protocol (CDP)
         CoreWebView2.CallDevToolsProtocolMethodAsync()
```

### Startup Flow

```
App.OnStartup()
  → Host.CreateDefaultBuilder()
  → LoadConfig("appsettings.json") → AppConfig
  → ConfigureSerilog()
  → RegisterServices(DI container)
  → RegisterMcpTools() — 25 tools → McpToolRegistry
  → StartMcpServer() — HttpListener on :9700
  → new MainWindow { DataContext = MainViewModel }
```

### Request Flow (MCP tool call)

```
Claude Code → POST /mcp {"method":"tools/call","params":{"name":"gdd_navigate",...}}
  → McpServer.HandleStreamableHttp()
  → McpToolRegistry.InvokeAsync("gdd_navigate", args)
  → Dispatcher.InvokeAsync (→ WPF UI thread)
  → MainViewModel.Players[id].Engine.NavigateAsync(url)
  → CoreWebView2.Navigate(url)
  → JSON-RPC response → Claude Code
```

---

## 4. Project Structure

```
src/BrowserXn/
├── Abstractions/           IBrowserEngine, IBrowserEngineFactory
├── Collections/            RingBuffer<T> (circular buffer, 500 entries)
├── Controls/               VideoWallPanel (custom WPF layout panel)
├── Converters/             BoolToVisibilityConverter
├── Engines/                WebView2Engine, WebView2EngineFactory
├── Interop/                DwmApi (P/Invoke: DWM thumbnails, User32)
├── Mcp/
│   ├── McpServer.cs        HTTP server (SSE + Streamable HTTP)
│   ├── McpProtocol.cs      JSON-RPC 2.0 DTOs
│   ├── McpToolRegistry.cs  Tool name → handler registry
│   ├── McpResult.cs        Helper for building tool results
│   └── Tools/
│       ├── PlayerTools.cs       add_players, remove_player, list_windows
│       ├── NavigationTools.cs   navigate, wait
│       ├── InteractionTools.cs  tap, swipe, scroll, type
│       ├── ReadTools.cs         read, read_all, screenshot
│       ├── ExecutionTools.cs    execute_js
│       ├── AuthTools.cs         quick_auth
│       ├── EmulationTools.cs    set_device/viewport/location/network/language
│       ├── StateTools.cs        get_state, get_notifications
│       └── DiagnosticsTools.cs  get_console, get_network, get_performance, clear_logs
├── Models/
│   ├── AppConfig.cs             FrontendUrl, BackendUrl, McpPort, etc.
│   ├── DevicePreset.cs          21 preset (phones/tablets/desktops)
│   ├── LocationPreset.cs        5 city presets
│   ├── NetworkPreset.cs         5 network conditions
│   ├── ConsoleEntry.cs          Console log record
│   ├── NetworkEntry.cs          Network request record
│   ├── AuthResult.cs            Token + user data
│   ├── NoiseAuthState.cs        Frontend auth state shape
│   ├── PushNotification.cs      Push notification record
│   ├── TelegramUserConfig.cs    Telegram WebApp user
│   └── ApiEnvelope.cs           Backend API response wrapper
├── Services/
│   ├── CdpService.cs                     CDP method caller wrapper
│   ├── DeviceEmulationService.cs          Device metrics + UA via CDP
│   ├── LocationEmulationService.cs        Geolocation + timezone + locale via CDP
│   ├── NetworkEmulationService.cs         Network throttling via CDP
│   ├── QuickAuthService.cs                Auto-register/login via backend API
│   ├── TokenInjectionService.cs           localStorage injection of auth tokens
│   ├── TelegramInitDataService.cs         HMAC-SHA256 signed initData
│   ├── TelegramInjectionService.cs        window.Telegram.WebApp API injection
│   ├── ConsoleInterceptionService.cs      CDP Runtime.consoleAPICalled listener
│   ├── NetworkMonitoringService.cs        CDP Network.* event listener
│   ├── NotificationInterceptionService.cs Push notification capture
│   └── IServiceCollectionExtensions.cs    DI registration
├── ViewModels/
│   ├── MainViewModel.cs           Player orchestration, commands, services
│   ├── BrowserCellViewModel.cs    Per-player state (URL, device, errors, etc.)
│   └── OverlayViewModel.cs        Floating browser window
├── Views/
│   ├── MainWindow.xaml/.cs        Main grid: toolbar + player cells + status bar
│   ├── OverlayWindow.xaml/.cs     Floating window with live WebView2
│   ├── CellSettingsWindow.xaml/.cs Device/location/network settings dialog
│   └── BrowserCellControl.xaml/.cs Player thumbnail with DWM rendering
└── Themes/
    └── Generic.xaml               Color scheme, button styles
```

---

## 5. Core Components

### 5.1 Browser Engine (WebView2)

`IBrowserEngine` — абстракция над браузерным движком:

```csharp
interface IBrowserEngine : IAsyncDisposable
{
    int PlayerId { get; }
    bool IsInitialized { get; }
    string CurrentUrl { get; }

    Task InitializeAsync(nint parentHwnd, string startUrl);
    Task NavigateAsync(string url);
    Task<string> ExecuteJavaScriptAsync(string script);
    Task CallCdpMethodAsync(string methodName, string parametersJson);
    Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson);
    Task<byte[]> CaptureScreenshotAsync();
    Task InjectScriptOnDocumentCreatedAsync(string script);

    event EventHandler<NotificationEventArgs>? NotificationReceived;
    event EventHandler<string>? NavigationCompleted;
    event EventHandler<string>? TitleChanged;
}
```

**WebView2Engine** — единственная реализация. Каждый player получает:
- Изолированный user data folder (`%LOCALAPPDATA%\GDD\Profiles\Player_{id}`)
- Собственный `CoreWebView2Environment` + `CoreWebView2Controller`
- Настройки: отключены статусбар, контекстное меню, зум, devtools
- Автоматическое разрешение Notifications и Geolocation
- Скриншоты через `Page.captureScreenshot` CDP (base64 PNG)

### 5.2 MCP Server

Кастомный HTTP-сервер на `HttpListener` с двумя транспортами:

| Transport | Endpoints | Протокол |
|-----------|-----------|----------|
| Streamable HTTP | `POST /mcp` | Request → JSON response |
| SSE | `GET /sse` + `POST /message?sessionId=` | Bidirectional SSE stream |

Порт: 9700 с fallback до 9709. CORS включен для всех origins.

### 5.3 Emulation Services

Все работают через CDP (Chrome DevTools Protocol):

**DeviceEmulationService** — 21 пресет устройства:
- `Emulation.setDeviceMetricsOverride` (ширина, высота, DPI, mobile)
- `Emulation.setUserAgentOverride` (User-Agent строка)
- `Emulation.setTouchEmulationEnabled` (5 touch points)

**LocationEmulationService** — 5 городов + custom:
- `Emulation.setGeolocationOverride` (lat, lon, accuracy)
- `Emulation.setTimezoneOverride` (IANA timezone)
- `Emulation.setLocaleOverride` (BCP-47 locale)

**NetworkEmulationService** — 5 пресетов:
- `Network.emulateNetworkConditions` (offline, latency, throughput)
- Online / 4G (20ms) / Fast 3G (563ms) / Slow 3G (2s) / Offline

### 5.4 Diagnostics

**ConsoleInterceptionService:**
- CDP: `Runtime.consoleAPICalled`, `Runtime.exceptionThrown`
- Per-player `RingBuffer<ConsoleEntry>` на 500 записей
- Фильтрация по level (log/warn/error/info/debug)

**NetworkMonitoringService:**
- CDP: `Network.requestWillBeSent`, `responseReceived`, `loadingFinished`, `loadingFailed`
- Per-player `RingBuffer<NetworkEntry>` на 500 записей
- URL, status, MIME, duration, error text

**Performance:** `Performance.getMetrics` CDP — JS heap, DOM nodes, task duration

### 5.5 Authentication

**QuickAuthService** — авто-регистрация/логин:
- Генерирует credentials: `player{id}@gdd.test` / `GDD-Player{id}!`
- POST `/auth/register` → fallback `/auth/login`
- Возвращает `AuthResult` (accessToken, sessionToken, user)

**TokenInjectionService** — инъекция токенов:
- Записывает `NoiseAuthState` в `localStorage["noise-auth"]`
- Перенаправляет на frontend URL

**TelegramInjectionService** — эмуляция Telegram WebApp:
- Инъекция `window.Telegram.WebApp` API
- HMAC-SHA256 подпись `initData` через BotToken
- Mock CloudStorage, BackButton, MainButton

### 5.6 UI (WPF)

**MainWindow** — основной интерфейс:
- Toolbar: Add Player, Device Presets, Quick Auth, Navigate All
- VideoWallPanel: адаптивная сетка миниатюр players
- Status bar: количество players, статус

**BrowserCellControl** — миниатюра player:
- Live DWM thumbnail рендеринг (Win32 DwmRegisterThumbnail)
- Click → открывает OverlayWindow

**OverlayWindow** — floating окно с live WebView2:
- Размер = пресету устройства
- Title bar с именем устройства

---

## 6. Platform Dependency Map

### Windows-only (16% codebase)

| Component | Dependency | Why Windows |
|-----------|-----------|-------------|
| `WebView2Engine` | Microsoft.Web.WebView2 | Chromium control для Windows (HWND parenting) |
| `DwmApi.cs` | dwmapi.dll, user32.dll | DWM thumbnail API, window management |
| `BrowserCellControl` | DWM thumbnails | Live миниатюры через Win32 |
| `OverlayWindow` | HwndSource, WndProc | Win32 message loop interop |
| XAML Views (4 файла) | WPF | Windows-only UI framework |
| `MainViewModel` | Dispatcher, SystemParameters | WPF threading model |

### Platform-Agnostic (59% codebase)

| Component | Files | Notes |
|-----------|-------|-------|
| Services (CDP, auth, emulation) | 12 | Pure C#, оперируют CDP JSON commands |
| Models (presets, DTOs) | 11 | POCOs |
| MCP Server + Protocol | 3 | HTTP/JSON-RPC, no OS dependencies |
| MCP Tools | 8 | Business logic → ViewModel calls |
| Collections (RingBuffer) | 1 | Thread-safe generic collection |
| Abstractions (interfaces) | 2 | IBrowserEngine, IBrowserEngineFactory |

### Coupled but Portable (25%)

| Component | Issue | Fix |
|-----------|-------|-----|
| MCP Tools → MainViewModel | Dispatcher.InvokeAsync для UI thread | Абстрагировать через ISynchronizationContext |
| Services → CoreWebView2 | Прямые CDP вызовы через WebView2 API | Абстрагировать CDP transport layer |
| AppConfig | `Environment.SpecialFolder.LocalApplicationData` | Уже кроссплатформенный |

---

## 7. Cross-Platform Refactoring Strategy

### 7.1 Target Architecture

```
┌──────────────────────────────────────────────────┐
│                  GDD.Core (netstandard2.1)        │
│  Models · Services · MCP Server · MCP Tools       │
│  Abstractions · Collections                       │
└──────────────────────┬───────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
┌──────────────┐ ┌───────────┐ ┌────────────┐
│ GDD.Desktop  │ │ GDD.Mac   │ │ GDD.Linux  │
│ (WPF+WV2)   │ │ (Avalonia │ │ (Avalonia  │
│ Windows      │ │ +WKWebView│ │ +CEF/GTK)  │
│              │ │ or CEF)   │ │            │
└──────────────┘ └───────────┘ └────────────┘
```

### 7.2 Phase 1 — Extract GDD.Core (shared library)

**Цель:** вынести platform-agnostic код в отдельную сборку.

```
GDD.Core/
├── Abstractions/
│   ├── IBrowserEngine.cs          # убрать nint parentHwnd → object hostHandle
│   ├── IBrowserEngineFactory.cs
│   ├── ICdpTransport.cs           # NEW: абстракция CDP вызовов
│   └── IMainThreadDispatcher.cs   # NEW: замена WPF Dispatcher
├── Collections/
│   └── RingBuffer.cs
├── Mcp/                           # весь MCP as-is
├── Models/                        # все модели as-is
└── Services/                      # все сервисы, но через ICdpTransport
```

**Ключевые новые абстракции:**

```csharp
// Замена прямых CoreWebView2 CDP вызовов
interface ICdpTransport
{
    Task CallAsync(string method, string parametersJson);
    Task<string> CallWithResultAsync(string method, string parametersJson);
    Task SubscribeEventAsync(string eventName, Action<string> handler);
}

// Замена WPF Dispatcher
interface IMainThreadDispatcher
{
    Task InvokeAsync(Func<Task> action);
}
```

**Объем:** ~35 файлов перенести без изменений, ~15 файлов — мелкий рефакторинг (заменить прямые CDP вызовы на ICdpTransport).

### 7.3 Phase 2 — Browser Engine Alternatives

| Platform | Engine | CDP Support | Maturity |
|----------|--------|-------------|----------|
| **Windows** | WebView2 (current) | Built-in | Production |
| **macOS** | WKWebView + WebKit Remote Debug | Partial | Experimental |
| **macOS/Linux** | CEFSharp / CefGlue | Full CDP | Production |
| **All platforms** | Playwright .NET | Full CDP | Production |
| **All platforms** | Puppeteer-Sharp | Full CDP | Production |
| **Headless** | Chrome/Chromium + CDP WebSocket | Full CDP | Production |

**Рекомендация:** Playwright .NET или headless Chromium + CDP WebSocket.

**Playwright .NET:**
- Кроссплатформенный из коробки
- Управляет Chromium/Firefox/WebKit
- Полный CDP доступ: `page.Context.NewCDPSession()`
- Скриншоты, навигация, JS execution — всё через единый API
- Недостаток: headless по умолчанию, headed mode ограничен

**Headless Chromium + CDP WebSocket:**
- Запуск `chrome --remote-debugging-port=9222`
- Подключение через WebSocket к CDP endpoint
- Полный контроль, как с WebView2
- Работает на всех платформах
- Недостаток: нужен установленный Chrome/Chromium

### 7.4 Phase 3 — UI Framework

| Framework | Platforms | WebView Support | Effort |
|-----------|-----------|----------------|--------|
| **Avalonia UI** | Windows, macOS, Linux | CefGlue / WebView control | High |
| **MAUI** | Windows, macOS, (Linux limited) | WebView2 / WKWebView | Medium |
| **Terminal UI (Spectre.Console)** | All | Headless only | Low |
| **Headless (no UI)** | All | n/a | Minimal |

**Рекомендация:** два варианта в зависимости от приоритетов:

**Вариант A — Headless-first (минимальный effort):**
- GDD.Core + Playwright/CDP WebSocket
- Без GUI, только MCP-сервер
- Управление исключительно через Claude Code
- Console app на всех платформах
- Effort: 2-3 недели

**Вариант B — Avalonia UI (полный port):**
- GDD.Core + Avalonia + CefGlue
- Полноценный GUI на всех платформах
- Live preview, thumbnails (без DWM — fallback на screenshot-based)
- Effort: 6-8 недель

### 7.5 Phase 4 — DWM Thumbnails Replacement

DWM live thumbnails — уникальная Windows-фича. Кроссплатформенные альтернативы:

| Approach | Quality | Performance | Complexity |
|----------|---------|-------------|------------|
| Periodic screenshot polling (500ms) | Medium | Low CPU | Simple |
| CDP `Page.screencastFrame` stream | High | Medium CPU | Medium |
| Offscreen rendering (CEF) | High | High CPU | Complex |

Рекомендация: `Page.screencastFrame` CDP — real-time stream скриншотов прямо через CDP, работает на всех платформах.

---

## 8. Migration Roadmap

```
Phase 1: Extract GDD.Core          [2 weeks]
  ├─ Create GDD.Core class library
  ├─ Move Models, Services, MCP, Collections
  ├─ Introduce ICdpTransport, IMainThreadDispatcher
  ├─ Refactor Services to use ICdpTransport
  └─ Windows version works as before (WebView2 implements interfaces)

Phase 2: Headless Cross-Platform    [2-3 weeks]
  ├─ Create GDD.Headless console app
  ├─ Implement PlaywrightBrowserEngine : IBrowserEngine
  ├─ Or ChromeCdpEngine (raw WebSocket CDP)
  ├─ MCP server starts, tools work
  └─ Test on macOS + Linux

Phase 3: Avalonia UI (optional)     [4-6 weeks]
  ├─ Create GDD.Avalonia project
  ├─ Port MainWindow, OverlayWindow
  ├─ Replace DWM thumbnails with CDP screencast
  ├─ CefGlue integration for embedded browser
  └─ Full-featured desktop app on all platforms

Phase 4: Polish                     [1-2 weeks]
  ├─ Platform-specific packaging (DMG, AppImage, MSI)
  ├─ Auto-launch configuration
  └─ CI/CD for all platforms
```

---

## 9. Summary

**Текущее состояние:** ~60% кода уже platform-agnostic. Архитектура с интерфейсом `IBrowserEngine` заложила фундамент для портирования, но прямые зависимости от WebView2 API в сервисах и WPF Dispatcher в MCP tools требуют рефакторинга.

**Минимальный path к кроссплатформенности:** headless mode через Playwright .NET — 2-3 недели работы, покрывает 100% MCP-функциональности без UI.

**Полный port с GUI:** Avalonia + CefGlue — 10-14 недель, но даёт полноценное десктопное приложение на Windows/macOS/Linux.

**Критический выбор:** browser engine. Playwright .NET даёт кроссплатформенность из коробки и активно поддерживается Microsoft. Headless Chromium + CDP WebSocket — максимальная гибкость и контроль. CEFSharp/CefGlue — для embedded browser в GUI.
