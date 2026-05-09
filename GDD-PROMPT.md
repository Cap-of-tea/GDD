# GDD (Giggly-Dazzling-Duckling) — Multi-Browser Testing MCP Server

## What is GDD

GDD is a cross-platform application that manages multiple isolated Chromium browser instances ("players") and exposes 26 MCP tools for browser automation, device/network/location emulation, and diagnostics. It listens on `http://localhost:9700/mcp`.

Two modes: **Windows GUI** (WebView2 with visual preview) and **Headless** (Playwright, runs on Windows/Linux/macOS). Both provide identical MCP tools.

You have access to GDD tools via the `gdd` MCP server. Use them to test web applications across different devices, networks, geolocations, and languages — all from a single machine.

## MCP Configuration

The `.mcp.json` in this project connects you to GDD. GDD auto-launches when you first call any tool. If tools return connection errors, wait 5-6 seconds and retry — GDD is starting up.

## Available Tools (26)

### Player Management
- `gdd_add_players(count, device?)` — Create N browser windows with optional device preset. Returns player IDs (e.g. [1, 2, 3]). Always start here.
- `gdd_remove_player(player_id)` — Close and remove a browser window.
- `gdd_list_windows()` — List all active players with their state (URL, device, auth).

### Navigation
- `gdd_navigate(player_id, url)` — Navigate a player to a URL.
- `gdd_wait(player_id, selector, timeout?)` — Wait for a CSS selector to appear (default 5000ms).

### Interaction
- `gdd_tap(player_id, selector?, x?, y?)` — Tap an element by CSS selector or coordinates.
- `gdd_swipe(player_id, direction, distance?)` — Swipe gesture (up/down/left/right, default 300px).
- `gdd_scroll(player_id, selector?, direction?, amount?)` — Scroll into view or by direction.
- `gdd_type(player_id, selector, text, clear?)` — Type text into an input field.

### Reading Content
- `gdd_read(player_id, selector)` — Read text content of one element.
- `gdd_read_all(player_id, selector)` — Read text of all matching elements (JSON array).
- `gdd_screenshot(player_id)` — Take a screenshot (returns base64 PNG image).
- `gdd_execute_js(player_id, script)` — Execute JavaScript and return the result.

### Device Emulation
- `gdd_set_device(player_id, preset)` — Set device preset. Available presets:
  - **Phones**: iPhone SE, iPhone 14, iPhone 15 Pro, iPhone 15 Pro Max, iPhone 16 Pro, iPhone 16 Pro Max, Pixel 9, Pixel 9 Pro, Galaxy S24, Galaxy S24 Ultra, OnePlus 12
  - **Tablets**: iPad Mini, iPad Air, iPad Pro 11", iPad Pro 13", Galaxy Tab S9, Pixel Tablet
  - **Desktop**: Laptop HD, Laptop HiDPI, Desktop 1080p, Desktop 1440p, Desktop 4K
- `gdd_set_viewport(player_id, width, height, device_scale_factor?, mobile?, user_agent?)` — Set arbitrary viewport size.

### Environment Emulation
- `gdd_set_location(player_id, preset, latitude?, longitude?, timezone?, locale?)` — Set geolocation. Presets: Moscow, Saint Petersburg, New York, London, Tokyo, custom.
- `gdd_set_network(player_id, preset)` — Set network conditions: Online, 4G, Fast 3G, Slow 3G, Offline.
- `gdd_set_language(player_id, locale)` — Set browser language (e.g. "ru", "en-US", "ja-JP"). Changes navigator.language and Accept-Language header.

### Diagnostics
- `gdd_get_state(player_id)` — Full state: URL, title, device, auth, errors, language.
- `gdd_get_console(player_id, level?, last?)` — Console output (log/warn/error) and uncaught exceptions.
- `gdd_get_network(player_id, failed_only?, resource_type?, last?)` — Network requests with status, timing, errors.
- `gdd_get_performance(player_id)` — Performance metrics: JS heap, DOM nodes, task duration.
- `gdd_get_notifications(player_id?)` — Push notifications received by players.
- `gdd_clear_logs(player_id, target?)` — Clear console/network/all logs.

### Authentication
- `gdd_quick_auth(player_id)` — Auto-register and login with generated credentials. Pass player_id=0 for all players.

### Help
- `gdd_get_manual()` — Returns the full GDD manual for self-learning.

## Workflow Patterns

### Basic testing
```
1. gdd_add_players(3)              → [1, 2, 3]
2. Wait 3-5 seconds for browser init
3. gdd_navigate(1, "https://myapp.com")
4. gdd_screenshot(1)               → see the page
5. gdd_read(1, "h1")              → read heading text
```

### Cross-device testing
```
1. gdd_add_players(1, device="iPhone 15 Pro")  → [1]
2. gdd_add_players(1, device="iPad Air")        → [2]
3. gdd_add_players(1, device="Desktop 1080p")   → [3]
4. Navigate all to the same URL, take screenshots, compare
```

### Debugging
```
1. gdd_navigate(1, "https://myapp.com")
2. gdd_get_console(1, level="error")     → JS errors
3. gdd_get_network(1, failed_only=true)  → failed requests
4. gdd_get_performance(1)                → memory & DOM metrics
```

### Multi-language testing
```
1. gdd_add_players(3)
2. gdd_set_language(1, "en-US")
3. gdd_set_language(2, "ru")
4. gdd_set_language(3, "ja-JP")
5. Navigate all to the same URL, verify localization
```

## Important Notes

- After `gdd_add_players`, wait 3-5 seconds before navigating — the browser needs time to initialize.
- `player_id` starts from 1 and increments. Use `gdd_list_windows()` to see current IDs.
- Screenshots capture the browser viewport content, not the application window frame.
- Console and network logs are captured automatically once a player is created. Use `gdd_get_console` and `gdd_get_network` to read them at any time.
- All emulation (device, location, network, language) persists until changed or the player is removed.
