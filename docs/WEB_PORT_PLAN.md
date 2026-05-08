# GDD‑Web — план переноса в веб‑интерфейс с локальным запуском браузеров

> Этот документ — самодостаточный контекст для старта реализации в любом новом
> чате. Описывает текущее состояние GDD, архитектурное решение для веб‑версии
> и две параллельно реализуемые backend‑стратегии: **CLI‑агент** (системный
> Chrome через CDP) и **Docker** (Chromium в контейнерах). Обе подключаются
> через общий контракт `IBrowserEngine` и переключаются в настройках.

---

## 1. Что такое GDD сейчас

**GDD** (`src/BrowserXn/`, namespace `GDD`, AssemblyName `GDD.exe`) —
Windows WPF‑приложение на .NET 8 для одновременного запуска и оркестрации
нескольких изолированных браузерных инстансов («players») в одной
видеостене. Заточено под QA / multi‑user тестирование, в том числе Telegram
Mini Apps. Управляется как из UI, так и AI‑агентом через встроенный
**MCP‑сервер**.

### 1.1. Ключевые подсистемы

| Папка / файл | Назначение |
|---|---|
| `src/BrowserXn/App.xaml.cs` | Точка входа. Поднимает `IHost` (Generic Host), Serilog, регистрирует MCP‑тулы, стартует MCP‑сервер, открывает `MainWindow`. |
| `Abstractions/IBrowserEngine.cs` | Контракт для браузерного движка (есть, надо расширить). |
| `Abstractions/IBrowserEngineFactory.cs` | Фабрика движков по `playerId` + `userDataFolder`. |
| `Engines/WebView2Engine.cs` | Текущая реализация — Microsoft Edge WebView2 со своим `UserDataFolder`. |
| `Mcp/McpServer.cs` | HTTP‑сервер на `HttpListener`, поддерживает Streamable HTTP (`POST /mcp`) и SSE (`GET /sse` + `POST /message`). Маршалит вызовы тулов в UI Dispatcher. |
| `Mcp/McpToolRegistry.cs` | Реестр зарегистрированных тулов. |
| `Mcp/Tools/` | 9 групп MCP‑тулов: `PlayerTools`, `NavigationTools`, `InteractionTools`, `ReadTools`, `ExecutionTools`, `AuthTools`, `EmulationTools`, `StateTools`, `DiagnosticsTools`. |
| `Services/CdpService.cs` | Прямые CDP‑команды поверх WebView2. |
| `Services/QuickAuthService.cs` | Регистрация/логин N тестовых пользователей (`player{N}@gdd.test`) через бэкенд. |
| `Services/TokenInjectionService.cs` | Инжект auth‑токенов в WebView storage. |
| `Services/TelegramInitDataService.cs`, `TelegramInjectionService.cs` | Подделка Telegram WebApp `initData` под BotToken. |
| `Services/DeviceEmulationService.cs` | CDP `Emulation.setDeviceMetricsOverride`, user‑agent, touch. |
| `Services/LocationEmulationService.cs` | CDP `Emulation.setGeolocationOverride`. |
| `Services/NetworkEmulationService.cs` | CDP `Network.emulateNetworkConditions`. |
| `Services/ConsoleInterceptionService.cs` | Перехват `console.*` через CDP. |
| `Services/NotificationInterceptionService.cs` | Перехват push/notification API. |
| `Services/NetworkMonitoringService.cs` | Перехват сетевых запросов. |
| `Models/AppConfig.cs` | Конфиг (`FrontendUrl`, `BackendUrl`, `BotToken`, `McpPort=9700`, `DataFolderRoot`). |
| `Views/MainWindow.xaml` + `VideoWallPanel.cs` | Видеостена N ячеек, тулбар (Add Player, Quick Auth All, Navigate All). |
| `Views/BrowserCellControl.xaml` | Одна ячейка с WebView2. |
| `ViewModels/MainViewModel.cs`, `BrowserCellViewModel.cs` | MVVM‑биндинги через CommunityToolkit.Mvvm. |
| `appsettings.json` | `FrontendUrl=http://localhost:5173`, `BackendUrl=http://localhost:8080/api/v1`, `McpPort=9700`. |
| `mcp-proxy.ps1` | stdio↔HTTP мост для MCP‑клиентов, которые умеют только stdio. |

### 1.2. Что уже спроектировано правильно для миграции

- **`IBrowserEngine` + `IBrowserEngineFactory`** — абстракция движка уже
  выделена. Это идеальная точка вставки CLI и Docker реализаций.
- **MCP‑сервер транспорт‑агностичный** — JSON‑RPC поверх HTTP/SSE, не
  привязан к WPF.
- **`Services/*` работают через CDP** — переедут на любой backend, который
  даёт CDP WebSocket endpoint.
- **`AppConfig` через DI и `IConfiguration`** — добавление новых полей
  тривиально.

### 1.3. Что привязано к WPF и должно быть заменено

- `Views/*`, `ViewModels/*`, `App.xaml*`, `Themes/*`, `Converters/*` — UI
  слой целиком.
- `Engines/WebView2Engine.cs` — нативное встраивание WebView2 в HWND.
  Останется как третий движок для desktop‑версии (опционально), но не
  обязателен в Web.
- `Interop/DwmApi.cs` — Windows‑специфичная композиция окон. Не нужна.
- `McpServer.HandleRequest` маршалит в `Application.Current.Dispatcher` —
  это надо снять, перевести на обычный thread‑pool.

---

## 2. Цель: GDD‑Web

Перенести UI в веб‑интерфейс при сохранении принципа «**браузеры живут на
машине пользователя**». Никакого хостинга чужих сессий в облаке, никаких
видеостримов через интернет. Поддержать **два варианта запуска браузеров
одновременно**, переключаемых в настройках:

1. **CLI backend** — лёгкий локальный агент поднимает системный Chrome/Edge
   через CDP. Минимальная установка (~15 МБ, один бинарь). Целевая
   аудитория: indie/freelance QA, manual‑QA, корпоративные пользователи без
   Docker.

2. **Docker backend** — агент поднимает контейнеры с Chromium per player,
   подключается по CDP. Идеальная воспроизводимость, multi‑engine,
   реальная сетевая изоляция, parity с CI. Целевая аудитория:
   enterprise QA, команды с DevOps‑культурой, regression‑testing.

UI, бизнес‑логика, MCP — общие. Разница только в реализации
`IBrowserEngine`.

### 2.1. Чего **не** делаем

- Не хостим браузеры в облаке (отвергнутый вариант).
- Не используем iframe для тестируемого сайта (X‑Frame‑Options/CSP — не
  работает).
- Не ставим Playwright/Node.js в агент (избыточно — у нас уже есть свой
  CDP‑клиент в `CdpService`).
- Не делаем browser extension как основной путь (слабая изоляция, плашка
  `chrome.debugger`). Может быть добавлен позже как отдельный «zero install»
  бонус.

---

## 3. Целевая архитектура

```
┌──────────────────────────────────────────────────────────────────────┐
│  Пользовательская машина                                             │
│                                                                       │
│  ┌─ Браузер пользователя (или Photino shell) ──┐                     │
│  │  GDD Web UI (React + TS + Vite)             │                     │
│  │  - Видеостена N <canvas> с CDP screencast   │                     │
│  │  - Тулбар, настройки, MCP‑консоль           │                     │
│  └──────────────────────────────────────────────┘                     │
│         │ WebSocket ws://localhost:9700/ws                            │
│         │ HTTP /mcp, /sse, /files/*, /static/*                        │
│         ▼                                                             │
│  ┌─ GDD Agent (.NET 8 self‑contained, ~25 МБ AOT) ────────────────┐  │
│  │  - Kestrel HTTP/WebSocket сервер                                │  │
│  │  - Reuse: Mcp/, Services/, Models/, CdpService                  │  │
│  │  - IBrowserEngine factory: выбирает CLI или Docker per player   │  │
│  │  - Bridge: WS events ↔ CDP commands, screencast pump            │  │
│  └────────────────────────────────────────────────────────────────┘  │
│         │                                    │                        │
│         │ CDP WebSocket                       │ CDP WebSocket          │
│         ▼                                    ▼                        │
│  ┌─ ChromiumProcessEngine ───┐  ┌─ DockerEngine ────────────────┐   │
│  │ Системный chrome.exe       │  │ docker run gdd/chromium:128   │   │
│  │ --remote-debugging-port=0  │  │ --rm --memory=512m            │   │
│  │ --user-data-dir=...        │  │ -v profile:/profile           │   │
│  │ Профиль на диске юзера     │  │ Xvfb + headed Chromium        │   │
│  └────────────────────────────┘  └───────────────────────────────┘   │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

### 3.1. Распределение ответственности

**Web UI (React)**
- Рендерит сетку из `<canvas>`.
- Принимает кадры скринкаста (JPEG base64) по WS, рисует в canvas.
- Слушает `mousedown/up/move/wheel/keydown/keyup/paste/drop/...`,
  отправляет в агент как CDP `Input.*` события.
- UI настроек: выбор backend (CLI / Docker), per‑player эмуляция,
  device presets, MCP‑консоль с тестовыми вызовами тулов.
- Загружает файлы для upload‑сценариев через `POST /files/upload`.
- Получает download‑события и триггерит сохранение в браузере юзера.

**Agent (.NET 8)**
- Один self‑contained бинарь, AOT‑скомпилированный (~20–30 МБ).
- Слушает `localhost:9700`:
  - `GET /` — раздаёт встроенную React‑сборку.
  - `WS /ws` — основной канал с UI (JSON‑RPC).
  - `POST /mcp`, `GET /sse`, `POST /message` — MCP (как сейчас).
  - `POST /files/upload`, `GET /files/{id}` — загрузка/выдача файлов.
- Хостит `IBrowserEngine` instances per player.
- Реализует bridge: входящие UI‑команды → CDP, исходящие CDP‑события +
  screencast → UI.
- Работает headless (CLI) или с системным WebView через Photino (desktop
  shell mode) — это два режима **дистрибуции**, не два кодовых дерева.

**ChromiumProcessEngine (CLI backend)**
- Ищет системный браузер: Edge → Chrome → Chromium → Brave → fallback.
- Запускает `chrome.exe` с уникальным `--user-data-dir` и
  `--remote-debugging-port=0`.
- Читает реальный порт из `<user-data-dir>/DevToolsActivePort`.
- Подключается WebSocket'ом к `ws://localhost:<port>/devtools/browser/<id>`.
- Стартует `Page.startScreencast`, прокидывает кадры в UI.
- На остановке: `Browser.close` → kill процесса → опционально wipe
  user‑data‑dir.

**DockerEngine (Docker backend)**
- Проверяет наличие Docker daemon (Docker Desktop / Podman / Rancher).
- При первом запуске пуллит образ `gdd/chromium:<tag>` (наш образ).
- `docker run --rm --name gdd-player-<id> -v gdd-profile-<id>:/profile
  -p 0:9222 gdd/chromium:<tag>`.
- Docker сам мапит порт 9222 контейнера на свободный хостовый, читаем
  через `docker inspect`.
- CDP WebSocket к `ws://localhost:<mapped>/devtools/browser/...`.
- Volume `gdd-profile-<id>` живёт между запусками — это «профиль player'а».
- Snapshot: `docker run gdd/profile-saver -v <volume>:/in -v
  <snapshot-dir>:/out tar`. Restore — обратно.

### 3.2. Контракт `IBrowserEngine` (расширение)

Сейчас в `Abstractions/IBrowserEngine.cs` есть базовый набор. Нужно
расширить так, чтобы обе реализации легли чисто:

```csharp
public interface IBrowserEngine : IDisposable
{
    int PlayerId { get; }
    string UserDataFolder { get; }    // путь или volume name
    bool IsInitialized { get; }
    string CurrentUrl { get; }

    // Жизненный цикл
    Task InitializeAsync(BrowserStartOptions options, CancellationToken ct);
    Task NavigateAsync(string url);
    Task CloseAsync();

    // CDP endpoint для Services/* (CdpService будет цепляться сюда)
    string CdpWebSocketUrl { get; }

    // Скринкаст (управляется агентом, кадры публикуются через event)
    Task StartScreencastAsync(ScreencastOptions options);
    Task StopScreencastAsync();
    event EventHandler<ScreencastFrame> ScreencastFrameReceived;

    // Существующие события
    event EventHandler<NotificationEventArgs>? NotificationReceived;
    event EventHandler<string>? NavigationCompleted;
    event EventHandler<string>? TitleChanged;
}

public sealed record BrowserStartOptions(
    string StartUrl,
    Size? Viewport,
    string? UserAgent,
    string? ProxyServer,
    IReadOnlyDictionary<string, string>? ExtraEnv);

public sealed record ScreencastOptions(
    string Format = "jpeg",       // jpeg | png
    int Quality = 70,
    int? MaxWidth = null,
    int? MaxHeight = null,
    int EveryNthFrame = 1);

public sealed record ScreencastFrame(
    int PlayerId,
    string DataBase64,
    int SessionId,                // для Page.screencastFrameAck
    long TimestampMs,
    int Width, int Height);
```

Текущий `WebView2Engine` остаётся — он покрывает desktop‑режим (вариант A
из обсуждения), и его можно оставить как третью реализацию для пользователей
Windows, которые не хотят ни CLI, ни Docker.

### 3.3. Протокол UI ↔ Agent (WebSocket)

JSON‑RPC 2.0 поверх одного WS‑соединения. Совместим по форме с MCP.

**UI → Agent (методы):**

| Метод | Параметры | Возврат |
|---|---|---|
| `players.list` | — | `[{id, name, status, url, ...}]` |
| `players.add` | `{count, preset?}` | `[{id}]` |
| `players.remove` | `{id}` | `ok` |
| `players.navigate` | `{id, url}` | `ok` |
| `engine.setBackend` | `{kind: "cli"\|"docker", config}` | `ok` |
| `input.mouse` | `{playerId, type, x, y, button, modifiers, clickCount}` | `ok` |
| `input.key` | `{playerId, type, key, code, modifiers, text}` | `ok` |
| `input.wheel` | `{playerId, x, y, deltaX, deltaY}` | `ok` |
| `input.text` | `{playerId, text}` | `ok` (insertText, для IME) |
| `input.file` | `{playerId, fileChooserId, fileIds}` | `ok` |
| `screencast.start` | `{playerId, options}` | `ok` |
| `screencast.stop` | `{playerId}` | `ok` |
| `auth.quickAuth` | `{playerId}` | `{token, user}` |
| `emulation.device` | `{playerId, preset}` | `ok` |
| `emulation.geo` | `{playerId, lat, lng}` | `ok` |
| `emulation.network` | `{playerId, preset}` | `ok` |

**Agent → UI (события):**

| Событие | Данные |
|---|---|
| `screencast.frame` | `ScreencastFrame` (см. выше) |
| `player.status` | `{id, status, url}` |
| `player.consoleEntry` | `{id, level, message, source}` |
| `player.networkEntry` | `{id, request, response}` |
| `player.notification` | `{id, title, body}` |
| `player.fileChooser` | `{id, fileChooserId, mode}` |
| `player.download` | `{id, name, sizeBytes, fileId}` |

### 3.4. Файлы — потоки

- **Upload в страницу**: при `Page.fileChooserOpened` агент шлёт
  `player.fileChooser` в UI. UI открывает нативный `<input type=file>`,
  пользователь выбирает файл, UI делает `POST /files/upload`, получает
  `{fileId, agentPath}`, шлёт `input.file` с `agentPath`. Агент вызывает
  `DOM.setFileInputFiles`.
- **Download**: `Browser.setDownloadBehavior(allowAndName, dir)`,
  `Browser.downloadProgress` → `player.download` → UI делает `GET
  /files/{id}` и через `<a download>` отдаёт юзеру.
- **Drag‑and‑drop из ОС**: `<canvas>.ondrop` → upload в агент → инжект
  скрипта в страницу через `Page.addScriptToEvaluateOnNewDocument`
  (helper `window.__gddDropFile`) → синтетический `DragEvent`.
- **Paste файла из буфера**: симметрично drop'у через инжект скрипта.

Helper‑скрипт инжектится агентом в каждую новую страницу через
`Page.addScriptToEvaluateOnNewDocument`.

---

## 4. Технологический стек

### 4.1. Agent

- **Платформа**: .NET 8, AOT‑компиляция (`PublishAot=true`,
  `<InvariantGlobalization>true</InvariantGlobalization>` для размера).
- **Целевые рантаймы**: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`,
  `linux-x64`, `linux-arm64`.
- **HTTP/WS сервер**: ASP.NET Core minimal API + Kestrel, WebSocket через
  `app.UseWebSockets()`.
- **CDP клиент**: оставляем существующий `CdpService` подход (прямой
  `ClientWebSocket` + JSON‑RPC). Никакого Playwright/PuppeteerSharp.
- **Docker SDK**: `Docker.DotNet` (NuGet). Альтернатива — `dotnet shell`
  выполнение `docker` CLI; SDK безопаснее.
- **Лог**: Serilog (как сейчас).
- **DI/Config**: Generic Host (как сейчас).
- **Подпись**: Authenticode (Win), Apple notarization (Mac). Без подписи
  не выкатывать.

### 4.2. Frontend

- **React 18 + TypeScript + Vite**.
- **Стейт**: Zustand или Redux Toolkit (Zustand проще для размеров проекта).
- **Стили**: Tailwind CSS (быстро) или CSS Modules (контролируемо).
  Рекомендую Tailwind.
- **WebSocket**: одно постоянное соединение, реконнект с экспоненциальным
  бэкоффом.
- **Canvas**: `<canvas>` 2D context, рисуем `Image` из base64. Для v2 —
  WebGL с YUV‑декодированием от WebRTC.
- **Билдится в `agent/wwwroot`** и embed'ится в .NET бинарь как
  `EmbeddedResource` или раздаётся из disk при разработке.

### 4.3. Desktop shell (опционально)

Для пользователей, кто хочет «нативное окно, а не вкладка в браузере»:

- **Photino.NET** — тонкий .NET wrapper над системным WebView (WebView2
  на Win, WKWebView на Mac, WebKitGTK на Linux). ~5 МБ, не тянет
  Chromium. Открывает наш React UI и работает как обычный desktop‑app.
- Альтернатива: Electron (но тогда +120 МБ Chromium, не нужно).

Один и тот же agent‑бинарь запускается:
- **CLI mode**: `gdd-agent.exe` → tray icon, юзер открывает `localhost:9700`
  в любом браузере.
- **Desktop mode**: `gdd.exe` (Photino) → нативное окно с UI.

### 4.4. Docker образ (для DockerEngine)

Базовый Dockerfile (упрощённо):

```dockerfile
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y \
    chromium xvfb fonts-liberation \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 9222
COPY entrypoint.sh /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
# entrypoint: Xvfb :99 & DISPLAY=:99 chromium \
#   --remote-debugging-address=0.0.0.0 --remote-debugging-port=9222 \
#   --user-data-dir=/profile --no-sandbox --disable-dev-shm-usage ...
```

Образы: `gdd/chromium:128`, `gdd/firefox:latest`, `gdd/webkit:latest`
(последние два — фаза 4+).

---

## 5. Структура репозитория (целевая)

```
GDD/
├── docs/
│   └── WEB_PORT_PLAN.md          (этот файл)
├── src/
│   ├── BrowserXn/                (текущий WPF, остаётся как desktop‑mode)
│   ├── Gdd.Core/                 (новый: общая бизнес‑логика, Services/, Mcp/)
│   ├── Gdd.Engines.WebView2/     (выделить из BrowserXn)
│   ├── Gdd.Engines.ChromiumProcess/   (новый: CLI backend)
│   ├── Gdd.Engines.Docker/       (новый: Docker backend)
│   ├── Gdd.Agent/                (новый: ASP.NET Core host, точка входа web‑mode)
│   └── Gdd.Desktop/              (новый: Photino shell, тонкая обёртка над Agent)
├── web/
│   ├── package.json
│   ├── vite.config.ts
│   ├── src/
│   │   ├── App.tsx
│   │   ├── stores/
│   │   ├── components/
│   │   │   ├── VideoWall.tsx
│   │   │   ├── PlayerCell.tsx        (canvas + input handlers)
│   │   │   ├── Toolbar.tsx
│   │   │   ├── SettingsPanel.tsx
│   │   │   └── McpConsole.tsx
│   │   ├── transport/
│   │   │   └── ws-client.ts
│   │   └── types/                    (зеркало JSON‑RPC контрактов)
│   └── dist/                         (build output, embed в Gdd.Agent)
├── docker/
│   ├── chromium/
│   │   ├── Dockerfile
│   │   └── entrypoint.sh
│   └── firefox/                      (фаза 4)
├── scripts/
│   ├── build-agent.ps1
│   ├── build-web.ps1
│   └── package-installer.ps1
└── BrowserXn.sln                     (добавить новые проекты)
```

---

## 6. Фазировка реализации

Цель: оба backend'а делаются **одновременно**, потому что 80% кода у них
общее — расходятся только в реализации `IBrowserEngine`. Такой подход
даёт сразу полный охват аудитории.

### Фаза 0. Подготовка (1–2 дня)

- [ ] Создать solution‑структуру (`Gdd.Core`, `Gdd.Agent`, `Gdd.Engines.*`).
- [ ] Выделить из `src/BrowserXn/` в `Gdd.Core`: `Mcp/`, `Services/`,
      `Models/`, `Abstractions/`. Это **код, который не зависит от WPF**.
- [ ] Снять из `Mcp/McpServer.cs` зависимость от
      `Application.Current.Dispatcher` — заменить на `IDispatcher`
      абстракцию (в desktop‑режиме = WPF Dispatcher, в web‑режиме =
      thread‑pool / `Task.Run`).
- [ ] Расширить `IBrowserEngine` под новый контракт (см. §3.2).
- [ ] `WebView2Engine` адаптировать под новый контракт. Должен по‑прежнему
      работать в текущем десктопном UI без регрессии.

### Фаза 1. Agent skeleton + ChromiumProcessEngine + минимальный Web UI (1–2 нед)

- [ ] `Gdd.Agent`: ASP.NET Core minimal API, Kestrel на `localhost:9700`,
      WS endpoint `/ws`, переезд MCP endpoint'ов из `McpServer.cs`.
- [ ] `Gdd.Engines.ChromiumProcess`: запуск `chrome.exe`, CDP WebSocket,
      реализация `IBrowserEngine`.
- [ ] `web/`: React skeleton, WS клиент, одна `<canvas>` ячейка, базовые
      input handlers, кнопка «Add Player».
- [ ] CDP screencast pipeline: `Page.startScreencast` → агент → WS →
      canvas. С `screencastFrameAck` — обязательно, иначе CDP перестаёт
      слать кадры.
- [ ] Маппинг координат `canvas → viewport` (скейл‑фактор из размеров
      кадра).
- [ ] DoD: можно открыть `http://localhost:9700`, нажать «+», увидеть
      реальный браузер с openable example.com, кликать и вводить текст.

### Фаза 2. Перенос Services/Tools на новый transport (1–2 нед)

- [ ] `CdpService` подключается к `IBrowserEngine.CdpWebSocketUrl` —
      универсально для CLI и Docker.
- [ ] Все `Mcp/Tools/*.Register(...)` перенести на агент. UI получает
      возможность вызывать тулы через `/mcp` (это автоматически работает,
      так как MCP‑сервер уже мигрирован в фазе 1).
- [ ] Quick Auth, device emulation, geolocation, network throttling,
      console interception, network monitoring — все Services работают
      из агента.
- [ ] Telegram `initData` injection — то же самое.
- [ ] DoD: все 9 групп MCP‑тулов вызываются из MCP‑клиента (Claude
      Desktop / curl) и UI отражает изменения.

### Фаза 3. DockerEngine (1.5–2 нед, параллельно с фазой 2)

- [ ] `docker/chromium/Dockerfile` + `entrypoint.sh` (Xvfb + headed
      Chromium с `--remote-debugging-address=0.0.0.0`).
- [ ] Опубликовать образ в registry (GHCR / Docker Hub) — `gdd/chromium`.
- [ ] `Gdd.Engines.Docker`: через `Docker.DotNet` поднимает контейнер,
      резолвит mapped port, открывает CDP.
- [ ] Volume management для профилей: создание, восстановление,
      snapshot/restore (опционально для фазы 3, можно фазу 5).
- [ ] Health‑check Docker daemon при старте, понятная ошибка если нет.
- [ ] Сетевая изоляция per‑player через отдельные `docker network create`
      (для realistic proxy / VPN сценариев — фаза 5, но архитектурно
      заложить сейчас).
- [ ] UI: переключатель backend в настройках (CLI / Docker), per‑player
      выбор движка (Chromium‑proc / Chromium‑docker).
- [ ] DoD: с тем же UI можно поднять player в контейнере и
      взаимодействовать как с CLI‑player'ом. MCP‑тулы работают
      одинаково.

### Фаза 4. Файлы, IME, advanced input (1 нед)

- [ ] `Page.setInterceptFileChooserDialog` + UI диалог + upload pipe.
- [ ] `Browser.setDownloadBehavior` + download pipe.
- [ ] `Input.insertText` для не‑ASCII ввода и IME.
- [ ] Helper‑скрипт для drag‑drop файлов из ОС
      (`Page.addScriptToEvaluateOnNewDocument`).
- [ ] Paste картинки из буфера.
- [ ] DoD: можно загрузить файл в форму, сделать drag‑drop, скачать
      файл и получить его в браузере юзера.

### Фаза 5. Полировка + дистрибуция (1–2 нед)

- [ ] AOT‑сборка агента, тест размера и времени старта.
- [ ] Tray icon (CLI mode) — Win/Mac/Linux.
- [ ] `Gdd.Desktop` (Photino shell) для пользователей, кто хочет
      нативное окно.
- [ ] Auto‑update (агент проверяет GitHub releases / свой сервер).
- [ ] Code signing (Authenticode + Apple notarization).
- [ ] Инсталляторы: MSI (Win), DMG/PKG (Mac), DEB/RPM/AppImage (Linux).
- [ ] Snapshot/restore профилей (Docker volumes, native zip).
- [ ] Документация: README, getting started для CLI и Docker сценариев.

### Фаза 6+. Будущее (вне MVP)

- Запись/повтор действий → экспорт в Playwright‑скрипт.
- Ассершены и шаги (DSL).
- Отчёты с видео сессии, console/network логами.
- Визуальная регрессия.
- CI mode (headless, JUnit/Allure отчёты, status check на PR).
- Multi‑engine matrix: Firefox / WebKit Docker образы.
- WebRTC screencast вместо JPEG.
- Команды и шеринг сессий (требует серверной части — отдельная история).

---

## 7. Зафиксированные архитектурные решения

| Решение | Выбор | Почему |
|---|---|---|
| Язык агента | .NET 8 AOT | ~70% существующего кода реюзается. ~25 МБ self‑contained. |
| Не используем Playwright | Прямой CDP через `ClientWebSocket` | У нас уже есть `CdpService`. Минус 200+ МБ зависимостей. |
| HTTP сервер | Kestrel + ASP.NET Core minimal API | Стандарт .NET, WS из коробки, AOT‑совместим. |
| Frontend framework | React + TS + Vite | Самый широкий найм и экосистема. |
| Стейт | Zustand | Минимальный boilerplate. |
| Стили | Tailwind | Быстро итерировать UI. |
| Транспорт UI↔Agent | JSON‑RPC поверх WebSocket | Симметрично с MCP, единый формат. |
| Скринкаст v1 | CDP `Page.startScreencast` JPEG | Ноль зависимостей, работает везде. |
| Скринкаст v2 (после MVP) | WebRTC через локальный peer | Лучше FPS, дешевле CPU при 16+ ячейках. |
| Docker SDK | `Docker.DotNet` | Типизированный, не shell out. |
| Desktop shell | Photino.NET (опционально) | 5 МБ, использует системный WebView. |
| Сохраняем `WebView2Engine` | Да, как desktop‑backend | Текущая Windows‑аудитория не теряется. |
| Code signing | Обязательно перед релизом | Без него SmartScreen/Gatekeeper. |

---

## 8. Открытые вопросы, которые нужно решить по ходу

1. **Auth для агента**. Локальный `localhost:9700` доступен любому
   процессу на машине. Нужен ли token‑based auth между UI и агентом?
   Скорее да — простой shared secret в URL или header, генерируется при
   старте агента. Иначе любая открытая вкладка может подключиться.
2. **Пул прогретых браузеров** для CLI и Docker — делать сразу или
   позже? Скорее позже, в фазе 5.
3. **Telegram Desktop emulation**. Telegram TMA в реальном Telegram
   Desktop работает на Qt WebEngine. Нужно ли воспроизводить его
   user‑agent + специфическое окружение? Если да — добавить как
   `TelegramDesktopProfile` в `DeviceEmulationService`.
4. **Multi‑monitor для CLI mode**: при 16 native окнах — куда их класть?
   Этот вопрос отпадает в Web UI (всё в canvas), но если кто‑то хочет
   режим «native окна tiled» — это уже задача оконного менеджера.
5. **Snapshot формат для профилей**: tar.gz volume (Docker) vs zip папки
   (CLI). Хотим ли единый формат для портабельности между backends?
   Желательно да — тогда `gdd-profile.zip` со схемой, которую обе
   реализации умеют читать.
6. **MCP‑proxy**: оставить ли `mcp-proxy.ps1` или переписать на
   кросс‑платформенный bridge (Node? Go single binary?). Многие
   MCP‑клиенты ещё на stdio.

---

## 9. Что должен сделать assistant в новом чате

1. Прочитать этот документ и `src/BrowserXn/` для актуального состояния
   кода (особенно `Mcp/`, `Services/`, `Abstractions/`, `Engines/`).
2. Подтвердить план или задать уточняющие вопросы по §8.
3. Начать с **Фазы 0** — она разблокирует всё остальное.
4. Работать в ветке `claude/review-project-docs-eXqi1` (или новой
   фичевой), коммитить часто, маленькими атомарными изменениями.
5. Не ломать текущий WPF GDD до конца фазы 1 — оба должны собираться
   параллельно.

### 9.1. Команды для быстрой ориентации

```bash
# Структура
ls -la src/BrowserXn/

# Что уже абстрагировано
cat src/BrowserXn/Abstractions/IBrowserEngine.cs
cat src/BrowserXn/Abstractions/IBrowserEngineFactory.cs

# MCP сервер и тулы
ls src/BrowserXn/Mcp/Tools/
cat src/BrowserXn/Mcp/McpServer.cs

# Конфиг и точка входа
cat src/BrowserXn/App.xaml.cs
cat src/BrowserXn/appsettings.json

# Текущий движок
cat src/BrowserXn/Engines/WebView2Engine.cs
```

---

## 10. Глоссарий

- **Player** — одна изолированная браузерная сессия. В UI = одна ячейка
  видеостены.
- **Backend** — реализация `IBrowserEngine`. Сейчас: WebView2. Цель: +
  ChromiumProcess (CLI) + Docker.
- **Agent** — .NET процесс на машине пользователя, который слушает
  `localhost:9700`, оркестрирует players, проксирует MCP, мостит UI ↔ CDP.
- **CDP** — Chrome DevTools Protocol. JSON‑RPC поверх WebSocket. Уже
  используется в `CdpService`.
- **MCP** — Model Context Protocol. Уже реализован в `Mcp/McpServer.cs`
  для управления GDD из LLM‑агентов.
- **TMA** — Telegram Mini App. Один из ключевых юзкейсов GDD —
  тестирование TMA с подделкой `initData`.
- **Screencast** — поток JPEG/H264 кадров со страницы. CDP метод
  `Page.startScreencast` выдаёт base64‑JPEG'и.
- **CLI backend** — `ChromiumProcessEngine`, использует системный браузер.
- **Docker backend** — `DockerEngine`, контейнер per player.
