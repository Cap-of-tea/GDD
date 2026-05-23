# Changelog

All notable changes to GDD are documented here.

## [1.5.3] - 2026-05-23

### Added

- **Update beacon** — when a new GDD version is available, notifies Claude once per day via tool response. Claude asks the user before updating. Checks GitHub API every 24 hours (previously only at startup)

### Changed

- **Error beacon moved to top** — console error warnings now prepend to tool responses instead of appending, so Claude processes them before the main result
- **Error beacon on errors** — console error warnings now show on error responses too (e.g. element-not-found), providing context that may explain the failure

## [1.5.2] - 2026-05-22

### Added

- **Auto-screenshot on element-not-found** — when any selector-based tool (`gdd_tap`, `gdd_type`, `gdd_hover`, `gdd_select`, `gdd_read`, `gdd_wait`) fails to find an element, the error response now includes a screenshot of the current page state. This lets the LLM see the actual page and self-correct in one retry instead of guessing selectors blindly
- **Page load check before error screenshot** — checks `document.readyState` up to 3 times (1s interval) before capturing; if page is still loading, returns text-only error without screenshot

### Changed

- **gdd_wait description** — updated to mention screenshot on timeout and warn against guessing selectors

## [1.5.1] - 2026-05-22

### Fixed

- **Misleading `session: "shared"` in gdd_list_windows** — renamed to `owner: "gui"` to prevent LLMs from incorrectly assuming shared browser state between players
- **Duplicate "Players: N" counter in GUI** — removed redundant displays from toolbar and status bar right; single counter now shown in status bar left

### Changed

- **gdd_list_windows description** — now explicitly states that each player has an isolated browser context (separate cookies, localStorage, sessions)

## [1.5.0] - 2026-05-20

### Fixed

- **gdd_tap now clicks custom components** — dispatches full mouse event chain (mouseMoved → mousePressed → mouseReleased) after touch events; fixes popover-dropdowns, toggle-chips, and other components that listen for `click`/`pointerdown` instead of touch events

### Added

- **`humanize` parameter** for `gdd_tap` and `gdd_hover` — moves mouse along a cubic Bézier curve with natural easing and micro-jitter before clicking; random duration 0.5–1.5s per movement
- **Docker support** — working Dockerfile with .dockerignore, full Playwright runtime deps, published to GHCR (`ghcr.io/cap-of-tea/gdd`)
- **Official MCP Registry** — GDD listed at registry.modelcontextprotocol.io

### Changed

- Downgraded Serilog.Settings.Configuration 10.0.0 → 8.0.4 for .NET 8 SDK compatibility

## [1.4.7] - 2026-05-18

### Added

- **MCP tool annotations** — all 36 tools now include `readOnlyHint`, `destructiveHint`, `idempotentHint`, and `openWorldHint` per MCP spec, enabling safer tool execution in AI clients
- **Privacy Policy** — README now documents GDD's local-only data handling, no telemetry, and opt-out update checks

## [1.4.6] - 2026-05-17

### Changed

- **TDQS-optimized tool descriptions** — all 36 MCP tool descriptions rewritten for Glama.ai Tool Definition Quality Score: added purpose clarity, usage guidelines, behavioral transparency, return value documentation, and cross-tool references

## [1.4.5] - 2026-05-17

### Changed

- **Documentation fully translated to English** — GDD-MANUAL.md and GDD-ARCHITECTURE.md translated from Russian to English (all sections: setup, architecture, emulation, diagnostics, authentication, UI)
- **VS Code IDE support documented** — README and Manual now include MCP config for VS Code-based IDEs (Windsurf, Antigravity, Copilot) with `.vscode/mcp.json` and `settings.json` formats
- **Permissions section** — README documents `mcp__gdd__*` wildcard for Claude Code `settings.json` to allow all GDD tools without confirmation prompts
- **Expanded config table** — client config table now shows project and global paths for Claude Code, Cursor, and VS Code-based IDEs

## [1.4.4] - 2026-05-16

### Fixed

- **Duplicate processes on update** — apply scripts now kill remaining GDD processes before relaunching (pkill on Unix, Stop-Process on Windows)
- **--help launching full GDD** — `--help`, `-h`, `-v`, `--version` now print usage and exit immediately without starting the MCP server
- **Duplicate instance prevention** — PID file (`.gdd.pid`) prevents running multiple GDD instances; second launch prints error and exits

## [1.4.2] - 2026-05-16

### Added

- **macOS: bundled Node.js fallback** — Chromium auto-install now falls back to `.playwright/node/{platform}/node` + `cli.js` when PowerShell (`pwsh`) is unavailable. Three-tier chain: Playwright.Program.Main → pwsh → bundled node
- **Crash logging** — unhandled exceptions written to `logs/gdd-crash.log` with timestamp, exception details, and OS/version info
- **`--headless` CLI flag** — explicitly disable headed mode (`./GDD.Headless --headless` for CI/CD)
- **macOS launchd autostart** — README documents `launchd` plist for persistent GDD on macOS
- **Linux systemd autostart** — README documents `systemd --user` service for persistent GDD on Linux
- **Direct URL connection** — documented `"url": "http://localhost:9700/mcp"` as recommended MCP connection method (no proxy needed)

### Changed

- **Headed mode by default** — `GDD.Headless` now launches with visible Chromium windows. Use `--headless` for CI/CD. `appsettings.json` default: `"Headed": true`
- **macOS xattr handling** — targeted quarantine removal on `.browsers`, `.playwright`, and executables instead of recursive on entire directory. Suppresses permission errors (`2>/dev/null || true`)
- **Proxy scripts** — `mcp-proxy.sh` and `mcp-proxy.ps1` now support both `--headed` and `--headless` flags
- **Documentation overhaul** — README and Manual rewritten with two MCP connection options (URL vs stdio-proxy), config file paths for Claude Code/Cursor, session restart notes

### Fixed

- **WebView2 orphan processes** — proper cleanup chain: event unsubscription in `WebView2ControlAdapter.DisposeAsync`, `CoreWebView2Environment` disposal in `OverlayWindow`, `BrowserCellViewModel.Dispose` calls `Engine.DisposeAsync`, `MainWindow.Closing` triggers `CloseAll`, `App.OnExit` safety net
- **CI macOS artifacts** — removed full `Chromium.app` bundle from macOS tar.gz (kept only `chromium_headless_shell`), eliminates xattr permission errors on `.app` bundle files

## [1.2.0] - 2026-05-12

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
- **Headed by default** — `GDD.Headless` now launches visible Chromium windows by default. Use `--headless` for CI/CD. `--headed` flag still works for explicit override. MCP proxy scripts support both flags
- **Copyright** — assembly copyright `imVS©, freeware for private use.` in both BrowserXn and GDD.Headless
- **Console & network diagnostics on ViewModel** — ConsoleErrorCount, NetworkErrorCount, LastError fields on BrowserCellViewModel
- **Stdout port output** — GDD prints `GDD MCP server listening on http://localhost:{port}` on startup
- **MCP endpoint in status bar** — moved from toolbar to bottom-left for cleaner UI

### Changed

- **Screenshots switched to JPEG at CSS pixel resolution** — ~5-10x smaller (50KB vs 500KB), coordinates in the image match CSS pixels directly for accurate `gdd_tap(x, y)`. Optional `quality` parameter (1-100, default 80)
- **Deterministic page load** — screenshot waits for CDP `Page.loadEventFired` event (WebView2) or `WaitForLoadStateAsync(LoadState.Load)` (Playwright) instead of readyState polling + arbitrary delay
- **Screenshot scroll fix** — CDP clip coordinates now use `cssLayoutViewport.pageX/pageY` as origin, fixing intermittent black top half when page is scrolled
- Default `FrontendUrl` changed from `http://localhost:5173` to `about:blank` (faster startup, no dependency on dev server)
- Main window opens at 30% of screen size (was maximized)
- Overlay window title and size update on device change
- `gdd_set_device` now calls `NavigateAsync` after applying device metrics (page reloads with correct viewport)
- WebView2ControlAdapter uses Dispatcher marshaling for all CDP calls

### Fixed

- mcp-proxy scripts (`.sh`, `.ps1`) and content files not included in `dotnet publish` output — changed from `None` to `Content` items in csproj

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
