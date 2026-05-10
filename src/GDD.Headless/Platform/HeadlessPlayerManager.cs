using GDD.Abstractions;
using GDD.Headless.Engines;
using GDD.Models;
using GDD.Services;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Headless.Platform;

public sealed class HeadlessPlayerManager : IPlayerManager, IAsyncDisposable
{
    private static readonly ILogger Logger = Log.ForContext<HeadlessPlayerManager>();

    private readonly AppConfig _config;
    private readonly NotificationInterceptionService _notificationService;
    private readonly ConsoleInterceptionService _consoleService;
    private readonly NetworkMonitoringService _networkMonitorService;
    private readonly List<HeadlessPlayerContext> _players = new();
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _nextPlayerId = 1;

    public HeadlessPlayerManager(
        AppConfig config,
        NotificationInterceptionService notificationService,
        ConsoleInterceptionService consoleService,
        NetworkMonitoringService networkMonitorService)
    {
        _config = config;
        _notificationService = notificationService;
        _consoleService = consoleService;
        _networkMonitorService = networkMonitorService;
    }

    public IReadOnlyList<IPlayerContext> GetPlayers() => _players;

    public IPlayerContext? GetPlayer(int playerId) =>
        _players.FirstOrDefault(p => p.PlayerId == playerId);

    public IReadOnlyList<int> AddPlayers(int count, string? devicePreset = null)
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
            var playerId = _nextPlayerId++;
            var ctx = new HeadlessPlayerContext(playerId, _config.FrontendUrl)
            {
                SelectedDevice = device
            };
            _players.Add(ctx);
            ids.Add(playerId);
            _ = InitializePlayerAsync(ctx);
        }
        return ids;
    }

    public void RemovePlayer(int playerId)
    {
        var player = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player is null) return;

        _players.Remove(player);
        _consoleService.Remove(playerId);
        _networkMonitorService.Remove(playerId);

        if (player.Engine is not null)
            _ = player.Engine.DisposeAsync();
    }

    private async Task InitializePlayerAsync(HeadlessPlayerContext ctx)
    {
        try
        {
            await EnsureBrowserAsync();
            var engine = new PlaywrightEngine(ctx.PlayerId, _browser!, _config);
            await engine.InitializeAsync(null, ctx.CurrentUrl);
            ctx.Engine = engine;

            engine.NavigationCompleted += (_, url) => ctx.CurrentUrl = url;
            engine.TitleChanged += (_, title) => ctx.StatusText = title;

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

            ctx.StatusText = "Ready";
            Logger.Information("Player {Id} initialized", ctx.PlayerId);
        }
        catch (Exception ex)
        {
            ctx.StatusText = $"Init failed: {ex.Message}";
            Logger.Error(ex, "Failed to initialize Player {Id}", ctx.PlayerId);
        }
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
                Headless = true
            });
            Logger.Information("Chromium launched (headless)");
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var player in _players.ToList())
        {
            if (player.Engine is not null)
                await player.Engine.DisposeAsync();
        }
        _players.Clear();

        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
    }
}
