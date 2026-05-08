using System.Collections.ObjectModel;
using Microsoft.Web.WebView2.Core;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class NotificationInterceptionService
{
    private static readonly ILogger Logger = Log.ForContext<NotificationInterceptionService>();

    public ObservableCollection<PushNotification> Notifications { get; } = new();

    public event EventHandler<PushNotification>? NotificationReceived;

    public void Attach(CoreWebView2 webView, int playerId)
    {
        webView.NotificationReceived += (sender, args) =>
        {
            args.Handled = true;

            var notification = new PushNotification
            {
                PlayerId = playerId,
                Title = args.Notification.Title ?? "NOISE",
                Body = args.Notification.Body ?? string.Empty,
                IconUri = args.Notification.IconUri,
                BadgeUri = args.Notification.BadgeUri,
                Tag = args.Notification.Tag,
                ReceivedAt = DateTimeOffset.Now
            };

            Logger.Information("Push for Player {PlayerId}: {Title} — {Body}",
                playerId, notification.Title, notification.Body);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Notifications.Add(notification);
                NotificationReceived?.Invoke(this, notification);
            });
        };
    }
}
