using System.Collections.Concurrent;
using System.Text.Json;
using GDD.Abstractions;
using GDD.Models;
using Microsoft.Playwright;
using Serilog;

namespace GDD.Headless.Engines;

public sealed class PlaywrightEngine : IBrowserEngine
{
    private static readonly ILogger Logger = Log.ForContext<PlaywrightEngine>();

    private readonly IBrowser _browser;
    private readonly AppConfig _config;
    private IBrowserContext? _context;
    private IPage? _page;
    private ICDPSession? _cdpSession;
    private readonly ConcurrentDictionary<IRequest, string> _requestIds = new();

    public int PlayerId { get; }
    public string UserDataFolder { get; }
    public bool IsInitialized => _page is not null;
    public string CurrentUrl => _page?.Url ?? string.Empty;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;
    public event EventHandler<string>? NavigationCompleted;
    public event EventHandler<string>? TitleChanged;

    public PlaywrightEngine(int playerId, IBrowser browser, AppConfig config)
    {
        PlayerId = playerId;
        _browser = browser;
        _config = config;
        UserDataFolder = Path.Combine(config.GetDataFolderRoot(), $"Player_{playerId}");
    }

    public async Task InitializeAsync(object? hostHandle, string startUrl)
    {
        var device = DevicePresets.Default;
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = device.Width, Height = device.Height },
            DeviceScaleFactor = (float)device.DeviceScaleFactor,
            IsMobile = device.IsMobile,
            UserAgent = device.UserAgent,
            Permissions = ["notifications"]
        });

        _page = await _context.NewPageAsync();
        _cdpSession = await _context.NewCDPSessionAsync(_page);

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
            NavigationCompleted?.Invoke(this, _page?.Url ?? "");
            try
            {
                var title = await _page!.TitleAsync();
                if (!string.IsNullOrEmpty(title))
                    TitleChanged?.Invoke(this, title);
            }
            catch { }
        };

        if (!string.IsNullOrEmpty(startUrl))
        {
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

        Logger.Information("Playwright engine initialized for Player {Id}", PlayerId);
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
        for (var i = 0; i < 10; i++)
        {
            var state = await _page.EvaluateAsync<string>("document.readyState");
            if (state == "complete") break;
            await Task.Delay(200);
        }
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

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
    {
        var sub = new PlaywrightCdpSubscription();
        if (_page is null) return sub;

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

        return sub;
    }

    public async ValueTask DisposeAsync()
    {
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
