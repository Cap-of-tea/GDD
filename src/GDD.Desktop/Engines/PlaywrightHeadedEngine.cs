using System.Collections.Concurrent;
using System.Text.Json;
using GDD.Abstractions;
using GDD.Models;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Desktop.Engines;

/// <summary>
/// Playwright engine for GDD.Desktop. Same as GDD.Headless PlaywrightEngine, plus CDP
/// input dispatch (Input.dispatch*) so the in-app interactive overlay can drive the page.
/// Runs headless by default (no OS windows); the grid shows polled thumbnails.
/// </summary>
public sealed class PlaywrightHeadedEngine : IBrowserEngine
{
    private static readonly ILogger Logger = Log.ForContext<PlaywrightHeadedEngine>();

    private readonly IBrowser _browser;
    private readonly AppConfig _config;
    private readonly DevicePreset _initialDevice;
    private IBrowserContext? _context;
    private IPage? _page;
    private ICDPSession? _cdpSession;
    private readonly ConcurrentDictionary<IRequest, string> _requestIds = new();
    // Live CDP-event subscriptions (console/network) created by services. Kept so they
    // can be re-bound to a fresh page when the window is reopened after the user closes it.
    private readonly Dictionary<string, PlaywrightCdpSubscription> _subs = new();
    private string _lastUrl = "about:blank";
    private bool _disposing;
    private bool _reopening;

    public int PlayerId { get; }
    public string UserDataFolder { get; }
    public bool IsInitialized => _page is not null;
    public string CurrentUrl => _page?.Url ?? string.Empty;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;
    public event EventHandler<string>? NavigationCompleted;
    public event EventHandler<string>? TitleChanged;

    /// <summary>Fires when the page/window is closed (e.g. user clicks the window's X).</summary>
    public event EventHandler? PageClosed;

    public PlaywrightHeadedEngine(int playerId, IBrowser browser, AppConfig config, DevicePreset? device = null)
    {
        PlayerId = playerId;
        _browser = browser;
        _config = config;
        _initialDevice = device ?? DevicePresets.Default;
        UserDataFolder = Path.Combine(config.GetDataFolderRoot(), $"Player_{playerId}");
    }

    public async Task InitializeAsync(object? hostHandle, string startUrl)
    {
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = _initialDevice.Width, Height = _initialDevice.Height },
            DeviceScaleFactor = (float)_initialDevice.DeviceScaleFactor,
            IsMobile = _initialDevice.IsMobile,
            HasTouch = _initialDevice.HasTouch,
            UserAgent = _initialDevice.UserAgent,
            Permissions = ["notifications"]
        });

        await ConfigureNewPageAsync(startUrl);
        Logger.Information("Playwright engine initialized for Player {Id}", PlayerId);
    }

    /// <summary>
    /// Creates the page in the (already-created) context and wires every per-page hook:
    /// CDP session, the notification bridge, navigation tracking and re-binding of any
    /// CDP-event subscriptions. Used for the initial page and to reopen the page after the
    /// user closes the real window — so the player returns to the grid instead of being
    /// destroyed. Cookies/storage persist because the context is reused.
    /// </summary>
    private async Task ConfigureNewPageAsync(string startUrl)
    {
        _page = await _context!.NewPageAsync();
        _cdpSession = await _context.NewCDPSessionAsync(_page);
        _windowId = null; // a fresh page means a fresh OS window

        _page.Close += OnPageClose;

        await _page.ExposeFunctionAsync<string, int>("__gddNotify", json =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                NotificationReceived?.Invoke(this, new NotificationEventArgs
                {
                    Title = root.GetProperty("title").GetString() ?? "",
                    Body = root.GetProperty("body").GetString() ?? "",
                    IconUri = root.GetProperty("icon").GetString(),
                    BadgeUri = root.GetProperty("badge").GetString(),
                    Tag = root.GetProperty("tag").GetString()
                });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to parse notification for Player {Id}", PlayerId);
            }
            return 0;
        });

        await _page.AddInitScriptAsync(@"
            (function() {
                window.Notification = function(title, options) {
                    options = options || {};
                    if (window.__gddNotify) {
                        window.__gddNotify(JSON.stringify({
                            title: title || '',
                            body: options.body || '',
                            icon: options.icon || '',
                            badge: options.badge || '',
                            tag: options.tag || ''
                        }));
                    }
                };
                window.Notification.permission = 'granted';
                window.Notification.requestPermission = async () => 'granted';
            })();
        ");

        _page.Load += async (_, _) =>
        {
            _lastUrl = _page?.Url ?? _lastUrl;
            NavigationCompleted?.Invoke(this, _page?.Url ?? "");
            try
            {
                var title = await _page!.TitleAsync();
                if (!string.IsNullOrEmpty(title))
                    TitleChanged?.Invoke(this, title);
            }
            catch { }
        };

        // Re-bind console/network subscriptions (created by services before a reopen)
        // to this fresh page so diagnostics keep flowing.
        foreach (var (eventName, sub) in _subs)
            WireCdpEvent(eventName, sub);

        if (!string.IsNullOrEmpty(startUrl))
        {
            _lastUrl = startUrl;
            try
            {
                await _page.GotoAsync(startUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 30000
                });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Initial navigation failed for Player {Id}: {Url}", PlayerId, startUrl);
            }
        }
    }

    private async void OnPageClose(object? sender, IPage page)
    {
        // Intentional teardown (DisposeAsync): let the player be removed.
        if (_disposing)
        {
            PageClosed?.Invoke(this, EventArgs.Empty);
            return;
        }
        // The user clicked the window's X. Don't destroy the player — reopen the page in
        // the same context and park it back off-screen, so the grid thumbnail returns.
        if (_reopening || _context is null) return;
        _reopening = true;
        try
        {
            await ConfigureNewPageAsync(_lastUrl);
            await HideOffscreenAsync();
            Logger.Information("Player {Id}: window closed by user — reopened in grid", PlayerId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Reopen after window close failed for Player {Id}", PlayerId);
            PageClosed?.Invoke(this, EventArgs.Empty); // could not recover — let it be removed
        }
        finally { _reopening = false; }
    }

    public async Task NavigateAsync(string url)
    {
        if (_page is null) return;
        try
        {
            await _page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });
        }
        catch (TimeoutException)
        {
            Logger.Warning("Navigation timeout for Player {Id}: {Url}", PlayerId, url);
        }
    }

    public async Task<string> ExecuteJavaScriptAsync(string script)
    {
        if (_page is null) return "null";
        try
        {
            var result = await _page.EvaluateAsync<JsonElement>(script);
            return result.GetRawText();
        }
        catch
        {
            return "null";
        }
    }

    public async Task CallCdpMethodAsync(string methodName, string parametersJson)
    {
        if (_cdpSession is null) return;
        await _cdpSession.SendAsync(methodName, DeserializeCdpParams(parametersJson));

        // Playwright tracks its own viewport; page.ScreenshotAsync (used for thumbnails and
        // the overlay) clips to it, ignoring a raw CDP setDeviceMetricsOverride. Mirror device
        // metric changes into Playwright's native viewport so device switches actually apply.
        if (methodName == "Emulation.setDeviceMetricsOverride" && _page is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(parametersJson);
                var r = doc.RootElement;
                if (r.TryGetProperty("width", out var w) && r.TryGetProperty("height", out var h)
                    && w.GetInt32() > 0 && h.GetInt32() > 0)
                {
                    await _page.SetViewportSizeAsync(w.GetInt32(), h.GetInt32());
                }
            }
            catch (Exception ex) { Logger.Debug("viewport mirror failed P{Id}: {M}", PlayerId, ex.Message); }
        }
    }

    public async Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson)
    {
        if (_cdpSession is null) return "{}";
        var result = await _cdpSession.SendAsync(methodName, DeserializeCdpParams(parametersJson));
        return result.HasValue ? result.Value.GetRawText() : "{}";
    }

    public async Task<byte[]> CaptureScreenshotAsync(int quality = 80)
    {
        if (_page is null) return [];
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 });
        }
        catch (TimeoutException) { }
        return await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Type = ScreenshotType.Jpeg,
            Quality = quality,
            Scale = ScreenshotScale.Css
        });
    }

    public async Task InjectScriptOnDocumentCreatedAsync(string script)
    {
        if (_page is null) return;
        await _page.AddInitScriptAsync(script);
    }

    // ── Real-window control (headed mode) ────────────────────────────────

    private long? _windowId;

    /// <summary>True when the real Chromium window is parked off-screen.</summary>
    public bool IsWindowHidden { get; private set; } = true;

    private async Task<long?> EnsureWindowIdAsync()
    {
        if (_windowId is not null) return _windowId;
        if (_cdpSession is null) return null;
        try
        {
            var res = await _cdpSession.SendAsync("Browser.getWindowForTarget");
            if (res.HasValue && res.Value.TryGetProperty("windowId", out var wid))
                _windowId = wid.GetInt64();
        }
        catch (Exception ex) { Logger.Debug("getWindowForTarget failed P{Id}: {M}", PlayerId, ex.Message); }
        return _windowId;
    }

    /// <summary>Move the window far off-screen (still rendering, unlike minimized).</summary>
    public async Task HideOffscreenAsync()
    {
        IsWindowHidden = true;
        var id = await EnsureWindowIdAsync();
        if (id is null || _cdpSession is null) return;
        try
        {
            await _cdpSession.SendAsync("Browser.setWindowBounds", new Dictionary<string, object>
            {
                ["windowId"] = id.Value,
                ["bounds"] = new Dictionary<string, object> { ["left"] = -32000, ["top"] = -32000 }
            });
        }
        catch (Exception ex) { Logger.Debug("HideOffscreen failed P{Id}: {M}", PlayerId, ex.Message); }
    }

    /// <summary>Bring the real Chromium window on-screen and focus it for native interaction.</summary>
    public async Task RestoreWindowAsync()
    {
        IsWindowHidden = false;
        var id = await EnsureWindowIdAsync();
        if (id is not null && _cdpSession is not null)
        {
            try
            {
                // Window is already in "normal" state (just parked off-screen) — set position
                // only. Combining bounds with windowState makes CDP ignore the bounds.
                await _cdpSession.SendAsync("Browser.setWindowBounds", new Dictionary<string, object>
                {
                    ["windowId"] = id.Value,
                    ["bounds"] = new Dictionary<string, object>
                    {
                        ["left"] = 120, ["top"] = 80,
                        ["width"] = _initialDevice.Width + 20, ["height"] = _initialDevice.Height + 140
                    }
                });
            }
            catch (Exception ex) { Logger.Debug("Restore failed P{Id}: {M}", PlayerId, ex.Message); }
        }
        if (_page is not null)
        {
            try { await _page.BringToFrontAsync(); } catch { }
        }
    }

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
    {
        if (_subs.TryGetValue(eventName, out var existing))
            return existing;

        var sub = new PlaywrightCdpSubscription();
        _subs[eventName] = sub;
        WireCdpEvent(eventName, sub);
        return sub;
    }

    /// <summary>Binds the current page's Playwright events to a CDP subscription. Re-run
    /// for each subscription whenever the page is (re)created.</summary>
    private void WireCdpEvent(string eventName, PlaywrightCdpSubscription sub)
    {
        if (_page is null) return;

        switch (eventName)
        {
            case "Runtime.consoleAPICalled":
                _page.Console += (_, msg) =>
                {
                    var valueJson = JsonSerializer.Serialize(msg.Text);
                    var typeJson = JsonSerializer.Serialize(msg.Type);
                    var json = $"{{\"type\":{typeJson},\"args\":[{{\"type\":\"string\",\"value\":{valueJson}}}]}}";
                    sub.Fire(json);
                };
                break;

            case "Runtime.exceptionThrown":
                _page.PageError += (_, error) =>
                {
                    var descJson = JsonSerializer.Serialize(error);
                    var json = $"{{\"exceptionDetails\":{{\"text\":\"Uncaught\",\"exception\":{{\"description\":{descJson}}}}}}}";
                    sub.Fire(json);
                };
                break;

            case "Network.requestWillBeSent":
                _page.Request += (_, request) =>
                {
                    var id = Guid.NewGuid().ToString("N");
                    _requestIds[request] = id;
                    var json = $"{{\"requestId\":{S(id)},\"request\":{{\"method\":{S(request.Method)},\"url\":{S(request.Url)}}},\"type\":{S(request.ResourceType)}}}";
                    sub.Fire(json);
                };
                break;

            case "Network.responseReceived":
                _page.Response += (_, response) =>
                {
                    if (!_requestIds.TryGetValue(response.Request, out var id)) return;
                    var headersObj = new Dictionary<string, string>();
                    if (response.Headers.TryGetValue("content-length", out var cl))
                        headersObj["content-length"] = cl;
                    var headersJson = JsonSerializer.Serialize(headersObj);
                    var json = $"{{\"requestId\":{S(id)},\"response\":{{\"status\":{response.Status},\"statusText\":{S(response.StatusText)},\"mimeType\":\"\",\"headers\":{headersJson}}}}}";
                    sub.Fire(json);
                };
                break;

            case "Network.loadingFinished":
                _page.RequestFinished += (_, request) =>
                {
                    if (!_requestIds.TryRemove(request, out var id)) return;
                    sub.Fire($"{{\"requestId\":{S(id)}}}");
                };
                break;

            case "Network.loadingFailed":
                _page.RequestFailed += (_, request) =>
                {
                    if (!_requestIds.TryRemove(request, out var id)) return;
                    var errorText = request.Failure ?? "Unknown error";
                    sub.Fire($"{{\"requestId\":{S(id)},\"errorText\":{S(errorText)}}}");
                };
                break;

            default:
                Logger.Debug("CDP event {Event} not mapped for Playwright", eventName);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposing = true;
        if (_cdpSession is not null)
        {
            try { await _cdpSession.DetachAsync(); } catch { }
            _cdpSession = null;
        }
        if (_page is not null)
        {
            try { await _page.CloseAsync(); } catch { }
            _page = null;
        }
        if (_context is not null)
        {
            try { await _context.CloseAsync(); } catch { }
            _context = null;
        }
    }

    private static Dictionary<string, object>? DeserializeCdpParams(string parametersJson)
    {
        if (string.IsNullOrEmpty(parametersJson) || parametersJson == "{}")
            return null;
        return JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);
    }

    private static string S(string value) => JsonSerializer.Serialize(value);
}
