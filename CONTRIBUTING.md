# Contributing to GDD

Thanks for your interest in contributing to GDD!

## Getting Started

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/GDD.git
   ```
3. Install prerequisites:
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Windows GUI only: [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
4. Build:
   ```bash
   # Shared core library
   dotnet build src/GDD.Core/GDD.Core.csproj

   # Windows GUI (Windows only)
   dotnet build src/BrowserXn/BrowserXn.csproj

   # Headless (any platform)
   dotnet build src/GDD.Headless/GDD.Headless.csproj
   ```

## Development Workflow

1. Create a feature branch from `master`:
   ```
   git checkout -b feature/your-feature
   ```
2. Make your changes
3. Test locally — run GDD and verify MCP tools work via HTTP
4. Commit with a clear message
5. Push and open a Pull Request

## Project Structure

```
BrowserXn.sln
├── src/
│   ├── GDD.Core/                ← Shared library (net8.0, cross-platform)
│   │   ├── Abstractions/        ← IBrowserEngine, IPlayerManager, IPlayerContext,
│   │   │                          IMainThreadDispatcher, ICdpEventSubscription
│   │   ├── Mcp/                 ← MCP server, tool registry, JSON-RPC protocol
│   │   │   └── Tools/           ← 33 MCP tools organized by category
│   │   ├── Models/              ← Device, Location, Network presets, AppConfig
│   │   ├── Services/            ← CDP, Emulation, Interception, Monitoring
│   │   └── Collections/         ← RingBuffer
│   ├── BrowserXn/               ← Windows GUI (net8.0-windows, WPF + WebView2)
│   │   ├── Engines/             ← WebView2ControlAdapter : IBrowserEngine
│   │   ├── Platform/            ← WpfDispatcher, WebView2CdpSubscription
│   │   ├── ViewModels/          ← MainViewModel : IPlayerManager
│   │   ├── Views/               ← WPF XAML views
│   │   └── Interop/             ← Win32 P/Invoke (DWM, DarkTitleBar)
│   └── GDD.Headless/            ← Headless runner (net8.0, cross-platform)
│       ├── Engines/             ← PlaywrightEngine : IBrowserEngine
│       ├── Platform/            ← ConsoleDispatcher, HeadlessPlayerManager,
│       │                          HeadlessPlayerContext, PlaywrightSetup
│       └── Scripts/             ← mcp-proxy.sh (bash), mcp-proxy.ps1 (pwsh)
└── .github/workflows/build.yml  ← CI/CD for all platforms
```

## Adding a New MCP Tool

1. Create or update a file in `src/GDD.Core/Mcp/Tools/`
2. Follow the existing pattern:
   ```csharp
   registry.Register(
       new McpToolDefinition
       {
           Name = "gdd_your_tool",
           Description = "One-line description",
           InputSchema = new { type = "object", properties = new { ... } }
       },
       async args => { /* implementation using IPlayerManager */ });
   ```
3. Register in tool registration code (see `App.xaml.cs` for GUI, `Program.cs` for Headless)
4. Update `GDD-MANUAL.md` with the new tool
5. Update the smoke test tool count in `.github/workflows/build.yml`

## Code Style

- C# 12 with nullable reference types enabled
- File-scoped namespaces
- `ObservableProperty` and `RelayCommand` via CommunityToolkit.Mvvm (WPF only)
- Minimal comments — code should be self-documenting
- All MCP tools must work through abstractions (`IBrowserEngine`, `IPlayerManager`) — never reference WPF or Playwright types in GDD.Core

## Testing

```bash
# Build entire solution
dotnet build BrowserXn.sln

# Quick verification: launch headless and check tools
dotnet run --project src/GDD.Headless/GDD.Headless.csproj &
curl -X POST http://localhost:9700/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

## Reporting Issues

Use [GitHub Issues](https://github.com/Cap-of-tea/GDD/issues) with the provided templates:
- **Bug Report** — for crashes, incorrect behavior, or broken tools
- **Feature Request** — for new tools, presets, or capabilities

## License

GDD is **Source Available — Non-Commercial**. By contributing, you agree that your contributions are licensed under the same terms (see [LICENSE](LICENSE), Section 6). Commercial use requires a separate license from the Copyright Holder.
