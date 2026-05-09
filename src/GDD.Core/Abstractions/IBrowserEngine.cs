namespace GDD.Abstractions;

public sealed class NotificationEventArgs : EventArgs
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? IconUri { get; init; }
    public string? BadgeUri { get; init; }
    public string? Tag { get; init; }
}

public interface IBrowserEngine : IAsyncDisposable
{
    int PlayerId { get; }
    string UserDataFolder { get; }
    bool IsInitialized { get; }
    string CurrentUrl { get; }

    Task InitializeAsync(object? hostHandle, string startUrl);
    Task NavigateAsync(string url);
    Task<string> ExecuteJavaScriptAsync(string script);
    Task CallCdpMethodAsync(string methodName, string parametersJson);
    Task<string> CallCdpMethodWithResultAsync(string methodName, string parametersJson);
    Task<byte[]> CaptureScreenshotAsync();
    Task InjectScriptOnDocumentCreatedAsync(string script);
    ICdpEventSubscription SubscribeToCdpEvent(string eventName);

    event EventHandler<NotificationEventArgs>? NotificationReceived;
    event EventHandler<string>? NavigationCompleted;
    event EventHandler<string>? TitleChanged;
}
