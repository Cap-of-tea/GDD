# Contributing to GDD

Thanks for your interest in contributing to GDD!

## Getting Started

1. Fork the repository
2. Clone your fork:
   ```powershell
   git clone https://github.com/YOUR_USERNAME/GDD.git
   ```
3. Install prerequisites:
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
4. Build:
   ```powershell
   dotnet build src/BrowserXn/BrowserXn.csproj
   ```

## Development Workflow

1. Create a feature branch from `master`:
   ```
   git checkout -b feature/your-feature
   ```
2. Make your changes
3. Test locally — run `GDD.exe` and verify MCP tools work
4. Commit with a clear message
5. Push and open a Pull Request

## Project Structure

```
src/BrowserXn/
├── Interop/          # Win32 P/Invoke (DWM, DarkTitleBar, WebView2Check)
├── Mcp/              # MCP server, tool registry, JSON-RPC handling
│   └── Tools/        # 25 MCP tools organized by category
├── Models/           # Device, Location, Network presets, AppConfig
├── Services/         # Emulation, Auth, Monitoring services
├── ViewModels/       # MVVM ViewModels (Main, BrowserCell, Overlay)
├── Views/            # WPF XAML views and code-behind
├── Converters/       # XAML value converters
├── Engines/          # Browser engine abstraction
└── Themes/           # WPF styles and theme resources
```

## Adding a New MCP Tool

1. Create or update a file in `src/BrowserXn/Mcp/Tools/`
2. Follow the existing pattern:
   ```csharp
   registry.Register(
       new McpToolDefinition
       {
           Name = "gdd_your_tool",
           Description = "One-line description",
           InputSchema = new { type = "object", properties = new { ... } }
       },
       async args => { /* implementation */ });
   ```
3. Register in `App.xaml.cs` → `RegisterMcpTools()`
4. Update `GDD-MANUAL.md` with the new tool

## Code Style

- C# 12 with nullable reference types enabled
- File-scoped namespaces
- `ObservableProperty` and `RelayCommand` via CommunityToolkit.Mvvm
- Minimal comments — code should be self-documenting
- No `var` when type isn't obvious from the right side

## Reporting Issues

Use [GitHub Issues](https://github.com/Cap-of-tea/GDD/issues) with the provided templates:
- **Bug Report** — for crashes, incorrect behavior, or broken tools
- **Feature Request** — for new tools, presets, or capabilities
