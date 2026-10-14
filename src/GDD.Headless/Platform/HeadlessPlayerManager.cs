using GDD.Abstractions;
using GDD.Headless.Engines;
using GDD.Mcp;
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
    private readonly RequestInterceptionService _interceptionService;
    private readonly object _sync = new();
    private readonly List<HeadlessPlayerContext> _players = new();
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _nextPlayerId = 1;
    private readonly TimeSpan _idleTimeout;
    private readonly System.Threading.Timer? _idleReaper;

    public HeadlessPlayerManager(
        AppConfig config,
        NotificationInterceptionService notificationService,
        ConsoleInterceptionService consoleService,
        NetworkMonitoringService networkMonitorService,
        RequestInterceptionService interceptionService)
    {
        _config = config;
        _notificationService = notificationService;
        _consoleService = consoleService;
        _networkMonitorService = networkMonitorService;
        _interceptionService = interceptionService;

        // A streamable-HTTP client (POST /mcp) has no persistent connection and no disconnect
        // event: it mints a session, may add players, then simply stops calling. Nothing reaps
        // those players, so their Chromium contexts (and ~1 GB renderer processes each) pile up
        // until the container OOMs. The idle reaper is the only cleanup that covers this; it also
        // catches SSE clients that die without a clean close. Set
        // GDD_PLAYER_IDLE_TIMEOUT_MINUTES=0 to disable.
        _idleTimeout = ParseIdleTimeout();
        if (_idleTimeout > TimeSpan.Zero)
        {
            // Check at least twice per idle window, but never more often than every 30 s nor
            // less often than every 5 min.
            var interval = TimeSpan.FromSeconds(Math.Clamp(_idleTimeout.TotalSeconds / 2, 30, 300));
            _idleReaper = new System.Threading.Timer(_ => ReapIdlePlayers(), null, interval, interval);
            Logger.Information("Idle player reaper enabled: timeout {Minutes} min, checked every {Seconds} s",
                _idleTimeout.TotalMinutes, interval.TotalSeconds);
        }
    }

    private static TimeSpan ParseIdleTimeout()
    {
        var raw = Environment.GetEnvironmentVariable("GDD_PLAYER_IDLE_TIMEOUT_MINUTES");
        if (int.TryParse(raw, out var minutes))
            return minutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(30);
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
        HeadlessPlayerContext? player;
        lock (_sync)
            player = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player is null) return null;

        var sessionId = McpSessionContext.CurrentSessionId;
        if (sessionId is not null && player.OwnerSessionId is not null && player.OwnerSessionId != sessionId)
            return null;

        // A tool is about to act on this player — keep it off the idle reaper's list.
        player.LastActivityUtc = DateTime.UtcNow;
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
            HeadlessPlayerContext ctx;
            lock (_sync)
            {
                var playerId = _nextPlayerId++;
                ctx = new HeadlessPlayerContext(playerId, _config.FrontendUrl)
                {
                    SelectedDevice = device,
                    OwnerSessionId = sessionId
                };
                _players.Add(ctx);
                ids.Add(playerId);
            }
            _ = InitializePlayerAsync(ctx);
        }
        return ids;
    }

    public void RemovePlayer(int playerId)
    {
        HeadlessPlayerContext? player;
        lock (_sync)
        {
            player = _players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player is not null)
                _players.Remove(player);
        }
        if (player is null) return;

        _consoleService.Remove(playerId);
        _networkMonitorService.Remove(playerId);
        _interceptionService.Remove(playerId);

        if (player.Engine is not null)
            _ = player.Engine.DisposeAsync();
    }

    /// <summary>
    /// Removes players left untouched longer than the idle timeout. Runs regardless of owner —
    /// headless has no GUI windows to preserve, so any abandoned player (from a disconnected or
    /// silent session) is fair game. RemovePlayer disposes the engine, which closes the browser
    /// context and tears down its renderer process.
    /// </summary>
    private void ReapIdlePlayers()
    {
        try
        {
            var cutoff = DateTime.UtcNow - _idleTimeout;
            List<HeadlessPlayerContext> stale;
            lock (_sync)
                stale = _players.Where(p => p.LastActivityUtc < cutoff).ToList();

            foreach (var p in stale)
            {
                Logger.Information("Reaping idle player {Id}: idle {Minutes:F0} min, owner {Owner}",
                    p.PlayerId, (DateTime.UtcNow - p.LastActivityUtc).TotalMinutes, p.OwnerSessionId ?? "none");
                RemovePlayer(p.PlayerId);
            }

            if (stale.Count > 0)
                Logger.Information("Idle reaper removed {Count} player(s)", stale.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Idle reaper pass failed");
        }
    }

    private async Task InitializePlayerAsync(HeadlessPlayerContext ctx)
    {
        try
        {
            await EnsureBrowserAsync();
            var engine = new PlaywrightEngine(ctx.PlayerId, _browser!, _config, ctx.SelectedDevice);
            await engine.InitializeAsync(null, ctx.CurrentUrl);

            // Counters subscribe to the shared services and filter by player id — wire them once
            // per player. The engine-level attach can be redone when the context is rebuilt.
            WireServiceCounters(ctx);
            await AttachEngineAsync(ctx, engine);

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

    // Per-player counters that read from the shared services. Added once, at creation — re-adding
    // them on a context rebuild would double-count every console error, failed request and push.
    private void WireServiceCounters(HeadlessPlayerContext ctx)
    {
        _notificationService.NotificationReceived += (_, n) =>
        {
            if (n.PlayerId == ctx.PlayerId)
                ctx.NotificationCount++;
        };
        _consoleService.EntryReceived += (_, entry) =>
        {
            if (entry.PlayerId != ctx.PlayerId) return;
            if (entry.Level == "error")
            {
                ctx.ConsoleErrorCount = _consoleService.GetErrorCount(ctx.PlayerId);
                ctx.LastError = entry.Message;
            }
        };
        _networkMonitorService.RequestFailed += (_, entry) =>
        {
            if (entry.PlayerId == ctx.PlayerId)
                ctx.NetworkErrorCount++;
        };
    }

    // Bind engine events and the CDP-level interceptors to a (possibly new) engine, then publish
    // it as the player's current engine. Safe to call again after rebuilding the context.
    private async Task AttachEngineAsync(HeadlessPlayerContext ctx, PlaywrightEngine engine)
    {
        engine.NavigationCompleted += (_, url) => ctx.CurrentUrl = url;
        engine.TitleChanged += (_, title) => ctx.StatusText = title;
        _notificationService.Attach(engine, ctx.PlayerId);
        await _consoleService.AttachAsync(engine, ctx.PlayerId);
        await _networkMonitorService.AttachAsync(engine, ctx.PlayerId);
        ctx.Engine = engine;
    }

    public async Task<bool> TrySwitchDeviceAsync(int playerId, string presetName)
    {
        HeadlessPlayerContext? ctx;
        lock (_sync)
            ctx = _players.FirstOrDefault(p => p.PlayerId == playerId);
        if (ctx is null) return true; // nothing to switch; caller already validated existence

        var preset = DevicePresets.All.FirstOrDefault(d =>
            d.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase)) ?? DevicePresets.Default;

        var url = ctx.CurrentUrl;
        ctx.SelectedDevice = preset;
        ctx.StatusText = "Switching device...";

        // Playwright fixes per-context emulation at creation, so a true device switch means a new
        // context. Detach the interceptors from the old page, build the new context, re-attach,
        // then close the old one. Cookies and storage do not carry over — a switch is a fresh
        // session (as is any gdd_set_location / gdd_set_network override).
        _consoleService.Remove(playerId);
        _networkMonitorService.Remove(playerId);
        _interceptionService.Remove(playerId);

        var old = ctx.Engine;
        try
        {
            var engine = new PlaywrightEngine(playerId, _browser!, _config, preset);
            await engine.InitializeAsync(null, url);
            await AttachEngineAsync(ctx, engine); // publishes the new engine as ctx.Engine
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Device switch failed for player {Id}", playerId);
            ctx.StatusText = $"Device switch failed: {ex.Message}";
        }

        if (old is not null)
        {
            try { await old.DisposeAsync(); }
            catch (Exception ex) { Logger.Warning(ex, "Disposing old context after device switch, player {Id}", playerId); }
        }

        ctx.StatusText = "Ready";
        ctx.LastActivityUtc = DateTime.UtcNow;
        Logger.Information("Player {Id} switched to {Device} (context rebuilt)", playerId, preset.Name);
        return true;
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser is not null) return;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null) return;
            _playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions { Headless = !_config.Headed };
            if (_config.Stealth)
            {
                var args = new List<string> { "--disable-blink-features=AutomationControlled" };
                if (_config.StealthMax)
                {
                    // Stop WebRTC from revealing the real/host IP outside the proxy path.
                    args.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
                    // Drop the automation switch Playwright adds by default.
                    launchOptions.IgnoreDefaultArgs = new[] { "--enable-automation" };
                    // Workers inherit the *launch* UA, not the per-context override — so a clean
                    // desktop Chrome UA here removes the "HeadlessChrome" leak CreepJS reads from
                    // the worker context (the per-context UserAgent still governs the main thread).
                    args.Add("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
                }
                launchOptions.Args = args;
            }
            // Optional: use a real Chrome build (proprietary codecs/fonts) when available.
            var channel = Environment.GetEnvironmentVariable("GDD_CHROME_CHANNEL");
            if (!string.IsNullOrWhiteSpace(channel))
                launchOptions.Channel = channel;
            // Optional upstream proxy for all players. GDD_PROXY=scheme://host:port
            // (+ GDD_PROXY_USER / GDD_PROXY_PASS for authenticated proxies).
            var proxyServer = Environment.GetEnvironmentVariable("GDD_PROXY");
            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                launchOptions.Proxy = new Proxy { Server = proxyServer };
                var pUser = Environment.GetEnvironmentVariable("GDD_PROXY_USER");
                var pPass = Environment.GetEnvironmentVariable("GDD_PROXY_PASS");
                if (!string.IsNullOrWhiteSpace(pUser)) launchOptions.Proxy.Username = pUser;
                if (!string.IsNullOrWhiteSpace(pPass)) launchOptions.Proxy.Password = pPass;
            }
            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
            Logger.Information("Chromium launched ({Mode}{Stealth}{Channel})",
                _config.Headed ? "headed" : "headless",
                _config.StealthMax ? ", stealth-max" : _config.Stealth ? ", stealth" : "",
                string.IsNullOrWhiteSpace(channel) ? "" : $", channel={channel}");
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_idleReaper is not null)
            await _idleReaper.DisposeAsync();

        List<HeadlessPlayerContext> snapshot;
        lock (_sync)
        {
            snapshot = _players.ToList();
            _players.Clear();
        }

        foreach (var player in snapshot)
        {
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
