# GDD — Linux & macOS Standalone Versions: Plan

## Context

GDD — Windows WPF приложение (5,600 LOC), 25 MCP-инструментов, WebView2. Задача: standalone приложения для Linux и macOS с общим ядром GDD.Core.

**Критический факт:** WebView2 .NET SDK поддерживает **только Windows**. macOS WebView2 — только для Swift/Obj-C. На Linux WebView2 не существует вообще. Для Linux и macOS нужен альтернативный браузерный движок.

**Ключевой инсайт:** GDD управляется через MCP (Claude Code шлёт команды). GUI вторичен. Headless-режим = 100% функциональный паритет.

---

## Проверенные платформенные факты

| Компонент | Windows | Linux | macOS |
|-----------|---------|-------|-------|
| WebView2 .NET SDK | **Да** | **Нет** | **Нет** |
| HttpListener localhost | Работает | Работает без root | Работает |
| Path.Combine | `\` | `/` | `/` |
| SpecialFolder.LocalApplicationData | `%LOCALAPPDATA%` | `~/.local/share` | `~/Library/Application Support` |
| System.Drawing | Нативный | Требует libgdiplus | Частичная поддержка |
| Self-contained .NET 8 | Работает | Нужен ICU или InvariantGlobalization | Code signing обязателен |
| Hardcoded `\\` в C# коде | — | — | **Нет** (проверено, везде Path.Combine) |

---

## Выбор браузерного движка

### Headless режим: Playwright .NET

| Критерий | Playwright | CefGlue | Puppeteer-Sharp |
|----------|-----------|---------|-----------------|
| Поддержка .NET 8 | Да (Microsoft) | Неопределённо | Да |
| Кроссплатформенность | Win/Linux/macOS | Win/Linux/macOS | Win/Linux/macOS |
| CDP доступ | `page.Client.SendAsync()` | `SendDevToolsMessage()` | Нативный CDP |
| Авто-скачивание браузера | `playwright install chromium` | Ручной CEF download (~200MB) | Авто Chromium |
| Встраивание в окно | **Нет** | Да | **Нет** |
| Обслуживание | Microsoft, активный | Сообщество, нестабильный | Активный |
| Размер | ~150MB (Chromium) | ~200MB (CEF) | ~150MB (Chromium) |

**Решение:**
- **Headless (Phase 1):** Playwright .NET — надёжный, кроссплатформенный, Microsoft-maintained, полный CDP
- **GUI Windows:** WebView2 (без изменений)
- **GUI Linux (Phase 3+):** CefGlue + GTK (единственный вариант встраивания Chromium в .NET на Linux)
- **GUI macOS (Phase 3+):** CefGlue + Cocoa (WKWebView не поддерживает CDP)

---

## Архитектура решения

```
GDD.sln
├── src/
│   ├── GDD.Core/              ← net8.0 shared library
│   │   ├── Abstractions/
│   │   │   ├── IBrowserEngine.cs        ← расширенный (без nint)
│   │   │   ├── IBrowserEngineFactory.cs
│   │   │   ├── ICdpEventSubscription.cs ← новый
│   │   │   ├── IMainThreadDispatcher.cs ← новый
│   │   │   ├── IPlayerManager.cs        ← новый
│   │   │   └── IPlayerContext.cs        ← новый
│   │   ├── Collections/RingBuffer.cs
│   │   ├── Models/              ← 11 файлов, без изменений
│   │   ├── Mcp/
│   │   │   ├── McpServer.cs     ← Dispatcher → IMainThreadDispatcher
│   │   │   ├── McpProtocol.cs   ← без изменений
│   │   │   ├── McpResult.cs     ← без изменений
│   │   │   ├── McpToolRegistry.cs
│   │   │   └── Tools/           ← 10 файлов: CoreWebView2 → IBrowserEngine
│   │   └── Services/
│   │       ├── CdpService.cs    ← CoreWebView2 → IBrowserEngine
│   │       ├── *EmulationService.cs (3) ← тот же рефакторинг
│   │       ├── *InterceptionService.cs (3) ← + ICdpEventSubscription
│   │       ├── *InjectionService.cs (2) ← IBrowserEngine
│   │       ├── QuickAuthService.cs     ← без изменений
│   │       └── TelegramInitDataService.cs ← без изменений
│   │
│   ├── GDD.Windows/            ← net8.0-windows, WPF (существующий)
│   │   ├── Engines/WebView2Engine.cs  ← + ICdpEventSubscription impl
│   │   ├── Platform/WpfDispatcher.cs  ← новый
│   │   ├── Interop/, Views/, ViewModels/, Themes/
│   │   └── App.xaml.cs
│   │
│   ├── GDD.Linux/              ← net8.0, standalone
│   │   ├── Engines/PlaywrightEngine.cs ← IBrowserEngine через Playwright
│   │   ├── Platform/
│   │   │   ├── ConsoleDispatcher.cs
│   │   │   └── HeadlessPlayerManager.cs
│   │   ├── Scripts/mcp-proxy.sh
│   │   └── Program.cs
│   │
│   └── GDD.macOS/              ← net8.0, standalone
│       ├── Engines/PlaywrightEngine.cs ← тот же (Playwright кроссплатформенный)
│       ├── Platform/
│       │   ├── MacDispatcher.cs
│       │   └── HeadlessPlayerManager.cs
│       ├── Scripts/mcp-proxy.sh
│       └── Program.cs
│
└── tests/GDD.Core.Tests/
```

---

## Фазы реализации

### Фаза 1 — GDD.Core extraction (7 дней)

Создание shared library. Самая критичная фаза.

**1.1 Новые абстракции** (Day 1-2)

```csharp
// IMainThreadDispatcher — замена WPF Dispatcher
interface IMainThreadDispatcher {
    Task InvokeAsync(Func<Task> action);
}

// ICdpEventSubscription — замена CoreWebView2.GetDevToolsProtocolEventReceiver
interface ICdpEventSubscription : IDisposable {
    event EventHandler<string> EventReceived; // JSON
}

// Расширение IBrowserEngine
interface IBrowserEngine : IAsyncDisposable {
    // Существующие методы (без nint!):
    Task InitializeAsync(object? hostHandle, string startUrl);  // ← nint → object?
    Task NavigateAsync(string url);
    Task<string> ExecuteJavaScriptAsync(string script);
    Task CallCdpMethodAsync(string method, string paramsJson);
    Task<string> CallCdpMethodWithResultAsync(string method, string paramsJson);
    Task<byte[]> CaptureScreenshotAsync();
    Task InjectScriptOnDocumentCreatedAsync(string script);
    // Новый:
    ICdpEventSubscription SubscribeToCdpEvent(string eventName);
}

// IPlayerManager — абстракция коллекции players для MCP tools
interface IPlayerManager {
    IReadOnlyList<IPlayerContext> Players { get; }
    IPlayerContext? GetPlayer(int playerId);
    IReadOnlyList<int> AddPlayers(int count, string? deviceName = null);
    void RemovePlayer(int playerId);
}

interface IPlayerContext {
    int PlayerId { get; }
    string PlayerName { get; }
    string CurrentUrl { get; set; }
    string StatusText { get; set; }
    IBrowserEngine? Engine { get; }
    DevicePreset SelectedDevice { get; set; }
    LocationPreset? SelectedLocation { get; set; }
    NetworkPreset SelectedNetwork { get; set; }
    // ... остальные свойства состояния
}
```

**1.2 Рефакторинг сервисов** (Day 2-4)

Ключевой файл — `CdpService.cs`. Текущий код:
```csharp
// БЫЛО:
public async Task CallAsync(CoreWebView2 webView, string method, object parameters)
// СТАЛО:
public async Task CallAsync(IBrowserEngine engine, string method, object parameters)
```
Каскадирует на: DeviceEmulationService, LocationEmulationService, NetworkEmulationService.

InterceptionService'ы — замена CDP event receiver:
```csharp
// БЫЛО (ConsoleInterceptionService):
webView.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled")
    .DevToolsProtocolEventReceived += (_, e) => { ... };
// СТАЛО:
engine.SubscribeToCdpEvent("Runtime.consoleAPICalled")
    .EventReceived += (_, json) => { ... };
```

InjectionService'ы — замена WebView2 API:
```csharp
// БЫЛО (TokenInjectionService):
await webView.ExecuteScriptAsync(script);
// СТАЛО:
await engine.ExecuteJavaScriptAsync(script);
```

**System.Drawing — 2 использования, убрать:**
- `WebView2Engine.cs:102` — `SetBounds(System.Drawing.Rectangle)` → оставить в GDD.Windows
- `OverlayWindow.xaml.cs:52` — `System.Drawing.Color` → оставить в GDD.Windows

**1.3 Рефакторинг MCP Tools** (Day 5-6)

Все 10 tool-файлов: `Register(registry, MainViewModel)` → `Register(registry, IPlayerManager, ...)`. 22 прямых обращения к CoreWebView2.

```csharp
// БЫЛО (NavigationTools.cs):
var player = mainVm.Players.FirstOrDefault(p => p.PlayerId == id);
player.WebView.CoreWebView2.Navigate(url);
// СТАЛО:
var player = playerManager.GetPlayer(id);
await player.Engine!.NavigateAsync(url);
```

**1.4 McpServer** (Day 5)
- Одна строка: `System.Windows.Application.Current.Dispatcher.InvokeAsync` → `_dispatcher.InvokeAsync`
- HttpListener с localhost prefix — работает на всех платформах, проверено

**1.5 GDD.Windows проект** (Day 6-7)
- Перенести Windows-specific код из BrowserXn в GDD.Windows
- `WpfDispatcher : IMainThreadDispatcher`
- `WebView2Engine` — добавить `SubscribeToCdpEvent` через `CoreWebView2DevToolsProtocolEventReceiver`
- `BrowserCellViewModel` — остаётся в GDD.Windows (WPF Brush, Window типы)
- **Проверка:** Windows-версия работает идентично

**Файлы для изменения (Phase 1):**
- `Abstractions/IBrowserEngine.cs` — расширение + `nint` → `object?`
- `Mcp/McpServer.cs` — 1 строка Dispatcher
- `Mcp/Tools/*.cs` — 10 файлов
- `Services/CdpService.cs` — ключевой, каскад на 6 сервисов
- `Services/*InterceptionService.cs` — 3 файла, CDP events
- `Services/*InjectionService.cs` — 2 файла
- `Engines/WebView2Engine.cs` — добавить SubscribeToCdpEvent

---

### Фаза 2 — PlaywrightEngine + Headless runner (5 дней)

**2.1 PlaywrightEngine : IBrowserEngine** (Day 8-11)

```csharp
public class PlaywrightEngine : IBrowserEngine
{
    private IPage _page;
    private IBrowserContext _context;
    
    public async Task InitializeAsync(object? hostHandle, string startUrl)
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        _context = await browser.NewContextAsync(new() { 
            UserAgent = "...",
            ViewportSize = new() { Width = 393, Height = 852 }
        });
        _page = await _context.NewPageAsync();
        
        // CDP client для прямых CDP вызовов
        _cdpSession = await _page.Context.NewCDPSessionAsync(_page);
    }

    public async Task CallCdpMethodAsync(string method, string paramsJson)
        => await _cdpSession.SendAsync(method, JsonDocument.Parse(paramsJson));

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
        => new PlaywrightCdpSubscription(_cdpSession, eventName);

    public async Task<byte[]> CaptureScreenshotAsync()
        => await _page.ScreenshotAsync();
}
```

**2.2 Headless console runner** (Day 11-12)
```csharp
// Program.cs
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) => {
        services.AddSingleton<IMainThreadDispatcher, ConsoleDispatcher>();
        services.AddSingleton<IBrowserEngineFactory, PlaywrightEngineFactory>();
        services.AddSingleton<IPlayerManager, HeadlessPlayerManager>();
        services.AddGDDCoreServices(ctx.Configuration);
    })
    .Build();

var mcpServer = host.Services.GetRequiredService<McpServer>();
mcpServer.Start();
Console.WriteLine($"GDD MCP server on http://localhost:{mcpServer.ActualPort}/mcp");
await host.WaitForShutdownAsync();
```

**2.3 ConsoleDispatcher** — без UI thread, просто выполняет на текущем SynchronizationContext.

**2.4 Тестирование** (Day 12)
- Сначала на Windows (валидация PlaywrightEngine без платформенных переменных)
- Все 25 MCP tools через HTTP
- Сравнение результатов с WebView2Engine

**Зависимости:**
```xml
<PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
```
Playwright скачивает Chromium при первом запуске: `playwright install chromium`

---

### Фаза 3 — Linux standalone (5 дней)

**3.1 Проект GDD.Linux** (Day 13-14)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <InvariantGlobalization>true</InvariantGlobalization> <!-- без ICU -->
  </PropertyGroup>
</Project>
```

**3.2 Платформенные нюансы Linux:**
- `InvariantGlobalization=true` — без ICU библиотеки (иначе crash на `CultureInfo`)
- Headless Chromium на Linux требует: `libnss3`, `libatk-bridge2.0-0`, `libdrm2`, `libxkbcommon0`, `libgbm1`
- Playwright автоматически устанавливает зависимости: `playwright install-deps chromium`
- HttpListener на localhost — работает без root
- `~/.local/share/GDD/Profiles/` — для данных

**3.3 mcp-proxy.sh** (Day 14)
```bash
#!/bin/bash
GDD_DIR="$(dirname "$0")"
GDD_PID=""

ensure_running() {
    if curl -s -o /dev/null -w "%{http_code}" \
       -X POST http://localhost:9700/mcp \
       -H "Content-Type: application/json" \
       -d '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}' \
       2>/dev/null | grep -q "200"; then
        return
    fi
    "$GDD_DIR/gdd-linux" --headless --port 9700 &
    GDD_PID=$!
    for i in $(seq 1 15); do
        sleep 1
        curl -s -o /dev/null http://localhost:9700/mcp 2>/dev/null && return
    done
}

ensure_running
while IFS= read -r line; do
    response=$(curl -s -X POST http://localhost:9700/mcp \
        -H "Content-Type: application/json" \
        -d "$line" 2>/dev/null)
    echo "$response"
done
```

**3.4 Упаковка** (Day 16-17)
- `dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true`
- Структура дистрибутива:
  ```
  gdd-linux-x64/
  ├── gdd-linux          ← self-contained executable
  ├── appsettings.json
  ├── GDD-MANUAL.md
  ├── mcp-proxy.sh       ← chmod +x
  └── install-deps.sh    ← playwright install chromium + apt install deps
  ```
- GitHub Release: `gdd-linux-x64.tar.gz`
- `install-deps.sh`:
  ```bash
  #!/bin/bash
  ./gdd-linux playwright install chromium
  # или для Ubuntu/Debian:
  sudo apt install -y libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libgbm1
  ```

---

### Фаза 4 — macOS standalone (5 дней)

**4.1 Проект GDD.macOS** (Day 18-19)

**Критические нюансы macOS:**
- **Gatekeeper** блокирует неподписанные приложения — минимум ad-hoc signing обязателен
- **Notarization** требуется для распространения (Apple Developer $99/год)
- **Apple Silicon (arm64)** — Playwright поддерживает нативно
- `.app` bundle обязателен для GUI, для консольного — просто executable
- `~/Library/Application Support/GDD/Profiles/` — для данных

**4.2 Universal binary** (Day 19)
```xml
<RuntimeIdentifiers>osx-x64;osx-arm64</RuntimeIdentifiers>
```
Два отдельных publish + `lipo` для объединения:
```bash
dotnet publish -r osx-x64 --self-contained -o publish/x64
dotnet publish -r osx-arm64 --self-contained -o publish/arm64
lipo -create publish/x64/gdd-macos publish/arm64/gdd-macos -output publish/gdd-macos
```

**4.3 Code signing** (Day 20)
```bash
# Ad-hoc (минимум для запуска):
codesign --force --deep --sign - gdd-macos

# Для распространения (Apple Developer):
codesign --force --deep --sign "Developer ID Application: ..." gdd-macos
xcrun notarytool submit gdd-macos.zip --apple-id ... --team-id ... --password ...
xcrun stapler staple gdd-macos
```

**4.4 mcp-proxy.sh** (Day 20) — идентичный Linux версии

**4.5 Упаковка** (Day 21-22)
- Для headless — tar.gz с executable (как Linux)
- Для GUI (будущее) — `.app` bundle + `.dmg`:
  ```
  GDD.app/
  └── Contents/
      ├── Info.plist
      ├── MacOS/gdd-macos
      ├── Resources/app.icns
      └── Frameworks/
  ```
- GitHub Release: `gdd-macos-universal.tar.gz`

---

### Фаза 5 — CI/CD и тестирование (3 дня)

**5.1 GitHub Actions:**
```yaml
strategy:
  matrix:
    include:
      - os: windows-latest
        rid: win-x64
        project: GDD.Windows
      - os: ubuntu-22.04
        rid: linux-x64
        project: GDD.Linux
      - os: macos-14        # Apple Silicon runner
        rid: osx-arm64
        project: GDD.macOS
```

Каждый job:
1. `dotnet restore` + `dotnet build`
2. `dotnet test` (GDD.Core.Tests)
3. `playwright install chromium` (Linux, macOS)
4. Integration test: запустить headless, послать MCP initialize + tools/list
5. `dotnet publish --self-contained`
6. Upload artifact + Release

**5.2 Тестирование:**
- **Unit (GDD.Core):** mock IBrowserEngine → все 25 tools, все services
- **Integration:** headless Playwright → реальный Chromium → MCP HTTP
- **Smoke per platform:** `initialize` → `tools/list` → `gdd_add_players` → `gdd_navigate` → `gdd_screenshot`

---

## Риски (уточнённые)

| Риск | Вероятность | Импакт | Митигация |
|------|------------|--------|-----------|
| Playwright CDP API отличается от WebView2 CDP | Средняя | Высокий | Phase 2 тестирование всех 25 tools |
| Playwright `CDPSession.SendAsync` не поддерживает все CDP методы | Низкая | Высокий | Fallback: прямой WebSocket к CDP endpoint |
| Headless Chromium на Linux требует X11/Wayland deps | Высокая | Средний | `install-deps.sh` + документация |
| macOS Gatekeeper блокирует unsigned app | Факт | Высокий | Ad-hoc signing минимум; notarization для дистрибуции |
| Playwright download Chromium (~150MB) при первом запуске | Факт | Средний | Бандлить Chromium в дистрибутив или pre-install скрипт |
| InvariantGlobalization ломает locale-dependent code | Низкая | Средний | Тестирование с различными locale |
| Apple Silicon + Rosetta fallback | Низкая | Низкий | Universal binary через lipo |

---

## Сроки

| Фаза | Дни | Что получаем |
|------|-----|-------------|
| 1. GDD.Core extraction | 7 | Shared library, Windows работает как раньше |
| 2. PlaywrightEngine + Headless | 5 | Headless runner, работает на Windows |
| 3. Linux standalone | 5 | `gdd-linux` headless + mcp-proxy.sh |
| 4. macOS standalone | 5 | `gdd-macos` universal headless + mcp-proxy.sh |
| 5. CI/CD + тесты | 3 | Автоматическая сборка и Release |
| **Итого** | **25 дней (~5 недель)** | Headless на всех платформах |

Фазы 3 и 4 параллелизуемы → **3.5 недели** при 2 разработчиках.

**Будущее (после v1.0 headless):**
- GUI Linux: CefGlue + GTK (4-6 недель)
- GUI macOS: CefGlue + AppKit (4-6 недель)

---

## Что НЕ меняется

- Все 25 MCP tools — та же функциональность
- MCP server — тот же HTTP протокол, тот же порт
- JSON-RPC 2.0 — без изменений
- appsettings.json — тот же формат
- `mcp-proxy` — аналогичная логика (auto-launch + proxy)
- Windows GUI — без изменений
