# Changelog

All notable changes to GDD are documented here.

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
  - Help: M2M manual API for agent self-learning
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
