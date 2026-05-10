# Changelog

All notable changes to GDD are documented here.

## [1.2.0] - 2026-05-09

### Added

- **`gdd_reload`** — reload current page with optional hard reload (bypass cache, like Ctrl+Shift+R)
- **`gdd_back`** — navigate back in browser history
- **`gdd_forward`** — navigate forward in browser history
- **`gdd_hover`** — hover over element (triggers mouseover/mouseenter, for tooltips/dropdowns)
- **`gdd_select`** — select option from `<select>` dropdown by value or visible text
- **`gdd_dialog`** — handle JS alert/confirm/prompt dialogs (accept/dismiss, enter text)
- **`gdd_storage`** — read/write/clear localStorage/sessionStorage (actions: get, set, remove, clear, keys)
- **`gdd_cookies`** — read or clear browser cookies (actions: get, clear)
- **Error beacon** — every MCP tool response now appends console error warnings for all players that have errors (format: `⚠ Player 2: 3 console errors. Use gdd_get_console(player_id) to inspect.`)
- **`gdd_add_players` device parameter** — create players with a specific device preset immediately (`gdd_add_players(3, device="iPad Air")`) instead of creating with default then switching
- **`gdd_set_viewport`** — set arbitrary viewport dimensions (width, height, scale, mobile flag, user agent)
- **`gdd_set_language`** — set browser language/locale, changes `navigator.language`, `navigator.languages`, and `Accept-Language` header
- **`gdd_set_device` expanded** — now exposes all 22 device presets (was 5 in enum)
- **Console & network diagnostics on ViewModel** — ConsoleErrorCount, NetworkErrorCount, LastError fields on BrowserCellViewModel
- **Stdout port output** — GDD prints `GDD MCP server listening on http://localhost:{port}` on startup
- **MCP endpoint in status bar** — moved from toolbar to bottom-left for cleaner UI

### Changed

- Default `FrontendUrl` changed from `http://localhost:5173` to `about:blank` (faster startup, no dependency on dev server)
- Main window opens at 30% of screen size (was maximized)
- Overlay window title and size update on device change
- `gdd_set_device` now calls `NavigateAsync` after applying device metrics (page reloads with correct viewport)
- WebView2ControlAdapter uses Dispatcher marshaling for all CDP calls

## [1.1.0] - 2026-05-09

### Added

- **Cross-platform headless mode** — GDD.Headless runs on Windows, Linux, and macOS
- **GDD.Core shared library** — platform-independent core with all MCP tools, services, and abstractions
- **PlaywrightEngine** — IBrowserEngine implementation via Microsoft Playwright (Chromium)
- **HeadlessPlayerManager** — manages browser instances without GUI
- **PlaywrightSetup** — automatic Chromium browser installation on first run
- **mcp-proxy.sh** — Bash auto-launch proxy for Linux/macOS
- **mcp-proxy.ps1** — PowerShell auto-launch proxy for headless Windows
- **`gdd_get_manual`** MCP tool — returns the full manual for M2M self-learning
- **CI/CD pipeline** — GitHub Actions builds for 5 targets: Windows GUI, headless win-x64, linux-x64, osx-arm64, osx-x64
- **Smoke tests in CI** — each headless build verifies tools via HTTP
- **Auto-release** — `v*` tags trigger GitHub Releases with tar.gz archives

### Changed

- Extracted all services, MCP tools, and models from BrowserXn into GDD.Core
- IBrowserEngine interface: `nint` → `object?` for cross-platform InitializeAsync
- Added ICdpEventSubscription abstraction replacing WebView2-specific CDP event receivers
- Added IMainThreadDispatcher abstraction replacing WPF Dispatcher
- Added IPlayerManager/IPlayerContext abstractions replacing direct ViewModel access
- MCP tools now work through abstractions — identical behavior on WebView2 and Playwright
- Headless default FrontendUrl changed to `about:blank` (no dev server dependency)
- Serilog config: added `"Using"` section for single-file publish compatibility

### Fixed

- Serilog crash on single-file publish (missing assembly resolution)
- HeadlessPlayerContext URL and title not updating after navigation
- `gdd_tap` failing on Playwright due to JSON.stringify double-escaping

## [1.0.0] - 2026-05-09

### Added

- **25 MCP tools** for browser automation via Claude Code
  - Player management: add, remove, list browser instances
  - Navigation: navigate, wait for selectors
  - Interaction: tap, swipe, scroll, type
  - Reading: read element text, read all matching, screenshot
  - Emulation: device (22 presets), viewport, geolocation, network, language
  - State & diagnostics: browser state, console, network requests, notifications, performance metrics
  - Auth: quick auto-registration with token injection
  - Execution: arbitrary JavaScript execution
- **MCP server** with Streamable HTTP and SSE transports
- **Stdio proxy** (`mcp-proxy-auto.ps1`) with auto-launch — GDD starts automatically when Claude Code connects
- **Video wall** UI with DWM thumbnail rendering
- **Device presets**: 11 phones, 6 tablets, 5 desktops (iPhone SE through 16 Pro Max, Pixel 9, Galaxy S24, iPads, etc.)
- **Quick presets**: 3 Phones, All Phones, Responsive, Cross-Platform, All Screens
- **Location presets**: Moscow, Saint Petersburg, New York, London, Tokyo
- **Network presets**: Online, 4G, Fast 3G, Slow 3G, Offline
- **Telegram testing mode** with WebApp data injection
- **Dark theme** with DWM title bar color matching
- **Help window** with built-in manual viewer
- **WebView2 Runtime check** at startup with guided installation
- **Single-file publish** — self-contained EXE (~70 MB), no .NET required
- **Per-player isolated profiles** in `%LOCALAPPDATA%\GDD\Profiles\`
