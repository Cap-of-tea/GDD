using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using GDD.Abstractions;
using GDD.Platform;

namespace GDD.Engines;

public sealed class WebView2ControlAdapter : IBrowserEngine
{
    private readonly CoreWebView2 _webView;
    private readonly Dispatcher _dispatcher;

    public int PlayerId { get; }
    public string UserDataFolder => "";
    public bool IsInitialized => true;
    public string CurrentUrl => _webView.Source ?? string.Empty;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;
    public event EventHandler<string>? NavigationCompleted;
    public event EventHandler<string>? TitleChanged;

    public WebView2ControlAdapter(CoreWebView2 webView, int playerId)
    {
        _webView = webView;
        PlayerId = playerId;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _webView.NavigationCompleted += (_, _) =>
            NavigationCompleted?.Invoke(this, _webView.Source ?? string.Empty);

        _webView.DocumentTitleChanged += (_, _) =>
            TitleChanged?.Invoke(this, _webView.DocumentTitle ?? string.Empty);

        _webView.NotificationReceived += (_, e) =>
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
        };
    }

    public Task InitializeAsync(object? hostHandle, string startUrl)
        => Task.CompletedTask;

    public Task NavigateAsync(string url)
        => _dispatcher.InvokeAsync(() => _webView.Navigate(url)).Task;

    public Task<string> ExecuteJavaScriptAsync(string script)
        => _dispatcher.InvokeAsync(() => _webView.ExecuteScriptAsync(script)).Task.Unwrap();

    public Task CallCdpMethodAsync(string methodName, string parametersJson)
        => _dispatcher.InvokeAsync(() =>
            _webView.CallDevToolsProtocolMethodAsync(methodName, parametersJson)).Task.Unwrap();

    public Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson)
        => _dispatcher.InvokeAsync(() =>
            _webView.CallDevToolsProtocolMethodAsync(methodName, parametersJson)).Task.Unwrap();

    public Task<byte[]> CaptureScreenshotAsync(int quality = 80)
        => _dispatcher.InvokeAsync(async () =>
        {
            var state = await _webView.ExecuteScriptAsync("document.readyState");
            if (!state.Contains("complete"))
            {
                var tcs = new TaskCompletionSource();
                var receiver = _webView.GetDevToolsProtocolEventReceiver("Page.loadEventFired");
                void handler(object? s, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
                {
                    receiver.DevToolsProtocolEventReceived -= handler;
                    tcs.TrySetResult();
                }
                receiver.DevToolsProtocolEventReceived += handler;
                await _webView.CallDevToolsProtocolMethodAsync("Page.enable", "{}");

                state = await _webView.ExecuteScriptAsync("document.readyState");
                if (state.Contains("complete"))
                {
                    receiver.DevToolsProtocolEventReceived -= handler;
                    tcs.TrySetResult();
                }

                await Task.WhenAny(tcs.Task, Task.Delay(5000));
            }

            var metricsJson = await _webView.CallDevToolsProtocolMethodAsync(
                "Page.getLayoutMetrics", "{}");
            using var metrics = System.Text.Json.JsonDocument.Parse(metricsJson);
            var viewport = metrics.RootElement.GetProperty("cssLayoutViewport");
            var x = viewport.GetProperty("pageX").GetDouble();
            var y = viewport.GetProperty("pageY").GetDouble();
            var w = viewport.GetProperty("clientWidth").GetInt32();
            var h = viewport.GetProperty("clientHeight").GetInt32();

            var cdpParams = $"{{\"format\":\"jpeg\",\"quality\":{quality}," +
                $"\"clip\":{{\"x\":{x},\"y\":{y},\"width\":{w},\"height\":{h},\"scale\":1}}}}";
            var resultJson = await _webView.CallDevToolsProtocolMethodAsync(
                "Page.captureScreenshot", cdpParams);
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
            var base64 = doc.RootElement.GetProperty("data").GetString()!;
            return Convert.FromBase64String(base64);
        }).Task.Unwrap();

    public Task InjectScriptOnDocumentCreatedAsync(string script)
        => _dispatcher.InvokeAsync(() =>
            _webView.AddScriptToExecuteOnDocumentCreatedAsync(script)).Task.Unwrap();

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
        => new WebView2CdpSubscription(_webView, eventName);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
