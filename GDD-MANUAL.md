# GDD Manual

## 1. What is GDD

GDD (Giggly-Dazzling-Duckling) — кроссплатформенный инструмент для мультибраузерного тестирования. Управляет N изолированными Chromium-инстансами и выставляет 34 MCP-инструмента для Claude Code.

Три режима: **Windows GUI** (WPF + WebView2, с визуальным превью), **Headless** (Playwright, работает на Windows/Linux/macOS) и **Headed** (`--headed` — видимые окна Chromium на любой платформе). Все режимы предоставляют идентичный набор MCP-инструментов.

Claude видит и управляет браузерами как человек: открывает страницы, тапает кнопки, читает текст, делает скриншоты, эмулирует устройства/сети/геолокации, мониторит консоль и сетевые запросы.

---

## 2. Setup & Launch

### Prerequisites

- Claude Code (VS Code extension или CLI)

**Windows GUI:**

- Windows 10/11
- WebView2 Runtime (обычно предустановлен с Edge, проверяется при запуске)

**Headless (Windows/Linux/macOS):**

- Chromium устанавливается автоматически при первом запуске через Playwright

### Запуск

**Windows GUI:**

```powershell
# Скачать из Releases или собрать из исходников:
.\GDD.exe
```

**Headless (любая платформа):**

```bash
./GDD.Headless
# MCP server starts on http://localhost:9700/mcp
```

**Headed (видимые окна Chromium):**

```bash
./GDD.Headless --headed
```

GDD запускает MCP HTTP сервер на порту 9700 (auto-fallback 9701..9709 если занят).

### Настройка Claude Code

`.mcp.json` в корне проекта:

**Windows (PowerShell proxy с автозапуском):**

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

**Linux / macOS (Bash proxy с автозапуском):**

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

**Linux / macOS (Headed — видимые окна Chromium):**

```json
{
  "mcpServers": {
    "gdd": {
      "command": "bash",
      "args": ["/path/to/Scripts/mcp-proxy.sh", "--headed"]
    }
  }
}
```

Прокси-скрипты автоматически запускают GDD если он не работает, и пробрасывают JSON-RPC из stdin Claude Code в HTTP endpoint `http://localhost:9700/mcp`.

### Проверка подключения

Откройте **новый чат** в Claude Code (или Reload Window). MCP клиент читает `.mcp.json` только при старте сессии.

34 tools должны появиться с `mcp__gdd__` префиксом.

### Troubleshooting

| Problem | Solution |
| ------- | -------- |
| Tools not found | GDD must be running **before** starting the Claude Code session |
| Connection refused | Check GDD is on port 9700: `Invoke-WebRequest http://localhost:9700/mcp -Method POST` |
| Port conflict | GDD auto-tries 9700-9709. Update `mcp-proxy.ps1` baseUrl if needed |
| Multiple GDD instances | Close extras: `Get-Process GDD \| Stop-Process` then restart one |

---

## 3. Tool Reference

### 3.1 Player Management

#### `gdd_add_players(count, device?)`

Create N browser windows. **Always start here.**

| Param | Type | Required | Description |
| -------- | ------- | -------- | ------------------------------------------ |
| `count` | integer | yes | Number of players (1-64) |
| `device` | string | no | Device preset name (default: iPhone 15 Pro) |

Returns: `"Created 3 players with iPad Air: [1, 2, 3]"`

The `device` parameter lets you create players with a specific device profile immediately — no need to call `gdd_set_device` separately. See Device Emulation for available presets.

After creation, wait ~5 seconds before navigating — the browser needs init time.

#### `gdd_remove_player(player_id)`

Close one browser window.

#### `gdd_list_windows()`

Returns JSON array of all active players:

```json
[
  { "id": 1, "name": "Player 1", "url": "about:blank", "status": "Ready", "overlay_open": false }
]
```

---

### 3.2 Navigation

#### `gdd_navigate(player_id, url)`

Navigate to URL. Includes 500ms settle delay. Always follow with `gdd_wait` before interacting.

#### `gdd_wait(player_id, selector, timeout?)`

Wait for CSS selector to appear. Polls every 200ms.

| Param | Type | Default |
| ----- | ---- | ------- |
| `selector` | string | — |
| `timeout` | integer (ms) | 5000 |

Returns: `"Found '.btn-submit' after 800ms"` or `"Timeout: '.btn-submit' not found after 5000ms"`

#### `gdd_reload(player_id, hard?)`

Reload the current page.

| Param | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| `hard` | boolean | false | Hard reload (bypass cache, like Ctrl+Shift+R) |

#### `gdd_back(player_id)`

Navigate back in browser history.

#### `gdd_forward(player_id)`

Navigate forward in browser history.

---

### 3.3 Interaction

#### `gdd_tap(player_id, selector?, x?, y?)`

Tap element by CSS selector (preferred) or coordinates. Uses touch events via CDP.

Either `selector` OR `x`+`y` required. Selector resolves to element center via `getBoundingClientRect()`.

#### `gdd_swipe(player_id, direction, distance?)`

Swipe gesture. 10-step animation over 160ms.

| Param | Values | Default |
| ----- | ------ | ------- |
| `direction` | "up", "down", "left", "right" | — |
| `distance` | pixels | 300 |

#### `gdd_scroll(player_id, selector?, direction?, amount?)`

Two modes:

- **Selector mode:** `scrollIntoView({ behavior: 'smooth' })` — scroll until element is visible
- **Direction mode:** `window.scrollBy()` — scroll up/down by N pixels

#### `gdd_type(player_id, selector, text, clear?)`

Type text into input/textarea.

| Param | Type | Default |
| ----- | ---- | ------- |
| `clear` | boolean | true |

Uses native value setter + dispatches `input` and `change` events. Set `clear=false` to append.

#### `gdd_hover(player_id, selector)`

Hover over an element (triggers `mouseover`/`mouseenter` events). Useful for tooltips, dropdown menus, and hover states.

#### `gdd_select(player_id, selector, value?, text?)`

Select an option from a `<select>` dropdown.

| Param | Type | Description |
| ----- | ---- | ----------- |
| `selector` | string | CSS selector of the `<select>` element |
| `value` | string | Select by option `value` attribute |
| `text` | string | Select by visible text |

Provide either `value` or `text` (not both).

#### `gdd_dialog(player_id, accept?, text?)`

Handle JavaScript alert/confirm/prompt dialogs.

| Param | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| `accept` | boolean | true | Accept (OK) or dismiss (Cancel) the dialog |
| `text` | string | — | Text to enter in prompt dialogs |

---

### 3.4 Reading Content

#### `gdd_read(player_id, selector)`

Returns `textContent` of first matching element.

#### `gdd_read_all(player_id, selector)`

Returns JSON array of `textContent` from all matching elements.

#### `gdd_screenshot(player_id)`

Captures viewport as JPEG at CSS pixel resolution (1x scale, quality 80 by default). Coordinates in the image match CSS pixels directly — use them for `gdd_tap(x, y)`. Optional `quality` parameter (1-100).

#### `gdd_execute_js(player_id, script)`

Execute JavaScript, return result.

**Caveats:**

- Returns raw result (string, number, or JSON for objects)
- `null` results return the string `"null"`
- Multi-line scripts must be wrapped in IIFE: `(function(){ ... })()`
- Async/await **does not work** — promises don't resolve in this context
- **Don't use for API calls** (`fetch`, `XMLHttpRequest`) — use for reading state only

---

### 3.5 Device Emulation

#### `gdd_set_device(player_id, preset)`

Apply device profile. Sets viewport size, DPI, User-Agent, touch emulation.

**22 presets:**

| Category | Presets |
| -------- | ------- |
| Phones | iPhone SE, iPhone 14, iPhone 15 Pro, iPhone 15 Pro Max, iPhone 16 Pro, iPhone 16 Pro Max, Pixel 9, Pixel 9 Pro, Galaxy S24, Galaxy S24 Ultra, OnePlus 12 |
| Tablets | iPad Mini, iPad Air, iPad Pro 11", iPad Pro 13", Galaxy Tab S9, Pixel Tablet |
| Desktop | Laptop HD, Laptop HiDPI, Desktop 1080p, Desktop 1440p, Desktop 4K |

Case-insensitive matching.

#### `gdd_set_viewport(player_id, width, height, device_scale_factor?, mobile?, user_agent?)`

Set arbitrary viewport. Auto-generates User-Agent if omitted (mobile Safari or desktop Chrome).

---

### 3.6 Environment Emulation

#### `gdd_set_location(player_id, preset, ...)`

Set geolocation + timezone + locale.

| Preset | City | Timezone | Locale |
| ------ | ---- | -------- | ------ |
| Moscow | 55.75, 37.61 | Europe/Moscow | ru-RU |
| Saint Petersburg | 59.93, 30.31 | Europe/Moscow | ru-RU |
| New York | 40.71, -74.00 | America/New_York | en-US |
| London | 51.50, -0.12 | Europe/London | en-GB |
| Tokyo | 35.68, 139.69 | Asia/Tokyo | ja-JP |
| custom | lat, lon, tz, locale — all manual | | |

#### `gdd_set_network(player_id, preset)`

Network condition emulation via CDP.

| Preset | Latency | Down | Up |
| ------ | ------- | ---- | -- |
| Online | 0ms | unlimited | unlimited |
| 4G | 20ms | 4 Mbps | 3 Mbps |
| Fast 3G | 563ms | 1.6 Mbps | 768 Kbps |
| Slow 3G | 2000ms | 500 Kbps | 500 Kbps |
| Offline | — | 0 | 0 |

#### `gdd_set_language(player_id, locale)`

Set browser language. Changes `navigator.language`, `Accept-Language` header, and locale override.

Examples: `"ru"`, `"en-US"`, `"ja-JP"`, `"de-DE"`

---

### 3.7 Authentication

#### `gdd_quick_auth(player_id)`

Auto-register/login against the backend.

- `player_id=0` — authenticate all players
- Generates credentials: `player{id}@gdd.test` / `GDD-Player{id}!`
- Injects tokens into `localStorage["noise-auth"]`
- Navigates to frontend URL
- Batches 8 players at a time with 500ms delay

---

### 3.8 Diagnostics

#### `gdd_get_state(player_id)`

Full player state as JSON:

```json
{
  "player_id": 1,
  "url": "https://app.example.com/dashboard",
  "device": { "name": "iPhone 15 Pro", "width": 393, "height": 852, "scale": 3, "mobile": true },
  "console_error_count": 2,
  "network_error_count": 0,
  "last_error": "TypeError: Cannot read properties of undefined",
  "language": "en-US"
}
```

#### `gdd_get_console(player_id, level?, last?)`

Console output. Levels: `log`, `warn`, `error`, `info`, `debug`. Circular buffer of 500 entries per player.

```json
[
  { "level": "error", "message": "Uncaught TypeError: ...", "source": "app.js", "line": 42, "is_exception": true, "timestamp": "2026-05-08T12:00:00Z" }
]
```

#### `gdd_get_network(player_id, failed_only?, resource_type?, last?)`

Network requests. Resource types: `Document`, `Script`, `Stylesheet`, `Image`, `XHR`, `Fetch`, `Font`, `Media`, `Other`.

```json
[
  { "method": "GET", "url": "https://api.example.com/users", "status": 500, "duration_ms": 234.5, "failed": true, "error": "net::ERR_FAILED" }
]
```

#### `gdd_get_performance(player_id)`

CDP `Performance.getMetrics`: JS heap size, DOM nodes, layout count, task duration, etc.

#### `gdd_get_notifications(player_id?)`

Push notifications received by players. `player_id=0` for all.

#### `gdd_clear_logs(player_id, target?)`

Clear logs. Target: `"console"`, `"network"`, or `"all"` (default). Resets error counters.

---

### 3.9 Browser Storage

#### `gdd_storage(player_id, action, storage?, key?, value?)`

Read, write, or clear localStorage/sessionStorage.

| Param | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| `action` | string | — | `get`, `set`, `remove`, `clear`, `keys` |
| `storage` | string | `"local"` | `"local"` (localStorage) or `"session"` (sessionStorage) |
| `key` | string | — | Key name (required for `get`, `set`, `remove`) |
| `value` | string | — | Value to store (required for `set`) |

#### `gdd_cookies(player_id, action, name?)`

Read or clear browser cookies.

| Param | Type | Description |
| ----- | ---- | ----------- |
| `action` | string | `get` (read cookies) or `clear` (delete cookies) |
| `name` | string | Cookie name filter (optional, for `get`/`clear` a specific cookie) |

---

## 4. Claude Agent Rules

### Core Principle

GDD is a **UI testing tool**. All actions go through the interface the user sees. Never bypass UI with direct API calls via `gdd_execute_js`.

### Work Cycle

```text
gdd_navigate → gdd_wait(selector) → gdd_screenshot → gdd_tap/gdd_type → gdd_wait → gdd_screenshot → repeat
```

**ALWAYS `gdd_wait` after `gdd_navigate`** — the page needs time to load. Never screenshot or interact immediately after navigation.

Always screenshot **before and after** every significant action. Never guess UI state.

### Rules

| Rule | Why |
| ---- | --- |
| **Wait after navigation** | **ALWAYS** `gdd_wait(selector)` after `gdd_navigate` — page is NOT loaded when navigate returns. Screenshot without wait = incomplete/blank page |
| Tap by selectors, not coordinates | Selectors are reliable across all devices. Screenshot coordinates are CSS pixels and can be used as fallback |
| `gdd_execute_js` is a last resort | Only for reading state (localStorage, JS variables). Never for `fetch`/`XHR`/API calls |
| Scroll before tap | Elements below the fold need `gdd_scroll` before interaction |
| Don't diagnose bugs from screenshots | Verify data format and logic first, then claim a bug |
| Multi-window awareness | `gdd_list_windows` to see all instances, then use correct `player_id` |
| Watch for error beacons | Every tool response appends console error warnings for ALL players. Act on them immediately |

### Error Beacon

Every MCP tool response automatically appends console error warnings for all players that have errors. Format:

```text
⚠ Player 2: 3 console errors. Use gdd_get_console(player_id) to inspect.
```

When you see an error beacon, call `gdd_get_console` to inspect the errors before continuing. Don't ignore beacons — they indicate real problems that may affect your testing.

### Anti-patterns (forbidden)

| Anti-pattern | Problem |
| ----------- | ------- |
| `gdd_execute_js` with `fetch()` or `XMLHttpRequest` | Bypasses UI, invisible to user, breaks test validity |
| Multi-line JS without IIFE wrapper | Returns `null` — wrap in `(function(){ ... })()` |
| Async JS expecting a return value | Promises don't resolve in `execute_js` context |
| Screenshot then immediate action without wait | After `gdd_navigate`, always `gdd_wait` first — otherwise you interact with a blank/loading page |
| Assumptions about data format without verification | Always `gdd_read` or `gdd_screenshot` to confirm actual state |

---

## 5. Real-World Workflow Examples

### 5.1 Basic: Open Page, Verify Content

```text
User: "Check that the login page loads correctly"

Agent:
1. gdd_add_players(1)                        → [1]
2. [wait 5 seconds]
3. gdd_set_device(1, "iPhone 15 Pro")
4. gdd_navigate(1, "https://app.example.com/login")
5. gdd_wait(1, "form.login-form")            → "Found 'form.login-form' after 320ms"
6. gdd_screenshot(1)                         → [sees login form with email/password fields]
7. gdd_read(1, "h1")                         → "Sign In"
8. gdd_read_all(1, "input[type]")            → ["", ""]  (empty email + password fields)

Report: "Login page loads correctly on iPhone 15 Pro. Form renders with email
and password fields, 'Sign In' heading visible."
```

### 5.2 Form Interaction: Login Flow

```text
User: "Log in with test@example.com / password123 and verify dashboard loads"

Agent:
1. gdd_navigate(1, "https://app.example.com/login")
2. gdd_wait(1, "input[name='email']")
3. gdd_screenshot(1)                         → [login form visible]
4. gdd_type(1, "input[name='email']", "test@example.com")
5. gdd_type(1, "input[name='password']", "password123")
6. gdd_screenshot(1)                         → [fields filled]
7. gdd_tap(1, "button[type='submit']")
8. gdd_wait(1, ".dashboard", timeout=10000)
9. gdd_screenshot(1)                         → [dashboard loaded]
10. gdd_read(1, ".user-name")                → "Test User"

Report: "Successfully logged in. Dashboard loaded with user 'Test User'."
```

### 5.3 Cross-Device Comparison

```text
User: "Compare the navbar on mobile vs tablet vs desktop"

Agent:
1. gdd_add_players(1, device="iPhone 15 Pro")   → [1]
2. gdd_add_players(1, device="iPad Air")         → [2]
3. gdd_add_players(1, device="Desktop 1080p")    → [3]
4. [wait 5 seconds]
5. gdd_navigate(1, "https://app.example.com")
6. gdd_navigate(2, "https://app.example.com")
7. gdd_navigate(3, "https://app.example.com")
8. gdd_wait(1, "nav")
9. gdd_wait(2, "nav")
10. gdd_wait(3, "nav")
11. gdd_screenshot(1)                        → [hamburger menu on mobile]
12. gdd_screenshot(2)                        → [compact nav on tablet]
13. gdd_screenshot(3)                        → [full nav on desktop]

Report: "Mobile (393px): hamburger menu, nav items hidden.
Tablet (820px): condensed nav, icons without labels.
Desktop (1920px): full navigation bar with text labels."
```

### 5.4 Debugging: Find Why a Button Doesn't Work

```text
User: "The 'Save' button on the settings page doesn't do anything"

Agent:
1. gdd_navigate(1, "https://app.example.com/settings")
2. gdd_wait(1, ".settings-form")
3. gdd_screenshot(1)                         → [settings page visible]
4. gdd_clear_logs(1)                         → clean slate
5. gdd_tap(1, "button.save-btn")
6. gdd_screenshot(1)                         → [no visible change]
7. gdd_get_console(1, level="error")         → [
     { "message": "TypeError: Cannot read properties of null (reading 'id')",
       "source": "settings.js", "line": 87 }
   ]
8. gdd_get_network(1, failed_only=true)      → []  (no failed requests)

Report: "JS error on save click: TypeError at settings.js:87 —
'Cannot read properties of null (reading id)'. The form handler
crashes before sending the API request. No network calls made.
Likely a null reference in the settings object."
```

### 5.5 Multi-Language Verification

```text
User: "Check that Russian and Japanese localizations render correctly"

Agent:
1. gdd_add_players(3)                        → [1, 2, 3]
2. [wait 5 seconds]
3. gdd_set_language(1, "en-US")
4. gdd_set_language(2, "ru")
5. gdd_set_language(3, "ja-JP")
6. gdd_set_device(1, "iPhone 15 Pro")
7. gdd_set_device(2, "iPhone 15 Pro")
8. gdd_set_device(3, "iPhone 15 Pro")
9. gdd_navigate(1, "https://app.example.com")
10. gdd_navigate(2, "https://app.example.com")
11. gdd_navigate(3, "https://app.example.com")
12. gdd_wait(1, "h1")
13. gdd_wait(2, "h1")
14. gdd_wait(3, "h1")
15. gdd_read(1, "h1")                        → "Welcome"
16. gdd_read(2, "h1")                        → "Добро пожаловать"
17. gdd_read(3, "h1")                        → "ようこそ"
18. gdd_screenshot(1)                        → [English UI]
19. gdd_screenshot(2)                        → [Russian UI]
20. gdd_screenshot(3)                        → [Japanese UI]

Report: "All 3 localizations render correctly. Headings:
EN 'Welcome', RU 'Добро пожаловать', JA 'ようこそ'.
No layout overflow on any language."
```

### 5.6 Network Performance Under Poor Conditions

```text
User: "Test how the app behaves on Slow 3G"

Agent:
1. gdd_set_network(1, "Slow 3G")
2. gdd_clear_logs(1)
3. gdd_navigate(1, "https://app.example.com")
4. gdd_wait(1, ".app-loaded", timeout=30000) → "Found '.app-loaded' after 12400ms"
5. gdd_screenshot(1)                         → [page loaded but images missing]
6. gdd_get_network(1, failed_only=true)      → [
     { "url": "https://cdn.example.com/hero.jpg", "status": 0, "error": "net::ERR_TIMED_OUT" }
   ]
7. gdd_get_performance(1)                    → { "JSHeapUsedSize": 18432000, "DomContentLoaded": 8200, ... }
8. gdd_set_network(1, "Online")              → restore

Report: "On Slow 3G: initial load 12.4s (vs ~1s on LTE).
Hero image timed out (CDN). JS heap 18MB.
Recommendation: add image lazy loading and reduce critical path."
```

### 5.7 Authenticated Multi-Player Testing

```text
User: "Create 3 users and verify they each see their own profile"

Agent:
1. gdd_add_players(3)                        → [1, 2, 3]
2. [wait 5 seconds]
3. gdd_quick_auth(0)                         → "Player 1: authenticated as gdd_player1
                                                Player 2: authenticated as gdd_player2
                                                Player 3: authenticated as gdd_player3"
4. gdd_navigate(1, "https://app.example.com/profile")
5. gdd_navigate(2, "https://app.example.com/profile")
6. gdd_navigate(3, "https://app.example.com/profile")
7. gdd_wait(1, ".profile-name")
8. gdd_wait(2, ".profile-name")
9. gdd_wait(3, "profile-name")
10. gdd_read(1, ".profile-name")             → "gdd_player1"
11. gdd_read(2, ".profile-name")             → "gdd_player2"
12. gdd_read(3, ".profile-name")             → "gdd_player3"

Report: "All 3 players have isolated sessions. Each sees their own profile name."
```

---

## 6. Architecture Overview

```text
Claude Code ──JSON-RPC──→ mcp-proxy ──HTTP──→ GDD (port 9700)
                                                │
                                      McpToolRegistry (34 tools)
                                                │
                                        IPlayerManager
                                      ┌────┬────┬────┐
                                      │    │    │    │
                                    [P1] [P2] [P3] [P4]  ← IBrowserEngine instances
                                      │    │    │    │    (WebView2 or Playwright)
                                  Chrome DevTools Protocol (CDP)
```

**Transport:** Claude Code spawns `mcp-proxy.ps1` (Windows) or `mcp-proxy.sh` (Linux/macOS) as a stdio subprocess. Скрипт конвертирует stdin JSON-RPC в HTTP POST → `http://localhost:9700/mcp` → HTTP response → stdout JSON-RPC. При необходимости автоматически запускает GDD.

**Threading:** MCP requests arrive on HttpListener threads. `tools/call` dispatches через `IMainThreadDispatcher` — `WpfDispatcher` (GUI, WPF UI thread) или `ConsoleDispatcher` (Headless, прямое выполнение).

**Isolation:** Each player has its own browser profile folder, cookies, localStorage, and session. Players are fully independent.

---

## 7. Configuration

### appsettings.json

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
| --- | ----------- | ------- |
| `FrontendUrl` | Default URL for new browsers & token injection | `about:blank` |
| `BackendUrl` | Backend API for `gdd_quick_auth` | `http://localhost:8080/api/v1` |
| `BotToken` | Telegram Bot Token (for WebApp injection) | empty |
| `McpPort` | MCP server port | 9700 |
| `DataFolderRoot` | Browser profile storage | `%LOCALAPPDATA%\GDD\Profiles` (Win), `~/.local/share/GDD/Profiles` (Linux), `~/Library/Application Support/GDD/Profiles` (macOS) |
| `Headed` | Launch visible browser windows (headless only) | `false` (or use `--headed` CLI flag) |

### Logs

Rolling daily files in `logs/gdd-YYYYMMDD.log` relative to working directory. Level: Information by default.
