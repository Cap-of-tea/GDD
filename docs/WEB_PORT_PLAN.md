# GDD‑Web — план реализации (React + Go + Rust)

> Самодостаточный контекст для старта реализации в новом чате. GDD‑Web —
> это веб‑приложение для multi‑user QA‑тестирования и оркестрации
> браузерных сессий. Браузеры запускаются **на машине пользователя** через
> локальный Rust‑агент, не в облаке. Поддерживаются два backend'а
> одновременно: системный Chrome через CDP и Chromium в Docker‑контейнерах.
>
> Стек:
> - **Frontend** — React + TypeScript (видеостена, управление, MCP‑консоль).
> - **Cloud backend** — Go (аккаунты, проекты, пресеты, история, лицензии,
>   телеметрия, webhooks).
> - **Local agent** — Rust CLI (HTTP/WS на localhost, CDP мост, Docker
>   оркестрация, MCP сервер).

---

## 1. Текущее состояние (GDD на .NET, исходный продукт)

В `src/BrowserXn/` лежит **существующий desktop GDD** на WPF + .NET 8 +
WebView2. Он остаётся работоспособным как Windows‑desktop версия и
**служит референсом для портирования бизнес‑логики в Rust**, но напрямую
повторно не используется в GDD‑Web.

### 1.1. Что портируется из C# в Rust (логика, не код)

| C# модуль (референс) | Назначение | Rust‑эквивалент в агенте |
|---|---|---|
| `Mcp/McpServer.cs` | JSON‑RPC сервер: Streamable HTTP + SSE | `axum` route'ы `/mcp`, `/sse`, `/message` |
| `Mcp/McpToolRegistry.cs` | Реестр тулов | `HashMap<String, ToolFn>` + `inventory` для регистрации |
| `Mcp/Tools/*` (9 групп) | Player/Navigation/Interaction/Read/Execution/Auth/Emulation/State/Diagnostics | модули `tools/{player,navigation,...}.rs` |
| `Services/CdpService.cs` | CDP wrapper | `chromiumoxide` (high‑level) или raw `tokio-tungstenite` |
| `Services/QuickAuthService.cs` | Регистрация/логин N тестовых юзеров | `reqwest` + параллельный join |
| `Services/TokenInjectionService.cs` | Инжект токенов в storage | `Page.evaluate` через CDP |
| `Services/TelegramInitDataService.cs` | Подделка `initData` под BotToken | HMAC‑SHA256 в `hmac` + `sha2` crates |
| `Services/TelegramInjectionService.cs` | Инжект Telegram WebApp API | `Page.addScriptToEvaluateOnNewDocument` |
| `Services/DeviceEmulationService.cs` | `Emulation.setDeviceMetricsOverride` | прямой CDP вызов |
| `Services/LocationEmulationService.cs` | `Emulation.setGeolocationOverride` | прямой CDP вызов |
| `Services/NetworkEmulationService.cs` | `Network.emulateNetworkConditions` | прямой CDP вызов |
| `Services/ConsoleInterceptionService.cs` | Перехват `console.*` | подписка на CDP события + WS broadcast |
| `Services/NotificationInterceptionService.cs` | Перехват push/notification API | инжект скрипта + bridge |
| `Services/NetworkMonitoringService.cs` | Перехват сетевых запросов | подписка на `Network.*` |
| `Models/AppConfig.cs` | Конфиг (FrontendUrl, BotToken, McpPort, ...) | `serde` структуры из TOML |

Объём логики для портирования — около **3000 строк C#**, что в Rust
обычно укладывается в 4000–5000 строк (типы и enum'ы расширяются за счёт
явности). Реалистично — 4–6 недель работы одного разработчика, знающего
Rust и async.

### 1.2. Что выбрасывается совсем

`Views/`, `ViewModels/`, `App.xaml*`, `Themes/`, `Converters/`,
`VideoWallPanel.cs`, `Engines/WebView2Engine.cs`, `Interop/DwmApi.cs` —
WPF UI и Windows‑native composition. UI заменяется React‑приложением,
встраивание окон — CDP screencast.

### 1.3. Что остаётся как есть

`mcp-proxy.ps1` остаётся как stdio↔HTTP мост для MCP‑клиентов на stdio.
В будущем переписывается на кросс‑платформенный Rust‑бинарь (`gdd-mcp-bridge`).

---

## 2. Цель: GDD‑Web

Веб‑продукт с тремя компонентами:

1. **gdd.app (React SPA)** — UI на любом устройстве с современным
   браузером. Видеостена, управление players, настройки, MCP‑консоль,
   аккаунты, проекты, история запусков.
2. **api.gdd.app (Go backend)** — облачный сервис: аутентификация,
   организации, проекты, сохранённые пресеты и сценарии, история
   запусков, лицензии, телеметрия, webhooks для CI.
3. **gdd CLI (Rust agent)** — локальный бинарь на машине пользователя.
   Слушает `localhost:9700`, поднимает браузеры (системный Chrome или
   Docker‑контейнеры), мостит ввод/скринкаст в React UI, проксирует
   MCP, синхронизируется с облаком.

Принцип: **браузеры всегда живут на машине пользователя**. Облачный
backend хранит метаданные и конфигурации, но не хостит чужие сессии.

### 2.1. Поддерживаемые сценарии браузеров (через `BrowserEngine` trait в Rust)

- **CLI backend** (`ChromiumProcessEngine`) — системный Chrome/Edge с
  `--remote-debugging-port`, изолированный `--user-data-dir`. Минимум
  установки, нативная производительность.
- **Docker backend** (`DockerEngine`) — контейнеры с Chromium per
  player. Воспроизводимость, multi‑engine, реальная сетевая изоляция,
  parity с CI.

Оба backend'а — части одного Rust бинаря, переключаются в настройках
per‑project или per‑player.

### 2.2. Чего **не** делаем

- Не хостим браузеры в облаке (отвергнутый сценарий).
- Не используем iframe для тестируемого сайта (X‑Frame‑Options/CSP).
- Не используем Playwright/Node.js в агенте — прямой CDP через `chromiumoxide`.
- Не делаем browser extension основным путём (слабая изоляция,
  раздражающая плашка `chrome.debugger`). Может появиться позже как
  бонус‑дистрибуция.

---

## 3. Архитектура

```
┌────────── облако (наша инфра) ──────────────────────────────────────┐
│                                                                       │
│  ┌─ Static hosting ─────────────────┐    ┌─ Go API ─────────────────┐│
│  │  gdd.app                          │    │  api.gdd.app              ││
│  │  React SPA (Vite build)           │    │  axum/echo/chi на Go     ││
│  │  CDN / S3+CloudFront / Vercel     │    │  Postgres + Redis + S3   ││
│  └───────────────────────────────────┘    └───────────────────────────┘│
│              ▲                                       ▲                 │
└──────────────┼───────────────────────────────────────┼─────────────────┘
               │ HTTPS                                  │ HTTPS (REST + WS для live)
               │                                        │
               │                                        │
┌────────── машина пользователя ─────────────────────────────────────────┐
│              │                                        │                 │
│   ┌─ Браузер ▼ ────────────────────────────┐          │                 │
│   │  React SPA, загружен с gdd.app          │          │                 │
│   │   - Видеостена N <canvas>               │          │                 │
│   │   - WS клиент:                          │          │                 │
│   │     * к локальному агенту (live ввод)   │          │                 │
│   │     * к облаку (state, sync, accounts)  │          │                 │
│   └──────────┬──────────────────────────────┘          │                 │
│              │ WS wss://local.gdd.app:9700              │                 │
│              │ (резолвится в 127.0.0.1, см. §3.5)       │                 │
│              ▼                                          │                 │
│   ┌─ Rust agent (gdd CLI) ──────────────────────────────┴────────────┐  │
│   │  axum HTTP/WS server на localhost:9700                            │  │
│   │  modules:                                                          │  │
│   │   - transport/   (WS bridge UI ↔ engine, JSON‑RPC)                 │  │
│   │   - mcp/         (МСР сервер — портирован из C# Mcp/)             │  │
│   │   - tools/       (9 групп тулов — портирован из Mcp/Tools/)        │  │
│   │   - services/    (auth, telegram, emulation, etc.)                │  │
│   │   - engines/     (chromium_process, docker)                        │  │
│   │   - cdp/         (CDP клиент — chromiumoxide или raw)             │  │
│   │   - cloud/       (HTTPS клиент к api.gdd.app: auth, sync, telemetry)│  │
│   │   - files/       (upload/download bridge)                         │  │
│   │   - screencast/  (Page.startScreencast → WS)                      │  │
│   └──┬──────────────────────────┬─────────────────────────────────────┘  │
│      │ CDP                       │ Docker socket                          │
│      ▼                           ▼                                        │
│   ┌─ ChromiumProcessEngine ─┐ ┌─ DockerEngine ─────────────────────────┐ │
│   │ chrome.exe              │ │ docker run gdd/chromium:128 ...        │ │
│   │ --remote-debugging-port │ │ Xvfb + headed Chromium в контейнере   │ │
│   │ --user-data-dir=...     │ │ volume gdd-profile-N для профиля       │ │
│   └─────────────────────────┘ └────────────────────────────────────────┘ │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

### 3.1. Frontend (React) — ответственность

- Рендерит сетку из `<canvas>` для каждого player.
- Принимает кадры скринкаста (JPEG base64) от Rust агента по WS, рисует
  в canvas. Скейл‑фактор для координат.
- Слушает `mousedown/up/move/wheel/keydown/paste/drop/...`, отправляет
  как CDP `Input.*` события через локальный WS.
- Управление: добавить/удалить player, навигация, Quick Auth All,
  device/geo/network presets, MCP‑консоль.
- Аккаунт: логин в облако, выбор организации/проекта, сохранение
  сессий и пресетов в облако.
- Загружает файлы для upload‑сценариев через `POST /files/upload`
  (агент или облако в зависимости от сценария).
- Получает download‑события и триггерит сохранение в браузере юзера.

Два WS‑соединения параллельно:
- `wss://local.gdd.app:9700/ws` — к локальному Rust агенту (low‑latency,
  ввод и скринкаст).
- `wss://api.gdd.app/ws/live` — к облачному Go (push‑уведомления,
  изменения проекта в команде, статусы).

### 3.2. Cloud backend (Go) — ответственность

| Домен | Что хранит / делает |
|---|---|
| Accounts | пользователи, организации, инвайты, RBAC, OAuth (Google/GitHub) |
| Projects | проекты, окружения, секреты (зашифрованные KMS) |
| Presets | сохранённые device/geo/network пресеты, профили устройств |
| Scenarios | (фаза 6+) записанные сценарии тестов |
| Runs | история запусков players, длительность, статусы, ошибки |
| Recordings | (фаза 6+) видео сессий, console/network логи |
| Licenses | проверка лицензий, тарифные лимиты, биллинг (Stripe webhook) |
| Telemetry | анонимная статистика использования, error reporting |
| Webhooks | приём из CI (GitHub Actions / GitLab) для запуска сценариев |
| Updates | endpoint для агента: проверка версий, выдача release notes |
| Notifications | email (Postmark/SES), Slack, webhook outbound |

Backend **не хостит браузеры**, **не получает screencast**, **не
проксирует ввод**. Это всё локально между React и Rust агентом.

### 3.3. Local agent (Rust) — ответственность

- Слушает `localhost:9700`:
  - `WS /ws` — основной канал с UI, JSON‑RPC.
  - `POST /mcp`, `GET /sse`, `POST /message` — MCP сервер.
  - `POST /files/upload`, `GET /files/{id}` — файловый bridge.
  - `GET /health`, `GET /version` — служебные.
- Хостит N инстансов `BrowserEngine` (CLI или Docker per player).
- Bridge: WS события UI → CDP `Input.*`, CDP screencast → WS UI.
- Опционально пингует облако: проверка лицензии, push телеметрии,
  pull сохранённых пресетов.
- Self‑update через GitHub Releases (или endpoint облака).
- Tray icon в системе (Win/Mac/Linux) для запуска/остановки.
- Кросс‑платформа: Win/Mac/Linux × x64/arm64.

### 3.4. Контракт `BrowserEngine` (Rust trait)

```rust
#[async_trait]
pub trait BrowserEngine: Send + Sync {
    fn player_id(&self) -> i32;
    fn user_data_folder(&self) -> &str;
    fn is_initialized(&self) -> bool;
    fn current_url(&self) -> &str;

    async fn initialize(&mut self, opts: BrowserStartOptions) -> Result<()>;
    async fn navigate(&mut self, url: &str) -> Result<()>;
    async fn close(&mut self) -> Result<()>;

    fn cdp_ws_url(&self) -> &str;

    async fn start_screencast(&mut self, opts: ScreencastOptions) -> Result<()>;
    async fn stop_screencast(&mut self) -> Result<()>;

    fn screencast_rx(&self) -> tokio::sync::broadcast::Receiver<ScreencastFrame>;
    fn events_rx(&self) -> tokio::sync::broadcast::Receiver<EngineEvent>;
}

#[derive(Debug, Clone)]
pub struct BrowserStartOptions {
    pub start_url: String,
    pub viewport: Option<(u32, u32)>,
    pub user_agent: Option<String>,
    pub proxy_server: Option<String>,
    pub extra_env: HashMap<String, String>,
}

#[derive(Debug, Clone)]
pub struct ScreencastOptions {
    pub format: ScreencastFormat,    // Jpeg | Png
    pub quality: u8,
    pub max_width: Option<u32>,
    pub max_height: Option<u32>,
    pub every_nth_frame: u32,
}

#[derive(Debug, Clone)]
pub struct ScreencastFrame {
    pub player_id: i32,
    pub data_base64: String,
    pub session_id: i32,
    pub timestamp_ms: i64,
    pub width: u32,
    pub height: u32,
}

#[derive(Debug, Clone)]
pub enum EngineEvent {
    NavigationCompleted(String),
    TitleChanged(String),
    Notification { title: String, body: String },
    FileChooserOpened { id: String, mode: FileChooserMode },
    Download { name: String, size: u64, file_id: String },
    Console { level: String, message: String, source: String },
    Network { /* ... */ },
}
```

Реализации:
- `engines/chromium_process.rs` — `impl BrowserEngine for ChromiumProcessEngine`
- `engines/docker.rs` — `impl BrowserEngine for DockerEngine`

### 3.5. Сетевой / TLS трюк для `https://gdd.app` ↔ `ws://localhost:9700`

Браузеры **блокируют** WebSocket из HTTPS‑страницы на `ws://localhost`
(mixed content). Решения, в порядке предпочтения:

1. **`local.gdd.app` → 127.0.0.1 + wildcard cert** (паттерн Plex,
   Tailscale Funnel, Jellyfin).
   - DNS A‑запись `local.gdd.app` → `127.0.0.1` (публичный DNS).
   - Wildcard cert `*.local.gdd.app` от Let's Encrypt, выпускается на
     наш домен. Приватный ключ отдаётся агенту при установке через
     облачный backend (или встраивается в подписанный бинарь —
     security tradeoff обсуждается).
   - Агент слушает `wss://local.gdd.app:9700`. Браузер видит валидный
     HTTPS на localhost.
2. **Self‑signed cert + auto‑trust в OS keystore** при установке агента.
   Раздражает security‑policy в корпоратах, но работает локально.
3. **Агент сам отдаёт React build по HTTP** — пользователь открывает
   `http://localhost:9700`, без cloud frontend. Безопасно, но без
   облачных фич (логин, проекты).

Для MVP — **режим 3 (local‑only)**. Cloud‑mode с режимом 1 — фаза 5+.

### 3.6. Протокол UI ↔ Local Agent (WebSocket)

JSON‑RPC 2.0 поверх одного WS. Совместим по форме с MCP (одинаковый
парсер).

**UI → Agent:**

| Метод | Параметры |
|---|---|
| `players.list` | — |
| `players.add` | `{count, preset?, engine: "cli"|"docker"}` |
| `players.remove` | `{id}` |
| `players.navigate` | `{id, url}` |
| `engine.setDefault` | `{kind, config}` |
| `input.mouse` | `{playerId, type, x, y, button, modifiers, clickCount}` |
| `input.key` | `{playerId, type, key, code, modifiers}` |
| `input.wheel` | `{playerId, x, y, deltaX, deltaY}` |
| `input.text` | `{playerId, text}` (для IME / не‑ASCII) |
| `input.file` | `{playerId, fileChooserId, fileIds}` |
| `screencast.start` | `{playerId, options}` |
| `screencast.stop` | `{playerId}` |
| `auth.quickAuth` | `{playerId}` |
| `emulation.device` | `{playerId, preset}` |
| `emulation.geo` | `{playerId, lat, lng}` |
| `emulation.network` | `{playerId, preset}` |

**Agent → UI (события):**

| Событие | Данные |
|---|---|
| `screencast.frame` | `ScreencastFrame` |
| `player.status` | `{id, status, url}` |
| `player.consoleEntry` | `{id, level, message, source}` |
| `player.networkEntry` | `{id, request, response}` |
| `player.notification` | `{id, title, body}` |
| `player.fileChooser` | `{id, fileChooserId, mode}` |
| `player.download` | `{id, name, sizeBytes, fileId}` |

### 3.7. Протокол UI ↔ Cloud (REST + WS)

REST `api.gdd.app/v1/...`:

```
POST   /auth/login           → JWT
POST   /auth/oauth/{provider}
GET    /me
GET    /orgs
GET    /orgs/{id}/projects
POST   /projects/{id}/presets
GET    /projects/{id}/presets
GET    /projects/{id}/runs
POST   /projects/{id}/runs        (опционально, для CI)
GET    /runs/{id}/recording       (фаза 6+)
GET    /agent/latest              (release info для self‑update)
POST   /telemetry                 (батч событий от агента)
POST   /webhooks/github           (CI integration)
POST   /webhooks/stripe           (биллинг)
```

WS `wss://api.gdd.app/ws/live`:
- push‑уведомления по проекту (кто‑то в команде поменял пресет),
- статусы запусков из CI,
- license expiry warnings.

### 3.8. Файлы и их потоки

- **Upload в страницу**: при `Page.fileChooserOpened` агент шлёт
  `player.fileChooser` в UI. UI открывает нативный
  `<input type=file>`, юзер выбирает файл, UI делает
  `POST /files/upload` к **локальному агенту** (не облаку), получает
  `{fileId, agent_path}`, шлёт `input.file`. Агент вызывает CDP
  `DOM.setFileInputFiles`.
- **Download**: `Browser.setDownloadBehavior(allowAndName, dir)`,
  `Browser.downloadProgress` → `player.download` → UI делает
  `GET /files/{id}` от локального агента → `<a download>` отдаёт юзеру.
- **Drag‑drop из ОС**: `<canvas>.ondrop` → upload в агент → инжект
  helper‑скрипта (`window.__gddDropFile`) → синтетический `DragEvent`
  в странице.
- **Paste файла**: симметрично drag‑drop.

Helper‑скрипт инжектится агентом через
`Page.addScriptToEvaluateOnNewDocument` в каждую новую страницу.

---

## 4. Технологический стек

### 4.1. Frontend (`web/`)

| Что | Выбор | Почему |
|---|---|---|
| Framework | React 18 + TypeScript | Самый широкий найм и экосистема |
| Bundler | Vite 5 | Быстрый dev, ESM, хороший DX |
| State | Zustand | Минимальный boilerplate, в отличие от Redux |
| Routing | React Router 6 (data router) | Стандарт, file‑based есть в Tanstack но overkill |
| Стили | Tailwind CSS | Быстрая итерация UI |
| Components | Radix UI primitives + кастом | Доступные нестилизованные блоки |
| Forms | React Hook Form + Zod | Самая частая комбинация в TS‑экосистеме |
| API клиент | TanStack Query (REST) + кастом WS клиент | Стандарт |
| Графика | `<canvas>` 2D context | Достаточно для JPEG screencast в MVP |
| Тесты | Vitest + Testing Library + Playwright | Стандарт |

Хостинг: Vercel / Cloudflare Pages / S3+CloudFront — любой статический
host. CSP должна разрешать `connect-src wss://local.gdd.app:9700` и
`wss://api.gdd.app`.

### 4.2. Cloud backend (`api/`)

| Что | Выбор | Почему |
|---|---|---|
| Язык | Go 1.22+ | Простой, быстрый, easy ops |
| HTTP framework | `chi` | Минимальный, идиоматичный, без магии |
| Альтернативно | `echo` или `fiber` | Если нужны batteries‑included |
| DB | PostgreSQL 16 | Стандарт |
| DB layer | `sqlc` (generated typed queries) | Лучшее сочетание safety/perf |
| Альтернативно | `sqlx` | Если хочется ad‑hoc SQL |
| Migrations | `goose` | Простой, стабильный |
| Auth | `golang-jwt/jwt` + OAuth2 (`coreos/go-oidc`) | Стандартно |
| Cache / queues | Redis + `redis/go-redis` | Кэш, rate limit, pub/sub |
| Background jobs | `hibiken/asynq` | Redis‑backed, удобный UI |
| Storage | S3‑compatible (`aws-sdk-go-v2`) | Универсально, MinIO для self‑host |
| Validation | `go-playground/validator` | Стандарт |
| Logging | `log/slog` (stdlib) | Структурированные логи без зависимостей |
| Tracing | OpenTelemetry (`go.opentelemetry.io/otel`) | Стандарт |
| Email | Postmark / SES через шаблоны | По вкусу |
| Stripe | `stripe-go/v76` | Биллинг |
| Config | `kelseyhightower/envconfig` | 12‑factor |
| Tests | stdlib `testing` + `testcontainers-go` | Интеграционные с Postgres |
| Build | многоэтапный Dockerfile, distroless | Минимальный образ |
| Deploy | Kubernetes (helm chart) или Fly.io | По нагрузке |

Структура внутри `api/`:

```
api/
├── cmd/
│   └── api/
│       └── main.go
├── internal/
│   ├── http/         (handlers, middleware, router)
│   ├── auth/
│   ├── orgs/
│   ├── projects/
│   ├── presets/
│   ├── runs/
│   ├── licenses/
│   ├── telemetry/
│   ├── webhooks/
│   ├── billing/
│   ├── storage/      (Postgres, Redis, S3 wrappers)
│   └── platform/     (config, logging, otel, errors)
├── migrations/       (goose SQL migrations)
├── sqlc.yaml
├── go.mod
├── Dockerfile
└── README.md
```

### 4.3. Local agent (`agent/`)

| Что | Выбор | Почему |
|---|---|---|
| Язык | Rust stable (2021 edition, 1.78+) | Минимальный бинарь, max performance, нет GC |
| Async runtime | `tokio` | Стандарт |
| HTTP/WS server | `axum` 0.7 | Идиоматичный, на `tower`, `hyper` |
| WS client | `tokio-tungstenite` | Стандарт для CDP WS |
| CDP клиент | `chromiumoxide` (high‑level) | Playwright‑like API, обширное покрытие CDP |
| Альтернативно для CDP | raw `tokio-tungstenite` + `serde_json` | Если нужно полностью контролировать low‑level |
| Docker SDK | `bollard` | Активный, типизированный |
| Process control | `tokio::process::Command` | stdlib async |
| Сериализация | `serde` + `serde_json` + `serde_with` | Стандарт |
| Конфиг | `toml` + `serde` | Читаемый формат |
| HTTP клиент | `reqwest` (rustls feature) | Для облачного API |
| Logging | `tracing` + `tracing-subscriber` | Структурированные логи, файлы и stdout |
| Errors | `thiserror` (для библиотек) + `anyhow` (для main) | Стандартная пара |
| Embed React build | `rust-embed` | React build встраивается в бинарь для local‑mode |
| Tray icon | `tray-icon` (cross‑platform) | Win/Mac/Linux |
| Cross‑compile | `cargo` + `rustup target add` + `cross` | Тривиально |
| Сборка релизов | GitHub Actions matrix | Стандарт |
| Подпись | Authenticode (Win), notarytool (Mac) | Обязательно для дистрибуции |
| Распространение | подписанные бинари в GitHub Releases + auto‑update | self‑update через `self_update` crate |
| Tests | stdlib + `tokio::test` + `wiremock` для HTTP | Стандарт |

Структура `agent/`:

```
agent/
├── Cargo.toml
├── Cargo.lock
├── build.rs                 (embed React, версия из git)
├── src/
│   ├── main.rs              (CLI args, запуск runtime)
│   ├── config.rs
│   ├── transport/
│   │   ├── mod.rs
│   │   ├── ws.rs            (UI ↔ agent JSON‑RPC)
│   │   └── jsonrpc.rs       (общие типы, зеркало MCP)
│   ├── mcp/
│   │   ├── mod.rs
│   │   ├── server.rs        (Streamable HTTP + SSE, портирован из McpServer.cs)
│   │   ├── registry.rs
│   │   └── types.rs
│   ├── tools/               (9 модулей, портированы из Mcp/Tools/)
│   │   ├── player.rs
│   │   ├── navigation.rs
│   │   ├── interaction.rs
│   │   ├── read.rs
│   │   ├── execution.rs
│   │   ├── auth.rs
│   │   ├── emulation.rs
│   │   ├── state.rs
│   │   └── diagnostics.rs
│   ├── services/            (портированы из Services/)
│   │   ├── quick_auth.rs
│   │   ├── token_injection.rs
│   │   ├── telegram.rs
│   │   ├── device_emulation.rs
│   │   ├── location_emulation.rs
│   │   ├── network_emulation.rs
│   │   ├── console_interception.rs
│   │   ├── network_monitoring.rs
│   │   └── notification_interception.rs
│   ├── engines/
│   │   ├── mod.rs           (trait BrowserEngine)
│   │   ├── chromium_process.rs   (CLI backend)
│   │   ├── docker.rs             (Docker backend)
│   │   └── browser_finder.rs     (поиск системного Chrome/Edge per OS)
│   ├── cdp/
│   │   ├── mod.rs
│   │   └── client.rs        (low‑level wrapper, если не chromiumoxide)
│   ├── cloud/
│   │   ├── mod.rs
│   │   ├── client.rs        (REST к api.gdd.app)
│   │   ├── auth.rs
│   │   ├── telemetry.rs
│   │   └── updates.rs
│   ├── files/
│   │   ├── mod.rs
│   │   ├── upload.rs
│   │   └── download.rs
│   ├── screencast/
│   │   ├── mod.rs
│   │   └── pump.rs
│   ├── tray/
│   │   ├── mod.rs
│   │   └── platform.rs
│   └── web/                 (опц. embed React build для local‑mode)
│       └── mod.rs
└── tests/
    ├── e2e_chromium_process.rs
    └── e2e_docker.rs
```

### 4.4. Docker‑образ для DockerEngine (`docker/chromium/`)

```dockerfile
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends \
      chromium \
      xvfb \
      fonts-liberation \
      libnss3 libxss1 libasound2 \
      ca-certificates \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 9222
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
```

`entrypoint.sh`:

```bash
#!/bin/sh
Xvfb :99 -screen 0 1920x1080x24 &
export DISPLAY=:99
exec chromium \
  --no-sandbox \
  --disable-dev-shm-usage \
  --remote-debugging-address=0.0.0.0 \
  --remote-debugging-port=9222 \
  --user-data-dir=/profile \
  --no-first-run \
  "$@"
```

Образы публикуются в GHCR: `ghcr.io/<org>/gdd-chromium:<chromium-version>`.

---

## 5. Структура репозитория

```
GDD/
├── docs/
│   └── WEB_PORT_PLAN.md         (этот файл)
├── src/
│   └── BrowserXn/               (легаси .NET WPF GDD, остаётся как desktop‑Win версия и референс)
├── web/                         (React frontend)
│   ├── package.json
│   ├── vite.config.ts
│   ├── tailwind.config.ts
│   ├── tsconfig.json
│   ├── index.html
│   ├── src/
│   │   ├── main.tsx
│   │   ├── App.tsx
│   │   ├── routes/
│   │   ├── stores/              (Zustand)
│   │   ├── components/
│   │   │   ├── VideoWall/
│   │   │   │   ├── VideoWall.tsx
│   │   │   │   ├── PlayerCell.tsx     (canvas + input handlers)
│   │   │   │   └── coords.ts          (canvas → viewport mapping)
│   │   │   ├── Toolbar.tsx
│   │   │   ├── SettingsPanel.tsx
│   │   │   ├── McpConsole.tsx
│   │   │   └── auth/
│   │   ├── transport/
│   │   │   ├── localAgent.ts          (WS клиент к Rust)
│   │   │   └── cloud.ts               (REST + WS к Go)
│   │   ├── types/                     (shared с агентом и backend'ом)
│   │   └── utils/
│   └── tests/
├── api/                         (Go cloud backend, см. §4.2)
├── agent/                       (Rust local agent, см. §4.3)
├── docker/
│   └── chromium/
│       ├── Dockerfile
│       └── entrypoint.sh
├── shared/                      (опц. shared schemas)
│   ├── jsonrpc.schema.json
│   └── README.md
├── scripts/
│   ├── build-all.sh
│   ├── package-agent-win.ps1
│   ├── package-agent-mac.sh
│   └── package-agent-linux.sh
├── .github/
│   └── workflows/
│       ├── web-build.yml
│       ├── api-build.yml
│       ├── agent-build.yml          (matrix: win/mac/linux × x64/arm64)
│       └── release.yml
├── BrowserXn.sln                (для легаси)
├── README.md
└── LICENSE
```

---

## 6. Фазировка реализации

Принцип: оба browser backend'а (CLI и Docker) делаются параллельно через
`BrowserEngine` trait. Frontend и cloud backend разрабатываются
параллельно с агентом.

### Фаза 0. Подготовка (3–5 дней)

- [ ] Создать структуру репо (`web/`, `api/`, `agent/`, `docker/`).
- [ ] Свежий Cargo workspace в `agent/`.
- [ ] Каркас Vite+React+TS в `web/` с роутингом и заглушкой видеостены.
- [ ] Каркас Go в `api/` с health‑check и одним эндпоинтом.
- [ ] Зафиксировать схему JSON‑RPC в `shared/jsonrpc.schema.json` (или
      генерировать из Rust через `schemars`).
- [ ] Базовый CI: lint и тесты для каждой части.

### Фаза 1. Rust agent — skeleton + ChromiumProcessEngine + JPEG screencast (1.5–2 нед)

- [ ] `agent/`: `axum` HTTP/WS server на `localhost:9700`.
- [ ] `engines/chromium_process.rs`: поиск браузера, спавн с
      `--remote-debugging-port=0`, чтение `DevToolsActivePort`,
      подключение к CDP WS.
- [ ] `cdp/client.rs` (или `chromiumoxide`): базовый wrapper.
- [ ] `screencast/pump.rs`: `Page.startScreencast` + ack +
      broadcast в WS.
- [ ] `transport/ws.rs`: JSON‑RPC, методы `players.add/remove/list/navigate`,
      `input.mouse/key/wheel/text`, `screencast.start/stop`.
- [ ] Обработка `Browser.close` и cleanup `user-data-dir` опционально.
- [ ] **DoD**: `cargo run`, открываешь `http://localhost:9700` → React
      stub с одной ячейкой, видишь живой браузер, кликаешь, печатаешь.

### Фаза 2. Web UI — продвинутый (1.5–2 нед, параллельно с фазой 1)

- [ ] Видеостена с CSS Grid (`VideoWall.tsx`), responsive.
- [ ] `PlayerCell.tsx`: canvas с draw loop, input handlers, координатное
      масштабирование.
- [ ] WS клиент с реконнектом и backpressure.
- [ ] Toolbar: Add Player, Quick Auth All, Navigate All, URL bar.
- [ ] SettingsPanel: per‑player engine selection (CLI/Docker), device
      preset, geo, network.
- [ ] McpConsole: список доступных тулов, форма вызова, JSON‑ответ.
- [ ] **DoD**: 16 ячеек, плавный ввод во всех, выбор Docker engine
      переключает backend.

### Фаза 3. DockerEngine + multi‑engine (1.5–2 нед, параллельно)

- [ ] `docker/chromium/Dockerfile` + `entrypoint.sh`, билд и push в GHCR.
- [ ] `engines/docker.rs`: `bollard`, поднять контейнер, резолвить
      mapped port, открыть CDP.
- [ ] Volume management для профилей (`gdd-profile-<id>`), создание,
      персистентность между запусками.
- [ ] Health‑check Docker daemon при старте, понятная ошибка если нет
      или неподдерживаемая версия.
- [ ] Docker socket per‑OS detection: `unix:///var/run/docker.sock` /
      `npipe://./pipe/docker_engine`. `bollard` это умеет.
- [ ] (Опц.) Сетевые namespaces per‑player для realistic proxy/VPN
      сценариев — заложить контракт, реализация позже.
- [ ] **DoD**: с тем же UI можно выбрать Docker engine, поднять
      player в контейнере, всё взаимодействие работает идентично CLI.

### Фаза 4. Перенос Services и MCP тулов (2 нед)

- [ ] Портировать в `agent/services/`: QuickAuth, TokenInjection,
      Telegram (`initData` HMAC + injection), DeviceEmulation,
      LocationEmulation, NetworkEmulation, ConsoleInterception,
      NetworkMonitoring, NotificationInterception.
- [ ] Портировать `agent/mcp/server.rs` (Streamable HTTP + SSE) и
      реализовать 9 групп тулов в `agent/tools/*`.
- [ ] Helper‑скрипты для drag/drop файлов и paste —
      `Page.addScriptToEvaluateOnNewDocument`.
- [ ] **DoD**: Claude Desktop подключается к `localhost:9700/mcp`,
      все тулы работают, эмуляция устройств/гео/сети применяется,
      Telegram TMA с подделанным `initData` открывается.

### Фаза 5. Файлы, IME, advanced input (1 нед)

- [ ] `Page.setInterceptFileChooserDialog` + UI диалог + upload.
- [ ] `Browser.setDownloadBehavior` + download bridge.
- [ ] `Input.insertText` для не‑ASCII / IME.
- [ ] Drag‑and‑drop файлов из ОС.
- [ ] Paste файла из буфера.
- [ ] **DoD**: загрузка файла в форму, drag‑drop из ОС, скачивание
      файла, всё работает в обоих backend'ах.

### Фаза 6. Cloud backend MVP (3–4 нед, можно параллельно с 4–5)

- [ ] Go API skeleton: `chi` router, postgres connection, `sqlc`.
- [ ] Auth: signup, login (JWT), OAuth Google/GitHub.
- [ ] Orgs/projects: CRUD, инвайты, RBAC.
- [ ] Presets: CRUD, sync с агентом.
- [ ] Telemetry intake.
- [ ] Update channel: `GET /agent/latest`.
- [ ] License‑check endpoint (заготовка).
- [ ] Stripe‑интеграция (фоном, заглушка эндпоинтов).
- [ ] Web UI: страницы login/signup, settings, dashboard, project
      switcher, presets editor.
- [ ] **DoD**: пользователь регистрируется, создаёт проект, сохраняет
      пресет в облаке, агент пуллит этот пресет.

### Фаза 7. TLS / cloud‑mode для локального агента (1 нед)

- [ ] DNS `local.gdd.app` → 127.0.0.1.
- [ ] Wildcard cert `*.local.gdd.app` от Let's Encrypt, выпуск через
      cloud backend.
- [ ] Агент при первом запуске: pull cert, hot‑reload каждые N часов.
- [ ] Frontend cloud‑mode: connect to `wss://local.gdd.app:9700`.
- [ ] **DoD**: `https://gdd.app` корректно общается с локальным
      агентом по WSS.

### Фаза 8. Дистрибуция и подпись (1–1.5 нед)

- [ ] AOT‑релизы через `cargo` matrix (Win/Mac/Linux × x64/arm64).
- [ ] Signing: Authenticode (Win, EV‑cert), Apple notarization.
- [ ] Auto‑update через `self_update` crate.
- [ ] Tray icon на всех ОС.
- [ ] Установщики: MSI (Win), DMG/PKG (Mac), DEB/RPM/AppImage (Linux).
- [ ] **DoD**: пользователь скачивает установщик с gdd.app, ставит,
      агент работает, обновление приходит автоматически.

### Фаза 9+. После MVP

- Snapshot/restore профилей (Docker volumes / native zip).
- Запись/повтор сценариев → экспорт в Playwright.
- Видео сессии и отчёты в облаке.
- Визуальная регрессия.
- CI mode (headless‑headed CDP, JUnit/Allure отчёты, GitHub Action).
- Multi‑engine matrix: Firefox / WebKit Docker образы.
- WebRTC скринкаст (Rust‑агент с `webrtc.rs`) вместо JPEG.
- Live‑шаринг сессии команде через cloud relay.

---

## 7. Зафиксированные архитектурные решения

| Решение | Выбор | Почему |
|---|---|---|
| Frontend | React 18 + TS + Vite + Tailwind + Zustand | Стандарт, найм, скорость |
| Cloud backend | Go 1.22 + chi + Postgres + sqlc + Redis | Простой ops, быстрый старт |
| Local agent | Rust 1.78+ + tokio + axum + bollard + chromiumoxide | Минимальный бинарь, true cross‑compile, нет GC, max performance |
| Browser backends | `ChromiumProcessEngine` (CLI) + `DockerEngine` — оба одновременно | Покрывает обе аудитории через один код |
| Не используем Playwright/Node | Прямой CDP через `chromiumoxide` | Минус 200 МБ зависимостей, контроль |
| Транспорт UI↔Agent | JSON‑RPC over WebSocket | Симметрия с MCP |
| Транспорт UI↔Cloud | REST + WS | Стандарт |
| Скринкаст v1 | CDP `Page.startScreencast` JPEG | Нулевые зависимости |
| Скринкаст v2 (после MVP) | WebRTC через `webrtc.rs` | Лучше FPS, дешевле CPU при 16+ ячейках |
| TLS для local | `local.gdd.app` → 127.0.0.1 + wildcard cert | Паттерн Plex / Tailscale, нет mixed content |
| Локальный mode без облака | Агент сам отдаёт React build из `rust-embed` | MVP без cloud зависимости |
| C# код GDD | Только референс, не реюз | Перенос языка, не миграция |
| Подпись бинарей | Authenticode + Apple notarytool | Обязательно для распространения |

---

## 8. Открытые вопросы

1. **Доставка wildcard cert** для `*.local.gdd.app`. Варианты:
   (a) встраивать приватный ключ в подписанный бинарь — security
   tradeoff (любой может извлечь), (b) пуллить per‑install от cloud
   backend под аутентификацией, (c) каждый юзер выпускает свой через
   ACME с DNS‑01 (требует контроля DNS — нет). Скорее (b).
2. **Self‑hosted режим** (без cloud): должен ли агент работать полностью
   автономно, без `api.gdd.app`? Скорее да — local‑mode по умолчанию,
   cloud‑mode по логину.
3. **Лицензирование**: open core / freemium / closed source SaaS?
   Влияет на доступность кода агента.
4. **CDP клиент**: `chromiumoxide` (high‑level, удобно) vs raw
   `tokio-tungstenite` (полный контроль). Скорее `chromiumoxide` для
   скорости разработки, fallback на raw для специфичных сценариев.
5. **Telegram Desktop emulation**: TMA в реальном Telegram Desktop
   работает на Qt WebEngine. Нужно ли воспроизводить его user‑agent +
   environment? Если да — `TelegramDesktopProfile` в DeviceEmulation.
6. **Snapshot формат профилей**: tar.gz volume (Docker) vs zip папки
   (CLI). Хотим ли единый портабельный формат? Желательно.
7. **MCP‑proxy**: оставлять `mcp-proxy.ps1` или переписать на
   `gdd-mcp-bridge` (отдельный маленький Rust‑бинарь для stdio↔HTTP).
   Скорее переписать, для кросс‑платформы.
8. **Скейл агента**: один агент на машину, или несколько (для разных
   проектов/окружений)? Один + workspaces — проще.
9. **Auth между React и локальным агентом**: agent при старте
   генерирует token, кладёт в OS‑specific secure storage, React
   читает через web‑интеграцию (cloud отдаёт токен) или через локальный
   `GET /pair` с одноразовым кодом. Нужно продумать UX без серверной
   зависимости.

---

## 9. Что должен сделать assistant в новом чате

1. Прочитать этот документ полностью.
2. Прочитать `src/BrowserXn/` для понимания референсного кода (особенно
   `Mcp/`, `Services/`, `Abstractions/IBrowserEngine.cs`).
3. Подтвердить план или задать уточняющие вопросы по §8.
4. Начать с **Фазы 0** — структура репо и заглушки.
5. Дальше идти параллельно: фаза 1 (Rust agent) + фаза 2 (Web UI) +
   фаза 3 (Docker) + фаза 6 (Go backend) — у разных компонентов
   независимые critical path'ы. Если разработчик один, последовательность:
   0 → 1 → 2 → 4 → 3 → 5 → 6 → 7 → 8.
6. Работать в ветке `claude/review-project-docs-eXqi1` или ответвлять
   фичевые ветки от неё. Часто и атомарно коммитить.
7. Не трогать `src/BrowserXn/` — это легаси‑референс, ломать его не
   нужно. Можно читать.

### 9.1. Команды для быстрой ориентации

```bash
# Структура существующего GDD (референс для портирования)
ls -la src/BrowserXn/

# Что уже абстрагировано в C# — образец для Rust trait
cat src/BrowserXn/Abstractions/IBrowserEngine.cs

# MCP сервер и тулы — образец для портирования
ls src/BrowserXn/Mcp/Tools/
cat src/BrowserXn/Mcp/McpServer.cs

# Бизнес‑логика, которую переносим
ls src/BrowserXn/Services/
cat src/BrowserXn/Services/QuickAuthService.cs
cat src/BrowserXn/Services/TelegramInitDataService.cs

# Конфиг
cat src/BrowserXn/appsettings.json
```

---

## 10. Глоссарий

- **Player** — одна изолированная браузерная сессия, одна ячейка
  видеостены.
- **Engine** / **Backend** — реализация `BrowserEngine` trait. Есть
  `ChromiumProcessEngine` (CLI) и `DockerEngine`.
- **Agent** — Rust CLI‑процесс на машине пользователя. Слушает
  `localhost:9700`.
- **Cloud** / **API** — Go‑сервис на `api.gdd.app`. Аккаунты,
  проекты, пресеты, история, лицензии.
- **CDP** — Chrome DevTools Protocol. JSON‑RPC поверх WebSocket.
- **MCP** — Model Context Protocol. Управление GDD из LLM‑агентов.
- **TMA** — Telegram Mini App. Один из ключевых юзкейсов GDD.
- **Screencast** — поток JPEG/H264 кадров со страницы. CDP
  `Page.startScreencast`.
- **Local mode** — агент работает автономно, React загружается с
  `localhost:9700`, без cloud.
- **Cloud mode** — React грузится с `gdd.app`, общается с агентом по
  `wss://local.gdd.app:9700`, синхронизация через `api.gdd.app`.
- **CLI backend** = `ChromiumProcessEngine` (системный браузер).
- **Docker backend** = `DockerEngine` (контейнер per player).
