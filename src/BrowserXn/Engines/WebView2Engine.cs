using System.IO;
using Microsoft.Web.WebView2.Core;
using GDD.Abstractions;
using GDD.Platform;
using Serilog;

namespace GDD.Engines;

public sealed class WebView2Engine : IBrowserEngine
{
    private static readonly ILogger Logger = Log.ForContext<WebView2Engine>();

    private CoreWebView2Environment? _environment;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _webView;
    private bool _disposed;

    public int PlayerId { get; }
    public string UserDataFolder { get; }
    public bool IsInitialized => _webView is not null;
    public string CurrentUrl => _webView?.Source ?? string.Empty;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;
    public event EventHandler<string>? NavigationCompleted;
    public event EventHandler<string>? TitleChanged;

    public WebView2Engine(int playerId, string userDataFolder)
    {
        PlayerId = playerId;
        UserDataFolder = userDataFolder;
    }

    public async Task InitializeAsync(object? hostHandle, string startUrl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var parentHwnd = hostHandle is nint hwnd ? hwnd : throw new ArgumentException("WebView2Engine requires nint hostHandle");

        Directory.CreateDirectory(UserDataFolder);

        _environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: UserDataFolder);

        _controller = await _environment.CreateCoreWebView2ControllerAsync(parentHwnd);
        _webView = _controller.CoreWebView2;

        _webView.Settings.IsStatusBarEnabled = false;
        _webView.Settings.AreDefaultContextMenusEnabled = false;
        _webView.Settings.IsZoomControlEnabled = false;

        _webView.PermissionRequested += OnPermissionRequested;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.DocumentTitleChanged += OnDocumentTitleChanged;
        _webView.NotificationReceived += OnNotificationReceived;

        Logger.Information("WebView2 initialized for Player {PlayerId} with data folder {Folder}",
            PlayerId, UserDataFolder);

        _webView.Navigate(startUrl);
    }

    public Task NavigateAsync(string url)
    {
        EnsureInitialized();
        _webView!.Navigate(url);
        return Task.CompletedTask;
    }

    public async Task<string> ExecuteJavaScriptAsync(string script)
    {
        EnsureInitialized();
        return await _webView!.ExecuteScriptAsync(script);
    }

    public async Task CallCdpMethodAsync(string methodName, string parametersJson)
    {
        EnsureInitialized();
        await _webView!.CallDevToolsProtocolMethodAsync(methodName, parametersJson);
    }

    public async Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson)
    {
        EnsureInitialized();
        return await _webView!.CallDevToolsProtocolMethodAsync(methodName, parametersJson);
    }

    public async Task<byte[]> CaptureScreenshotAsync(int quality = 80)
    {
        EnsureInitialized();
        for (var i = 0; i < 10; i++)
        {
            var state = await _webView!.ExecuteScriptAsync("document.readyState");
            if (state.Contains("complete")) break;
            await Task.Delay(200);
        }
        await Task.Delay(150);

        var metricsJson = await _webView!.CallDevToolsProtocolMethodAsync(
            "Page.getLayoutMetrics", "{}");
        using var metrics = System.Text.Json.JsonDocument.Parse(metricsJson);
        var viewport = metrics.RootElement.GetProperty("cssLayoutViewport");
        var w = viewport.GetProperty("clientWidth").GetInt32();
        var h = viewport.GetProperty("clientHeight").GetInt32();

        var cdpParams = $"{{\"format\":\"jpeg\",\"quality\":{quality}," +
            $"\"clip\":{{\"x\":0,\"y\":0,\"width\":{w},\"height\":{h},\"scale\":1}}}}";
        var resultJson = await _webView!.CallDevToolsProtocolMethodAsync(
            "Page.captureScreenshot", cdpParams);

        using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
        var base64 = doc.RootElement.GetProperty("data").GetString()!;
        return Convert.FromBase64String(base64);
    }

    public async Task InjectScriptOnDocumentCreatedAsync(string script)
    {
        EnsureInitialized();
        await _webView!.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
    {
        EnsureInitialized();
        return new WebView2CdpSubscription(_webView!, eventName);
    }

    public void SetBounds(System.Drawing.Rectangle bounds)
    {
        if (_controller is not null)
            _controller.Bounds = bounds;
    }

    public void SetVisible(bool visible)
    {
        if (_controller is not null)
            _controller.IsVisible = visible;
    }

    public nint? GetParentHwnd() => _controller is not null
        ? _controller.ParentWindow
        : null;

    public void ReparentWindow(nint newParentHwnd)
    {
        if (_controller is not null)
            _controller.ParentWindow = newParentHwnd;
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind is CoreWebView2PermissionKind.Notifications
            or CoreWebView2PermissionKind.Geolocation)
        {
            e.State = CoreWebView2PermissionState.Allow;
            Logger.Debug("Auto-granted {Permission} for Player {PlayerId}",
                e.PermissionKind, PlayerId);
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        NavigationCompleted?.Invoke(this, _webView?.Source ?? string.Empty);
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        TitleChanged?.Invoke(this, _webView?.DocumentTitle ?? string.Empty);
    }

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        e.Handled = true;
        NotificationReceived?.Invoke(this, new NotificationEventArgs
        {
            Title = e.Notification.Title ?? "NOISE",
            Body = e.Notification.Body ?? string.Empty,
            IconUri = e.Notification.IconUri,
            BadgeUri = e.Notification.BadgeUri,
            Tag = e.Notification.Tag
        });
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_webView is null)
            throw new InvalidOperationException($"WebView2 for Player {PlayerId} is not initialized.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_webView is not null)
        {
            _webView.PermissionRequested -= OnPermissionRequested;
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.DocumentTitleChanged -= OnDocumentTitleChanged;
            _webView.NotificationReceived -= OnNotificationReceived;
        }

        _controller?.Close();
        _controller = null;
        _webView = null;
        _environment = null;

        await Task.CompletedTask;

        Logger.Information("WebView2 disposed for Player {PlayerId}", PlayerId);
    }
}
