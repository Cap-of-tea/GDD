using Microsoft.Web.WebView2.Core;
using GDD.Abstractions;
using GDD.Platform;

namespace GDD.Engines;

public sealed class WebView2ControlAdapter : IBrowserEngine
{
    private readonly CoreWebView2 _webView;

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
    {
        _webView.Navigate(url);
        return Task.CompletedTask;
    }

    public async Task<string> ExecuteJavaScriptAsync(string script)
        => await _webView.ExecuteScriptAsync(script);

    public async Task CallCdpMethodAsync(string methodName, string parametersJson)
        => await _webView.CallDevToolsProtocolMethodAsync(methodName, parametersJson);

    public async Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson)
        => await _webView.CallDevToolsProtocolMethodAsync(methodName, parametersJson);

    public async Task<byte[]> CaptureScreenshotAsync()
    {
        var resultJson = await _webView.CallDevToolsProtocolMethodAsync(
            "Page.captureScreenshot", "{\"format\":\"png\"}");
        using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
        var base64 = doc.RootElement.GetProperty("data").GetString()!;
        return Convert.FromBase64String(base64);
    }

    public async Task InjectScriptOnDocumentCreatedAsync(string script)
        => await _webView.AddScriptToExecuteOnDocumentCreatedAsync(script);

    public ICdpEventSubscription SubscribeToCdpEvent(string eventName)
        => new WebView2CdpSubscription(_webView, eventName);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
