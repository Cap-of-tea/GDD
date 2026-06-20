using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using GDD.Abstractions;
using GDD.Desktop.Engines;
using GDD.Desktop.Services;
using GDD.Mcp;
using GDD.Models;
using GDD.Services;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Desktop.Platform;

/// <summary>
/// IPlayerManager for GDD.Desktop. Mirrors HeadlessPlayerManager (shared IBrowser,
/// one context per player, service wiring) but exposes an ObservableCollection for
/// the Avalonia UI. Browsers run headed with their windows parked off-screen (no desktop
/// clutter); the grid shows polled screenshots and clicking a cell brings the real
/// Chromium window on-screen for native interaction.
/// </summary>
public sealed class DesktopPlayerManager : IPlayerManager, IAsyncDisposable
{
    private static readonly ILogger Logger = Log.ForContext<DesktopPlayerManager>();

    private readonly AppConfig _config;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly IThumbnailService _thumbnailService;
    private readonly NotificationInterceptionService _notificationService;
    private readonly ConsoleInterceptionService _consoleService;
    private readonly NetworkMonitoringService _networkMonitorService;

    private readonly object _sync = new();
    private readonly List<DesktopPlayerContext> _players = new();
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _nextPlayerId = 1;

    /// <summary>UI-bound collection (mutated only on the UI thread).</summary>
    public ObservableCollection<DesktopPlayerContext> Players { get; } = new();

    public DesktopPlayerManager(
        AppConfig config,
        IMainThreadDispatcher dispatcher,
        IThumbnailService thumbnailService,
        NotificationInterceptionService notificationService,
        ConsoleInterceptionService consoleService,
        NetworkMonitoringService networkMonitorService)
    {
        _config = config;
        _dispatcher = dispatcher;
        _thumbnailService = thumbnailService;
        _notificationService = notificationService;
        _consoleService = consoleService;
        _networkMonitorService = networkMonitorService;
    }

    public IReadOnlyList<IPlayerContext> GetPlayers()
    {
        var sessionId = McpSessionContext.CurrentSessionId;
        lock (_sync)
        {
            if (sessionId is null)
                return _players.ToList<IPlayerContext>();
            return _players
                .Where(p => p.OwnerSessionId is null || p.OwnerSessionId == sessionId)
                .ToList<IPlayerContext>();
        }
    }

    public IPlayerContext? GetPlayer(int playerId)
    {
        DesktopPlayerContext? player;
        lock (_sync)
            player = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player is null) return null;

        var sessionId = McpSessionContext.CurrentSessionId;
        if (sessionId is not null && player.OwnerSessionId is not null && player.OwnerSessionId != sessionId)
            return null;

        return player;
    }

    public IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null, string? sessionId = null)
    {
        var device = DevicePresets.Default;
        if (!string.IsNullOrEmpty(devicePreset))
        {
            var found = DevicePresets.All.FirstOrDefault(d =>
                d.Name.Equals(devicePreset, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
                device = found;
        }

        var ids = new List<int>();
        for (var i = 0; i < count; i++)
        {
            int playerId;
            DesktopPlayerContext ctx;
            lock (_sync)
            {
                playerId = _nextPlayerId++;
                ctx = new DesktopPlayerContext(playerId, _config.FrontendUrl)
                {
                    SelectedDevice = device,
                    OwnerSessionId = sessionId
                };
                _players.Add(ctx);
            }
            ids.Add(playerId);
            _ = _dispatcher.InvokeAsync(() => Players.Add(ctx));
            _ = InitializePlayerAsync(ctx);
        }
        return ids;
    }

    public void RemovePlayer(int playerId)
    {
        DesktopPlayerContext? player;
        lock (_sync)
        {
            player = _players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player is not null)
                _players.Remove(player);
        }
        if (player is null) return;

        _thumbnailService.Stop(playerId);
        _consoleService.Remove(playerId);
        _networkMonitorService.Remove(playerId);
        _ = _dispatcher.InvokeAsync(() => Players.Remove(player));

        if (player.Engine is not null)
            _ = player.Engine.DisposeAsync();
    }

    private async Task InitializePlayerAsync(DesktopPlayerContext ctx)
    {
        try
        {
            await EnsureBrowserAsync();
            var engine = new PlaywrightHeadedEngine(ctx.PlayerId, _browser!, _config, ctx.SelectedDevice);
            await engine.InitializeAsync(null, ctx.CurrentUrl);
            ctx.Engine = engine;

            engine.NavigationCompleted += (_, url) => ctx.CurrentUrl = url;
            engine.TitleChanged += (_, title) => ctx.StatusText = title;
            // Closing the real window (X) no longer drops the player — the engine reopens
            // the page off-screen so the tile stays. PageClosed now fires only on teardown
            // or an unrecoverable reopen failure, where removing the player is correct.
            engine.PageClosed += (_, _) => RemovePlayer(ctx.PlayerId);

            _notificationService.Attach(engine, ctx.PlayerId);
            _notificationService.NotificationReceived += (_, n) =>
            {
                if (n.PlayerId == ctx.PlayerId)
                    ctx.NotificationCount++;
            };

            await _consoleService.AttachAsync(engine, ctx.PlayerId);
            _consoleService.EntryReceived += (_, entry) =>
            {
                if (entry.PlayerId != ctx.PlayerId) return;
                if (entry.Level == "error")
                {
                    ctx.ConsoleErrorCount = _consoleService.GetErrorCount(ctx.PlayerId);
                    ctx.LastError = entry.Message;
                }
            };

            await _networkMonitorService.AttachAsync(engine, ctx.PlayerId);
            _networkMonitorService.RequestFailed += (_, entry) =>
            {
                if (entry.PlayerId == ctx.PlayerId)
                    ctx.NetworkErrorCount++;
            };

            // Headed window moved off-screen (still renders, unlike minimized) so it doesn't
            // clutter the desktop; restored on click for full native interaction.
            await engine.HideOffscreenAsync();

            _thumbnailService.Start(ctx.PlayerId, engine, OnThumbnailFrame);

            ctx.SetEngineReady();
            ctx.StatusText = "Ready";
            Logger.Information("Player {Id} initialized", ctx.PlayerId);
        }
        catch (Exception ex)
        {
            ctx.SetEngineFailed();
            ctx.StatusText = $"Init failed: {ex.Message}";
            Logger.Error(ex, "Failed to initialize Player {Id}", ctx.PlayerId);
        }
    }

    private void OnThumbnailFrame(int playerId, byte[] jpeg)
    {
        DesktopPlayerContext? ctx;
        lock (_sync)
            ctx = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (ctx is null) return;

        Bitmap bmp;
        try
        {
            using var ms = new MemoryStream(jpeg);
            bmp = new Bitmap(ms);
        }
        catch
        {
            return;
        }

        _ = _dispatcher.InvokeAsync(() =>
        {
            // Dispose the frame from two generations ago — it is no longer being rendered.
            ctx.PendingDispose?.Dispose();
            ctx.PendingDispose = ctx.Thumbnail;
            ctx.Thumbnail = bmp;
        });
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser is not null) return;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null) return;
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !_config.Headed,
                // Keep off-screen / occluded windows rendering so screencast thumbnails
                // stay live while the real Chromium windows are hidden until demanded.
                Args =
                [
                    "--disable-backgrounding-occluded-windows",
                    "--disable-features=CalculateNativeWinOcclusion",
                    "--disable-renderer-backgrounding",
                    "--disable-background-timer-throttling"
                ]
            });
            Logger.Information("Chromium launched ({Mode})", _config.Headed ? "headed" : "headless");
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<DesktopPlayerContext> snapshot;
        lock (_sync)
        {
            snapshot = _players.ToList();
            _players.Clear();
        }

        foreach (var player in snapshot)
        {
            _thumbnailService.Stop(player.PlayerId);
            if (player.Engine is not null)
                await player.Engine.DisposeAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
    }
}
