using System.Collections.Concurrent;
using GDD.Abstractions;
using GDD.Models;
using Serilog;

namespace GDD.Services;

public sealed class NotificationInterceptionService
{
    private static readonly ILogger Logger = Log.ForContext<NotificationInterceptionService>();
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly ConcurrentBag<PushNotification> _notifications = new();

    public event EventHandler<PushNotification>? NotificationReceived;

    public NotificationInterceptionService(IMainThreadDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Attach(IBrowserEngine engine, int playerId)
    {
        engine.NotificationReceived += (_, args) =>
        {
            var notification = new PushNotification
            {
                PlayerId = playerId,
                Title = args.Title,
                Body = args.Body,
                IconUri = args.IconUri,
                BadgeUri = args.BadgeUri,
                Tag = args.Tag,
                ReceivedAt = DateTimeOffset.Now
            };

            Logger.Information("Push for Player {PlayerId}: {Title} — {Body}",
                playerId, notification.Title, notification.Body);

            _notifications.Add(notification);

            _dispatcher.InvokeAsync(() =>
            {
                NotificationReceived?.Invoke(this, notification);
            });
        };
    }

    public List<PushNotification> GetNotifications(int? playerId = null)
    {
        var all = _notifications.ToList();
        if (playerId is not null and not 0)
            return all.Where(n => n.PlayerId == playerId).ToList();
        return all;
    }
}
